using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace LevelingCompanion.Windows;

/// <summary>Styled after the game's Companion window: the chocobo's panel, then Skills (the plan) and Learned tabs.</summary>
public sealed class MainWindow : Window
{
    private readonly Configuration config;
    private readonly Watcher watcher;
    private readonly Learner learner;
    private readonly SkillBoard board = new();

    internal MainWindow(Configuration config, Watcher watcher, Learner learner)
        : base("Leveling Companion###LevelingCompanionMain")
    {
        (this.config, this.watcher, this.learner) = (config, watcher, learner);
        this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(780, 830), MaximumSize = new Vector2(2000, 2000) };
    }

    public override void PreDraw() => Theme.Push();

    public override void PostDraw() => Theme.Pop();

    public override void Draw()
    {
        CharacterPlan? plan = this.watcher.CurrentPlan();
        if (plan == null)
        {
            ImGui.TextColored(Theme.Dim, "Not logged in.");
            return;
        }
        // Read fresh while the window is open; the watcher itself only reads while the chocobo is out.
        CompanionState state = CompanionState.Read();

        this.DrawPanel(state);
        if (!ImGui.BeginTabBar("##tabs"))
            return;
        if (ImGui.BeginTabItem("Skills"))
        {
            if (this.board.Draw(plan, state))
            {
                this.config.Save();
                this.watcher.Poke();
            }
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem($"Learned ({plan.History.Count})"))
        {
            DrawHistory(plan);
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawPanel(CompanionState state)
    {
        (string name, uint exp, float timeLeft) = CompanionState.Details();
        ImGui.BeginChild("##panel", new Vector2(0, 96 * ImGuiHelpers.GlobalScale), true);

        ImGui.TextColored(Theme.Gold, name == "" ? "Chocobo companion" : name);
        ImGui.SameLine();
        if (state.Summoned)
            ImGui.TextColored(Theme.Green, $"  Summoned · {(int)timeLeft / 60}:{(int)timeLeft % 60:00} left");
        else
            ImGui.TextColored(Theme.Dim, "  Not summoned · plugin dormant");

        if (!state.Obtained)
            ImGui.TextColored(Theme.Red, "No companion yet: complete \"My Little Chocobo\" for your Grand Company.");
        else if (!state.SkillsUnlocked)
            ImGui.TextColored(Theme.Yellow, "Skills locked: complete \"My Feisty Little Chocobo\" (Camp Tranquil, level 30).");
        else
            this.DrawRankLine(state, exp);

        ImGui.TextColored(this.learner.Active ? Theme.Green : Theme.Text, this.learner.Active ? this.learner.Status : this.watcher.Status);
        if (!this.learner.Active && this.learner.Status != "")
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.Dim, $"  Last: {this.learner.Status}");
        }
        ImGui.EndChild();
    }

    private void DrawRankLine(CompanionState state, uint exp)
    {
        ImGui.TextUnformatted($"Rank {state.Rank}");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Gold, $"SP {state.SkillPoints}");
        ImGui.SameLine();
        uint needed = Svc.Data.GetExcelSheet<BuddyRank>().GetRowOrDefault((uint)state.Rank)?.ExpRequired ?? 0;
        if (needed > 0)
            ImGui.ProgressBar((float)exp / needed, new Vector2(200 * ImGuiHelpers.GlobalScale, 0), $"EXP {exp:N0} / {needed:N0}");
        else
            ImGui.TextColored(Theme.Dim, "Max rank");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Dim, $"Defender {state.Defender} · Attacker {state.Attacker} · Healer {state.Healer}");

        ImGui.SameLine(ImGui.GetContentRegionMax().X - 330 * ImGuiHelpers.GlobalScale);
        bool enabled = this.config.Enabled;
        if (ImGui.Checkbox("Learn automatically", ref enabled))
        {
            this.config.Enabled = enabled;
            this.config.Save();
            this.watcher.Poke();
        }
        ImGui.SameLine();
        if (ImGui.Button("Learn now"))
            this.watcher.Poke();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Checks the plan again within a few seconds, e.g. after a give-up.");
        if (state.Rank >= 10 && state.Rank < Skills.MaxRank && ImGui.IsItemHovered())
            ImGui.SetTooltip("Past rank 10 every rank needs a Thavnairian Onion fed at the stable to raise the cap.");
    }

    private static void DrawHistory(CharacterPlan plan)
    {
        if (plan.History.Count == 0)
        {
            ImGui.TextColored(Theme.Dim, "Nothing learned by the plugin yet.");
            return;
        }
        foreach (LearnRecord record in Enumerable.Reverse(plan.History))
        {
            ImGui.TextColored(Theme.Dim, $"{record.When:yyyy-MM-dd HH:mm}");
            ImGui.SameLine();
            ImGui.TextColored(Theme.Gold, Skills.Name(record.Skill));
            ImGui.SameLine();
            ImGui.TextUnformatted($"{record.Skill.Tree} {record.Skill.Level} · planned for rank {record.PlannedRank} · learned at rank {record.CompanionRank}");
        }
    }
}
