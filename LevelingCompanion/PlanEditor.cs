using System.Collections.Generic;
using System.Linq;

namespace LevelingCompanion;

/// <summary>Where a skill stands for one rank of the plan.</summary>
internal enum SkillStatus
{
    /// <summary>The chocobo knows it already.</summary>
    Learned,

    /// <summary>Planned for an earlier rank: done by then, not offered again.</summary>
    PlannedEarlier,

    /// <summary>Planned for this rank.</summary>
    PlannedHere,

    /// <summary>Planned for a later rank; can be moved here when its earlier level is ready by now.</summary>
    PlannedLater,

    /// <summary>The next skill of its tree by this rank: can be added.</summary>
    Available,

    /// <summary>Its earlier level is neither learned nor planned by this rank.</summary>
    Locked,
}

/// <summary>A skill's status at one rank, and whether a click on it does something (and why not).</summary>
internal readonly record struct SkillCell(SkillRef Skill, SkillStatus Status, int PlannedRank, bool Clickable, string Why);

/// <summary>
///     The plan as the window edits it, held to the game's rules so it stays valid: a skill can be added only
///     as the next one of its tree by that rank (every earlier level learned or planned at that rank or
///     before), never twice, and only when the points earned cover it at that rank and every rank after.
///     Removing a skill removes the higher levels of its tree with it, since they would be left without it.
/// </summary>
internal static class PlanEditor
{
    /// <summary>The rank <paramref name="skill" /> is planned for, or 0.</summary>
    internal static int PlannedAt(CharacterPlan plan, SkillRef skill) =>
        plan.Ranks.Where(r => r.Value.Contains(skill)).Select(r => r.Key).DefaultIfEmpty(0).Min();

    /// <summary>The level <paramref name="tree" /> reaches by <paramref name="rank" />: learned, then planned in order.</summary>
    internal static int LevelBy(CharacterPlan plan, CompanionState state, Tree tree, int rank)
    {
        int level = state.Level(tree);
        while (level < Skills.MaxLevel)
        {
            int at = PlannedAt(plan, new SkillRef(tree, level + 1));
            if (at == 0 || at > rank)
                break;
            level++;
        }
        return level;
    }

    /// <summary>Points still free from <paramref name="from" /> up to (not including) <paramref name="until" />: the least left at any of those ranks.</summary>
    internal static int FreePoints(Dictionary<int, RankCheck> checks, int from, int until = Skills.MaxRank + 1) =>
        Enumerable.Range(from, until - from).Select(r => checks[r].PointsLeft).DefaultIfEmpty(0).Min();

    internal static SkillCell Cell(CharacterPlan plan, CompanionState state, Dictionary<int, RankCheck> checks, SkillRef skill, int rank)
    {
        string name = Skills.Name(skill);
        int cost = Skills.Cost(skill.Level);
        if (state.Learned(skill))
            return new SkillCell(skill, SkillStatus.Learned, 0, false, $"{name} is learned.");

        int at = PlannedAt(plan, skill);
        bool ready = LevelBy(plan, state, skill.Tree, rank) >= skill.Level - 1;
        if (at == rank)
            return new SkillCell(skill, SkillStatus.PlannedHere, at, true, RemoveWhy(plan, skill));
        if (at != 0 && at < rank)
            return new SkillCell(skill, SkillStatus.PlannedEarlier, at, false, $"Already planned for rank {at}.");
        if (!ready)
            return new SkillCell(skill, at != 0 ? SkillStatus.PlannedLater : SkillStatus.Locked, at, false,
                $"Needs {Skills.Name(new SkillRef(skill.Tree, skill.Level - 1))} ({skill.Tree} {skill.Level - 1}) by this rank first."
                + (at != 0 ? $" Planned for rank {at}." : ""));

        int free = at != 0 ? FreePoints(checks, rank, at) : FreePoints(checks, rank);
        SkillStatus status = at != 0 ? SkillStatus.PlannedLater : SkillStatus.Available;
        if (free < cost)
            return new SkillCell(skill, status, at, false, $"Costs {cost} SP; only {System.Math.Max(free, 0)} SP free from this rank on.");
        return new SkillCell(skill, status, at, true,
            at != 0 ? $"Planned for rank {at}. Click to learn it at this rank instead ({cost} SP)." : $"Click to learn at this rank ({cost} SP).");
    }

    /// <summary>Applies a click: adds an available skill, moves a later one here, or removes a planned one (and its tree above it).</summary>
    internal static void Click(CharacterPlan plan, SkillCell cell, int rank)
    {
        if (!cell.Clickable)
            return;
        if (cell.Status == SkillStatus.PlannedHere)
        {
            foreach (List<SkillRef> skills in plan.Ranks.Values)
                skills.RemoveAll(s => s.Tree == cell.Skill.Tree && s.Level >= cell.Skill.Level);
        }
        else
        {
            foreach (List<SkillRef> skills in plan.Ranks.Values)
                skills.Remove(cell.Skill);
            if (!plan.Ranks.TryGetValue(rank, out List<SkillRef>? here))
                plan.Ranks[rank] = here = [];
            here.Add(cell.Skill);
        }
        plan.Ranks = plan.Ranks.Where(r => r.Value.Count > 0).ToDictionary(r => r.Key, r => r.Value);
    }

    private static string RemoveWhy(CharacterPlan plan, SkillRef skill)
    {
        List<string> above = plan.Ranks.Values.SelectMany(s => s)
                                 .Where(s => s.Tree == skill.Tree && s.Level > skill.Level)
                                 .OrderBy(s => s.Level).Select(Skills.Name).ToList();
        return above.Count == 0
            ? "Planned for this rank. Click to remove."
            : $"Planned for this rank. Click to remove it, together with {string.Join(", ", above)}.";
    }
}
