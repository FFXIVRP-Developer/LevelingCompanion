using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace LevelingCompanion.Windows;

/// <summary>The companion's status, the plan rank by rank, what was learned, and the click recorder.</summary>
public sealed class MainWindow : Window
{
    private static readonly Vector4 Green  = new(0.4f, 0.9f, 0.4f, 1);
    private static readonly Vector4 Red    = new(1f, 0.45f, 0.45f, 1);
    private static readonly Vector4 Yellow = new(1f, 0.85f, 0.35f, 1);

    private readonly Configuration config;
    private readonly Watcher watcher;
    private readonly Learner learner;
    private readonly ClickRecorder recorder;

    internal MainWindow(Configuration config, Watcher watcher, Learner learner, ClickRecorder recorder)
        : base("Leveling Companion###LevelingCompanionMain")
    {
        (this.config, this.watcher, this.learner, this.recorder) = (config, watcher, learner, recorder);
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 480), MaximumSize = new Vector2(2000, 2000) };
    }

    public override void Draw()
    {
        CharacterPlan? plan = this.watcher.CurrentPlan();
        if (plan == null)
        {
            ImGui.TextDisabled("Not logged in.");
            return;
        }
        // Read fresh while the window is open; the watcher itself only reads while the chocobo is out.
        CompanionState state = CompanionState.Read();

        this.DrawStatus(state);
        ImGui.Separator();
        if (ImGui.BeginTabBar("##tabs"))
        {
            if (ImGui.BeginTabItem("Plan"))
            {
                this.DrawPlan(plan, state);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem($"Learned ({plan.History.Count})"))
            {
                DrawHistory(plan);
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Recorder"))
            {
                this.DrawRecorder();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawStatus(CompanionState state)
    {
        if (!state.Obtained)
            ImGui.TextColored(Red, "No companion yet: complete \"My Little Chocobo\" for your Grand Company.");
        else if (!state.SkillsUnlocked)
            ImGui.TextColored(Yellow, "Skills locked: complete \"My Feisty Little Chocobo\" (Camp Tranquil, level 30).");
        else
            ImGui.TextUnformatted($"Rank {state.Rank}   SP {state.SkillPoints}   Defender {state.Defender}   Attacker {state.Attacker}   Healer {state.Healer}");

        if (state.Summoned)
            ImGui.TextColored(Green, "Chocobo summoned: active.");
        else
            ImGui.TextDisabled("Chocobo not summoned: the plugin is dormant.");

        ImGui.TextWrapped(this.learner.Active ? this.learner.Status : this.watcher.Status);
        if (!this.learner.Active && this.learner.Status != "")
            ImGui.TextDisabled($"Last: {this.learner.Status}");

        bool enabled = this.config.Enabled;
        if (ImGui.Checkbox("Learn planned skills by myself", ref enabled))
        {
            this.config.Enabled = enabled;
            this.config.Save();
            this.watcher.Poke();
        }
        ImGui.SameLine();
        if (ImGui.Button("Learn now"))
            this.watcher.Poke();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Asks the plan again at the next check (a few seconds), e.g. after a give-up.");
        if (state.Rank >= 10 && state.Rank < Skills.MaxRank)
            ImGui.TextDisabled("Past rank 10 every rank needs a Thavnairian Onion fed at the stable to raise the cap.");
    }

    private void DrawPlan(CharacterPlan plan, CompanionState state)
    {
        ImGui.TextDisabled("Each rank lists the skills to learn once it is reached, in order. Skills of a tree go in order and the level-N skill costs N SP.");
        Dictionary<int, RankCheck> checks = Planner.Check(plan, state);
        bool changed = false;

        if (ImGui.BeginChild("##ranks", Vector2.Zero, true))
        {
            for (int rank = 1; rank <= Skills.MaxRank; rank++)
                changed |= this.DrawRank(plan, state, rank, checks[rank]);
        }
        ImGui.EndChild();

        if (changed)
        {
            plan.Ranks = plan.Ranks.Where(r => r.Value.Count > 0).ToDictionary(r => r.Key, r => r.Value);
            this.config.Save();
            this.watcher.Poke();
        }
    }

    private bool DrawRank(CharacterPlan plan, CompanionState state, int rank, RankCheck check)
    {
        List<SkillRef> skills = plan.Ranks.GetValueOrDefault(rank) ?? [];
        bool changed = false;
        ImGui.PushID(rank);

        Vector4 color = rank <= state.Rank ? Green : new Vector4(0.85f, 0.85f, 0.85f, 1);
        ImGui.TextColored(color, $"Rank {rank}");
        ImGui.SameLine();
        ImGui.TextDisabled($"+{Skills.PointsAt(rank)} SP, {check.PointsLeft} left after this rank");

        for (int i = 0; i < skills.Count; i++)
        {
            SkillRef skill = skills[i];
            ImGui.PushID(i);
            ImGui.Indent();
            if (ImGui.SmallButton("x"))
            {
                skills.RemoveAt(i);
                changed = true;
            }
            ImGui.SameLine();
            string text = $"{skill.Tree} {skill.Level}: {Skills.Name(skill)} ({Skills.Cost(skill.Level)} SP)";
            if (state.Learned(skill))
                ImGui.TextColored(Green, text + "  learned");
            else
                ImGui.TextUnformatted(text);
            ImGui.Unindent();
            ImGui.PopID();
            if (changed)
                break;
        }

        foreach (string problem in check.Problems)
        {
            ImGui.Indent();
            ImGui.TextColored(Red, problem);
            ImGui.Unindent();
        }

        ImGui.Indent();
        ImGui.SetNextItemWidth(260);
        if (ImGui.BeginCombo("##add", "Add a skill"))
        {
            foreach (SkillRef skill in Skills.All())
                if (ImGui.Selectable($"{skill.Tree} {skill.Level}: {Skills.Name(skill)} ({Skills.Cost(skill.Level)} SP)"))
                {
                    skills.Add(skill);
                    plan.Ranks[rank] = skills;
                    changed = true;
                }
            ImGui.EndCombo();
        }
        ImGui.Unindent();
        ImGui.Spacing();
        ImGui.PopID();
        return changed;
    }

    private static void DrawHistory(CharacterPlan plan)
    {
        if (plan.History.Count == 0)
        {
            ImGui.TextDisabled("Nothing learned by the plugin yet.");
            return;
        }
        foreach (LearnRecord record in Enumerable.Reverse(plan.History))
            ImGui.TextUnformatted($"{record.When:yyyy-MM-dd HH:mm}  {Skills.Name(record.Skill)} ({record.Skill.Tree} {record.Skill.Level}), planned for rank {record.PlannedRank}, at rank {record.CompanionRank}");
    }

    private void DrawRecorder()
    {
        ImGui.TextWrapped("The click on a skill in the Companion window is not documented yet. Turn recording on, learn one skill by hand, "
                        + "turn it off: the lines below (also in the Dalamud log) show what the click sends, so it can be built in.");
        if (ImGui.Button(this.recorder.Recording ? "Stop recording" : "Start recording"))
        {
            if (this.recorder.Recording)
                this.recorder.Stop();
            else
                this.recorder.Start();
        }
        ImGui.SameLine();
        if (ImGui.Button("Copy"))
            ImGui.SetClipboardText(string.Join("\n", this.recorder.Lines));
        foreach (string line in this.recorder.Lines)
            ImGui.TextUnformatted(line);
    }
}
