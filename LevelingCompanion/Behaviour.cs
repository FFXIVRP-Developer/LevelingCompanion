using System;
using System.Collections.Generic;
using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace LevelingCompanion;

/// <summary>The companion's stances; the values are their BuddyAction rows.</summary>
public enum Stance : uint
{
    Free     = 4,
    Defender = 5,
    Attacker = 6,
    Healer   = 7,
}

/// <summary>
///     Your HP below the set percentage: the chocobo goes to Healer stance; at or above it, back to the chosen
///     stance. Each check schedules the next one (no per-frame callback): four a second in combat, one a second
///     out of it, only while the chocobo is summoned and the option is on; the watcher's slow check switches it
///     on and off. A stance is ordered again at most every
///     <see cref="Reorder" />, so a stance the game does not report back cannot be spammed.
/// </summary>
internal sealed class Behaviour(Configuration config) : IDisposable
{
    private static readonly TimeSpan InCombat    = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan OutOfCombat = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Reorder     = TimeSpan.FromSeconds(10);

    private DateTime orderedAt;
    private Stance? ordered;
    private CancellationTokenSource? loop;

    internal bool Running { get; private set; }

    /// <summary>The last stance changes ordered, newest first, for the window.</summary>
    internal List<string> Recent { get; } = [];

    /// <summary>The stance the game reports (CompanionInfo.ActiveCommand), when it is one of the four.</summary>
    internal static unsafe Stance? Current()
    {
        UIState* ui = UIState.Instance();
        if (ui == null)
            return null;
        uint command = ui->Buddy.CompanionInfo.ActiveCommand;
        return Enum.IsDefined(typeof(Stance), command) ? (Stance)command : null;
    }

    /// <summary>Your HP in percent, or null when not logged in.</summary>
    internal static float? HealthPercent()
    {
        if (Svc.Objects.LocalPlayer is not { MaxHp: > 0 } player)
            return null;
        return 100f * player.CurrentHp / player.MaxHp;
    }

    /// <summary>The stance wanted at <paramref name="health" /> percent.</summary>
    internal Stance Wanted(float health) => health < config.HealBelowPercent ? Stance.Healer : config.NormalStance;

    internal static string Name(Stance stance) =>
        Svc.Data.GetExcelSheet<BuddyAction>().GetRowOrDefault((uint)stance)?.Name.ExtractText() ?? stance.ToString();

    internal static string Description(Stance stance) =>
        Svc.Data.GetExcelSheet<BuddyAction>().GetRowOrDefault((uint)stance)?.Description.ExtractText() ?? "";

    internal static uint Icon(Stance stance) =>
        (uint)(Svc.Data.GetExcelSheet<BuddyAction>().GetRowOrDefault((uint)stance)?.Icon ?? 0);

    /// <summary>Called by the watcher's slow check: runs only while it should.</summary>
    internal void SetRunning(bool run)
    {
        if (run == this.Running)
            return;
        this.Running = run;
        this.ordered = null;
        this.loop?.Cancel();
        this.loop = null;
        if (run)
        {
            this.loop = new CancellationTokenSource();
            this.Schedule(this.loop.Token);
        }
    }

    public void Dispose() => this.SetRunning(false);

    private void Schedule(CancellationToken token)
    {
        TimeSpan delay = Svc.Condition[ConditionFlag.InCombat] ? InCombat : OutOfCombat;
        Svc.Framework.RunOnTick(() => this.Tick(token), delay, cancellationToken: token);
    }

    private void Tick(CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return;
        try
        {
            this.Check();
        }
        catch (Exception e)
        {
            Svc.Log.Error(e, "LevelingCompanion: stance check");
        }
        if (!token.IsCancellationRequested)
            this.Schedule(token);
    }

    private void Check()
    {
        if (!config.BehaviourEnabled || !CompanionState.IsSummoned())
        {
            this.SetRunning(false);
            return;
        }
        if (HealthPercent() is not { } health || health <= 0 || Mounted() || GenericHelpers.IsOccupied() || Svc.Condition[ConditionFlag.Mounted])
            return;

        Stance wanted = this.Wanted(health);
        if (Current() == wanted)
            return;
        if (this.ordered == wanted && DateTime.Now - this.orderedAt < Reorder)
            return;
        this.Order(wanted, health);
    }

    private unsafe void Order(Stance stance, float health)
    {
        ActionManager* actions = ActionManager.Instance();
        if (actions->GetActionStatus(ActionType.BuddyAction, (uint)stance) != 0)
            return;
        actions->UseAction(ActionType.BuddyAction, (uint)stance);
        (this.ordered, this.orderedAt) = (stance, DateTime.Now);
        string line = $"{DateTime.Now:HH:mm:ss}  {Name(stance)} at {health:0}% HP";
        this.Recent.Insert(0, line);
        if (this.Recent.Count > 5)
            this.Recent.RemoveAt(5);
        Svc.Log.Info($"LevelingCompanion: {line} (game reported {Current()?.ToString() ?? "another command"})");
    }

    private static unsafe bool Mounted()
    {
        UIState* ui = UIState.Instance();
        return ui != null && ui->Buddy.CompanionInfo.Mounted;
    }
}
