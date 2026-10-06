using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;

namespace LevelingCompanion.Windows;

/// <summary>
///     The plan drawn like the game's Companion > Skills screen: the three trees side by side, ten skills each
///     from level 1 down, with a rank strip on top choosing which rank's picks the board edits. Clicks go
///     through <see cref="PlanEditor" />, so only a tree's next skill can be added and nothing twice.
/// </summary>
internal sealed class SkillBoard
{
    /// <summary>Stance icons of the game's companion commands, used as tree headings.</summary>
    private static readonly uint[] TreeIcons = [903, 904, 905];

    private int rank;

    /// <summary>Draws the board; true when the plan changed.</summary>
    internal bool Draw(CharacterPlan plan, CompanionState state)
    {
        if (this.rank == 0)
            this.rank = System.Math.Clamp(state.Rank, 1, Skills.MaxRank);

        Dictionary<int, RankCheck> checks = Planner.Check(plan, state);
        this.DrawRankStrip(plan, state, checks);
        DrawRankSummary(plan, checks, this.rank);

        bool changed = false;
        float width = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X * 2) / 3;
        foreach (Tree tree in Skills.Trees)
        {
            if (tree != Tree.Defender)
                ImGui.SameLine();
            changed |= this.DrawTree(plan, state, checks, tree, width);
        }
        return changed;
    }

    private void DrawRankStrip(CharacterPlan plan, CompanionState state, Dictionary<int, RankCheck> checks)
    {
        ImGui.TextColored(Theme.Gold, "Plan for rank");
        float size = 26 * ImGuiHelpers.GlobalScale;
        for (int r = 1; r <= Skills.MaxRank; r++)
        {
            ImGui.SameLine(0, r == 1 ? 8 : 3);
            int picks = plan.Ranks.GetValueOrDefault(r)?.Count ?? 0;
            bool selected = r == this.rank;
            bool problem = checks[r].Problems.Count > 0;
            ImGui.PushStyleColor(ImGuiCol.Button, selected ? Theme.Trim : picks > 0 ? Theme.PanelHigh : Theme.Panel);
            ImGui.PushStyleColor(ImGuiCol.Text, problem ? Theme.Red : r <= state.Rank ? Theme.Gold : Theme.Dim);
            if (ImGui.Button($"{r}##rank{r}", new Vector2(size, size)))
                this.rank = r;
            ImGui.PopStyleColor(2);

            if (picks > 0)
            {
                Vector2 max = ImGui.GetItemRectMax();
                ImGui.GetWindowDrawList().AddCircleFilled(new Vector2(max.X - 4, ImGui.GetItemRectMin().Y + 4), 3, Theme.U32(Theme.Green));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(RankTooltip(plan, checks, r, state));
        }
    }

    private static string RankTooltip(CharacterPlan plan, Dictionary<int, RankCheck> checks, int r, CompanionState state)
    {
        List<SkillRef> picks = plan.Ranks.GetValueOrDefault(r) ?? [];
        string head = $"Rank {r}{(r <= state.Rank ? " (reached)" : "")}: +{Skills.PointsAt(r)} SP, {checks[r].PointsLeft} SP left after it";
        string body = picks.Count == 0 ? "Nothing planned." : string.Join("\n", picks.Select((s, i) => $"{i + 1}. {Skills.Name(s)} ({s.Tree} {s.Level}, {Skills.Cost(s.Level)} SP)"));
        string problems = checks[r].Problems.Count == 0 ? "" : "\n" + string.Join("\n", checks[r].Problems);
        return $"{head}\n{body}{problems}";
    }

    private static void DrawRankSummary(CharacterPlan plan, Dictionary<int, RankCheck> checks, int r)
    {
        int before = r == 1 ? 0 : checks[r - 1].PointsLeft;
        ImGui.TextColored(Theme.Gold, $"Rank {r}");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Dim, $"+{Skills.PointsAt(r)} SP   ·   {before + Skills.PointsAt(r)} SP to spend   ·   {checks[r].PointsLeft} SP left after");
        List<SkillRef> picks = plan.Ranks.GetValueOrDefault(r) ?? [];
        if (picks.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.Green, "   Order: " + string.Join(" → ", picks.Select(Skills.Name)));
        }
        foreach (string problem in checks[r].Problems)
            ImGui.TextColored(Theme.Red, problem);
        ImGui.Spacing();
    }

    private bool DrawTree(CharacterPlan plan, CompanionState state, Dictionary<int, RankCheck> checks, Tree tree, float width)
    {
        bool changed = false;
        float scale = ImGuiHelpers.GlobalScale;
        ImGui.BeginChild($"##tree{tree}", new Vector2(width, 0), true, ImGuiWindowFlags.NoScrollbar);

        DrawIcon(TreeIcons[(int)tree], 32 * scale, Vector4.One);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(Theme.Gold, tree.ToString());
        int learned = state.Level(tree);
        int by = PlanEditor.LevelBy(plan, state, tree, this.rank);
        ImGui.TextColored(Theme.Dim, by > learned ? $"Lv {learned} → {by} by rank {this.rank}" : $"Lv {learned}");
        ImGui.EndGroup();
        ImGui.Separator();

        float icon = 40 * scale;
        Vector2? previous = null;
        for (int level = 1; level <= Skills.MaxLevel; level++)
        {
            SkillCell cell = PlanEditor.Cell(plan, state, checks, new SkillRef(tree, level), this.rank);
            Vector2 top = ImGui.GetCursorScreenPos();
            if (previous is { } p) // the line joining the tree's skills, as in the game
                ImGui.GetWindowDrawList().AddLine(p, top + new Vector2(icon / 2 + 4, 0), Theme.U32(LineColor(cell.Status)), 2 * scale);
            if (DrawCell(cell, icon))
            {
                PlanEditor.Click(plan, cell, this.rank);
                changed = true;
            }
            previous = top + new Vector2(icon / 2 + 4, icon + 4);
        }
        ImGui.EndChild();
        return changed;
    }

    /// <summary>One skill: icon with a frame showing its status, name, and what a click does. True when clicked.</summary>
    private static bool DrawCell(SkillCell cell, float icon)
    {
        SkillRef skill = cell.Skill;
        Vector2 pos = ImGui.GetCursorScreenPos();
        Vector2 size = new(ImGui.GetContentRegionAvail().X, icon + 8);
        ImGui.InvisibleButton($"##cell{skill.Tree}{skill.Level}", size);
        bool hovered = ImGui.IsItemHovered();
        bool clicked = cell.Clickable && ImGui.IsItemClicked(ImGuiMouseButton.Left);

        (Vector4 frame, Vector4 tint, Vector4 name, string note) = Look(cell);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        if (cell.Status == SkillStatus.PlannedHere || hovered && cell.Clickable)
            draw.AddRectFilled(pos, pos + size, Theme.U32(cell.Status == SkillStatus.PlannedHere ? Theme.Alpha(Theme.Green, 0.12f) : Theme.Alpha(Theme.Gold, 0.10f)), 4);

        Vector2 iconMin = pos + new Vector2(4, 4);
        Vector2 iconMax = iconMin + new Vector2(icon, icon);
        draw.AddImage(Svc.Texture.GetFromGameIcon(new GameIconLookup(Skills.Icon(skill))).GetWrapOrEmpty().Handle, iconMin, iconMax, Vector2.Zero, Vector2.One, Theme.U32(tint));
        draw.AddRect(iconMin - Vector2.One, iconMax + Vector2.One, Theme.U32(frame), 4, ImDrawFlags.None, cell.Status is SkillStatus.PlannedHere or SkillStatus.Learned ? 2.5f : 1.5f);

        float x = iconMax.X + 8;
        draw.AddText(new Vector2(x, pos.Y + 5), Theme.U32(name), Skills.Name(skill));
        draw.AddText(new Vector2(x, pos.Y + 5 + ImGui.GetTextLineHeight()), Theme.U32(Theme.Dim), $"Lv {skill.Level} · {Skills.Cost(skill.Level)} SP · {note}");

        if (hovered)
            Tooltip(cell);
        return clicked;
    }

    private static (Vector4 Frame, Vector4 Tint, Vector4 Name, string Note) Look(SkillCell cell) => cell.Status switch
    {
        SkillStatus.Learned        => (Theme.Gold, Vector4.One, Theme.Gold, "learned"),
        SkillStatus.PlannedHere    => (Theme.Green, Vector4.One, Theme.Green, "this rank"),
        SkillStatus.PlannedEarlier => (Theme.Blue, new Vector4(1, 1, 1, 0.6f), Theme.Blue, $"rank {cell.PlannedRank}"),
        SkillStatus.PlannedLater   => (Theme.Alpha(Theme.Blue, 0.6f), new Vector4(0.7f, 0.7f, 0.7f, 0.6f), Theme.Dim, $"rank {cell.PlannedRank}"),
        SkillStatus.Available when cell.Clickable => (Theme.Text, Vector4.One, Theme.Text, "available"),
        SkillStatus.Available      => (Theme.Red, new Vector4(0.6f, 0.6f, 0.6f, 0.8f), Theme.Dim, "not enough SP"),
        _                          => (Theme.Alpha(Theme.Dim, 0.5f), new Vector4(0.30f, 0.30f, 0.30f, 0.9f), Theme.Alpha(Theme.Dim, 0.7f), "locked"),
    };

    private static Vector4 LineColor(SkillStatus status) => status switch
    {
        SkillStatus.Learned                                  => Theme.Gold,
        SkillStatus.PlannedEarlier or SkillStatus.PlannedHere => Theme.Alpha(Theme.Green, 0.8f),
        _                                                    => Theme.Alpha(Theme.Dim, 0.4f),
    };

    private static void Tooltip(SkillCell cell)
    {
        SkillRef skill = cell.Skill;
        ImGui.BeginTooltip();
        DrawIcon(Skills.Icon(skill), 40 * ImGuiHelpers.GlobalScale, Vector4.One);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(Theme.Gold, Skills.Name(skill));
        ImGui.TextColored(Theme.Dim, $"{skill.Tree} Lv {skill.Level} · {(Skills.IsAction(skill) ? "Action" : "Trait")} · {Skills.Cost(skill.Level)} SP");
        ImGui.EndGroup();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22);
        string description = Skills.Description(skill);
        if (description != "")
            ImGui.TextUnformatted(description);
        ImGui.Separator();
        ImGui.TextColored(cell.Clickable ? Theme.Green : Theme.Yellow, cell.Why);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void DrawIcon(uint icon, float size, Vector4 tint) =>
        ImGui.Image(Svc.Texture.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty().Handle, new Vector2(size, size), Vector2.Zero, Vector2.One, tint);
}
