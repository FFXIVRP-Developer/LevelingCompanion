using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
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
///     stance. Runs on the framework (four checks a second) only while the chocobo is summoned and the option
///     is on; the watcher's slow check switches it on and off. A stance is ordered again at most every
///     <see cref="Reorder" />, so a stance the game does not report back cannot be spammed.
/// </summary>
internal sealed class Behaviour(Configuration config) : IDisposable
{
    private static readonly TimeSpan Step    = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Reorder = TimeSpan.FromSeconds(10);

    private DateTime lastStep, orderedAt;
    private Stance? ordered;

    internal bool Running { get; private set; }

    /// <summary>The last stance change ordered, for the window.</summary>
    internal string Last { get; private set; } = "";

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

    internal static uint Icon(Stance stance) =>
        (uint)(Svc.Data.GetExcelSheet<BuddyAction>().GetRowOrDefault((uint)stance)?.Icon ?? 0);

    /// <summary>Called by the watcher's slow check: runs only while it should.</summary>
    internal void SetRunning(bool run)
    {
        if (run == this.Running)
            return;
        this.Running = run;
        this.ordered = null;
        if (run)
            Svc.Framework.Update += this.OnUpdate;
        else
            Svc.Framework.Update -= this.OnUpdate;
    }

    public void Dispose() => this.SetRunning(false);

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.Now - this.lastStep < Step)
            return;
        this.lastStep = DateTime.Now;

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
        this.Last = $"{DateTime.Now:HH:mm:ss} {Name(stance)} at {health:0}% HP (game reported {Current()?.ToString() ?? "another command"})";
        Svc.Log.Info($"LevelingCompanion: {this.Last}");
    }

    private static unsafe bool Mounted()
    {
        UIState* ui = UIState.Instance();
        return ui != null && ui->Buddy.CompanionInfo.Mounted;
    }
}
