using System;
using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;

namespace LevelingCompanion;

/// <summary>
///     Dormant by design: no per-frame work. One delayed check every few seconds reads only the summon timer;
///     with the chocobo out, it reads the companion and asks the planner, and only when the companion's state
///     changed since the last answer. The learner, which does run every frame, is started only for a skill the
///     points cover and stops by itself. The stance behaviour runs only while the chocobo is out and it is on.
/// </summary>
internal sealed class Watcher : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private readonly Configuration config;
    private readonly Learner learner;
    private readonly Behaviour behaviour;
    private readonly CancellationTokenSource stop = new();

    /// <summary>The companion state the last answer was for; the planner is not asked again until it changes.</summary>
    private (ulong Character, CompanionState State)? handled;

    internal Watcher(Configuration config, Learner learner, Behaviour behaviour)
    {
        this.config    = config;
        this.learner   = learner;
        this.behaviour = behaviour;
        this.Schedule();
    }

    /// <summary>The companion as last read (only refreshed while summoned).</summary>
    internal CompanionState Last { get; private set; }

    /// <summary>What the plan asks for now, for the window.</summary>
    internal string Status { get; private set; } = "Dormant: no chocobo summoned.";

    internal bool Dormant { get; private set; } = true;

    /// <summary>Forget the last answer: the plan changed, or the player asked to learn now.</summary>
    internal void Poke() => this.handled = null;

    /// <summary>The plan of the character logged in, created empty on first use. Null when not logged in.</summary>
    internal CharacterPlan? CurrentPlan()
    {
        ulong id = Svc.PlayerState.ContentId;
        if (id == 0)
            return null;
        if (!this.config.Characters.TryGetValue(id, out CharacterPlan? plan))
        {
            plan = new CharacterPlan();
            this.config.Characters[id] = plan;
        }
        plan.Name = Svc.PlayerState.CharacterName;
        // Nothing planned yet: the default plan (PlanDefaults), so a new character or profile starts with it.
        if (PlanDefaults.FillFromDefault(plan, this.config.DefaultRanks))
            this.config.Save();
        return plan;
    }

    public void Dispose() => this.stop.Cancel();

    private void Schedule()
    {
        if (!this.stop.IsCancellationRequested)
            Svc.Framework.RunOnTick(this.Check, Interval, cancellationToken: this.stop.Token);
    }

    private void Check()
    {
        try
        {
            this.Look();
        }
        catch (Exception e)
        {
            Svc.Log.Error(e, "LevelingCompanion: check");
        }
        this.Schedule();
    }

    /// <summary>The server info bar entry, refreshed on every check.</summary>
    internal ServerBar? Bar { get; set; }

    private void Look()
    {
        this.Bar?.Update();
        bool summoned = Svc.ClientState.IsLoggedIn && CompanionState.IsSummoned();
        this.behaviour.SetRunning(summoned && this.config.BehaviourEnabled);
        if (this.learner.Active)
            return;
        if (!summoned)
        {
            this.Dormant = true;
            this.Status  = "Dormant: no chocobo summoned.";
            return;
        }
        this.Dormant = false;

        ulong character = Svc.PlayerState.ContentId;
        CompanionState state = CompanionState.Read();
        this.Last = state;
        if (this.handled == (character, state))
            return;

        CharacterPlan? plan = this.CurrentPlan();
        if (plan == null)
            return;
        if (!state.SkillsUnlocked)
        {
            this.Status  = "Skills are locked: complete \"My Feisty Little Chocobo\".";
            this.handled = (character, state);
            return;
        }

        NextStep next = Planner.Next(plan, state);
        this.Status = next.Status;
        if (next.Learn is not { } skill || !this.config.Enabled)
        {
            this.handled = (character, state);
            return;
        }
        if (Svc.Condition[ConditionFlag.InCombat] || GenericHelpers.IsOccupied())
            return; // asked again next check, unchanged state or not
        this.handled = (character, state);
        this.learner.Start(skill, next.PlannedRank, plan);
    }
}
