using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace LevelingCompanion;

/// <summary>
///     Learns one skill: opens the Companion window on its Skills tab, clicks the skill, presses Yes on
///     "Spend N SP to acquire “skill”?" for that skill only, and waits for the game to count it learned.
///     Runs on the framework only while a skill is being learned; it unsubscribes as soon as it ends.
/// </summary>
internal sealed class Learner(SkillPrompt prompt, Configuration config) : IDisposable
{
    private const int SkillsTab = 1;

    private static readonly TimeSpan Step    = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan GiveUp  = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Confirm = TimeSpan.FromSeconds(5);

    private SkillRef skill;
    private int plannedRank;
    private CharacterPlan? plan;
    private DateTime started, lastStep, confirmedAt;
    private bool openedWindow, clickSent;

    internal bool Active { get; private set; }

    /// <summary>What the learner is doing or last did, for the window.</summary>
    internal string Status { get; private set; } = "";

    internal void Start(SkillRef target, int rank, CharacterPlan forPlan)
    {
        if (this.Active)
            return;
        (this.skill, this.plannedRank, this.plan) = (target, rank, forPlan);
        (this.started, this.confirmedAt, this.openedWindow, this.clickSent) = (DateTime.Now, default, false, false);
        this.Active = true;
        this.Status = $"Learning {Skills.Name(target)}.";
        Svc.Framework.Update += this.OnUpdate;
    }

    internal void Stop(string why)
    {
        if (!this.Active)
            return;
        Svc.Framework.Update -= this.OnUpdate;
        this.Active = false;
        this.Status = why;
        Svc.Log.Info($"LevelingCompanion: {why}");
        if (this.openedWindow)
            CloseWindow();
    }

    public void Dispose()
    {
        if (this.Active)
            Svc.Framework.Update -= this.OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.Now - this.lastStep < Step)
            return;
        this.lastStep = DateTime.Now;

        CompanionState state = CompanionState.Read();
        if (state.Learned(this.skill))
        {
            this.plan!.History.Add(new LearnRecord(this.skill, this.plannedRank, state.Rank, DateTime.Now));
            config.Save();
            this.Stop($"Learned {Skills.Name(this.skill)} (planned for rank {this.plannedRank}).");
            return;
        }
        if (!state.Summoned || Svc.Condition[ConditionFlag.InCombat])
        {
            this.Stop($"Stopped learning {Skills.Name(this.skill)}: the chocobo left or combat started.");
            return;
        }
        if (this.confirmedAt != default)
        {
            if (DateTime.Now - this.confirmedAt > Confirm)
                this.Stop($"Pressed Yes for {Skills.Name(this.skill)} but the game did not count it learned.");
            return;
        }
        if (DateTime.Now - this.started > GiveUp)
        {
            this.Stop($"Gave up on {Skills.Name(this.skill)} after {GiveUp.TotalSeconds:0} s; retries when something changes or on Learn now.");
            return;
        }
        this.Advance();
    }

    private unsafe void Advance()
    {
        string? text = prompt.Showing();
        if (text != null)
        {
            if (prompt.AsksFor(text, this.skill))
            {
                Svc.Log.Info($"LevelingCompanion: Yes to \"{text}\"");
                prompt.Yes();
                this.confirmedAt = DateTime.Now;
            }
            return; // another skill's prompt: the player's own, left alone
        }

        if (!GenericHelpers.TryGetAddonByName("Buddy", out AddonBuddy* buddy) || !GenericHelpers.IsAddonReady(&buddy->AtkUnitBase))
        {
            if (!this.openedWindow)
            {
                AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy)->Show();
                this.openedWindow = true;
            }
            return;
        }
        if (buddy->TabIndex != SkillsTab)
        {
            buddy->SetTab(SkillsTab);
            return;
        }
        if (!this.clickSent)
        {
            this.clickSent = true;
            this.Status = SkillClick.Send(buddy, this.skill)
                ? $"Clicked {Skills.Name(this.skill)}; waiting for the prompt."
                : $"Click {Skills.Name(this.skill)} ({this.skill.Tree} {this.skill.Level}) in the Skills tab; Yes is pressed for you.";
        }
    }

    private static unsafe void CloseWindow()
    {
        if (GenericHelpers.TryGetAddonByName("Buddy", out AtkUnitBase* addon))
            addon->Close(true);
    }
}
