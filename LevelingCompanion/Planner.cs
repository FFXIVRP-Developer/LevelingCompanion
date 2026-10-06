using System.Collections.Generic;
using System.Linq;

namespace LevelingCompanion;

/// <summary>What the plan asks for now: a skill to learn, a skill waiting for points, or nothing.</summary>
internal readonly record struct NextStep(SkillRef? Learn, SkillRef? WaitingFor, int PlannedRank, string Status);

/// <summary>A note on one rank of the plan, from <see cref="Planner.Check" />.</summary>
internal readonly record struct RankCheck(int PointsLeft, List<string> Problems);

/// <summary>
///     Reads a plan against the companion. Ranks are taken in order and skills in the order listed; a skill
///     already learned is passed over, a skill whose earlier level in its tree is not learned yet is passed
///     over too (it is not that rank's fault), and a skill the points do not cover stops the walk, so a later
///     rank never spends the points an earlier one is waiting for.
/// </summary>
internal static class Planner
{
    internal static NextStep Next(CharacterPlan plan, CompanionState state)
    {
        List<string> blocked = [];
        foreach ((int rank, List<SkillRef> skills) in plan.Ranks.Where(r => r.Key <= state.Rank).OrderBy(r => r.Key))
        {
            foreach (SkillRef skill in skills)
            {
                if (state.Learned(skill))
                    continue;
                if (state.Level(skill.Tree) < skill.Level - 1)
                {
                    blocked.Add($"{Skills.Name(skill)} (rank {rank}) needs {skill.Tree} {skill.Level - 1} first");
                    continue;
                }
                if (state.SkillPoints < Skills.Cost(skill.Level))
                    return new NextStep(null, skill, rank, $"Waiting for {Skills.Cost(skill.Level)} SP for {Skills.Name(skill)} (rank {rank}); has {state.SkillPoints}.");
                return new NextStep(skill, null, rank, $"Learn {Skills.Name(skill)} (rank {rank}).");
            }
        }
        string status = blocked.Count > 0 ? "Blocked: " + string.Join("; ", blocked) : "Nothing to learn until the next planned rank.";
        return new NextStep(null, null, 0, status);
    }

    /// <summary>
    ///     Per rank: the points left once every planned skill up to that rank is paid for (skills already learned
    ///     but not planned count as paid first), and what is wrong with the rank: missing earlier levels,
    ///     skills planned twice, or more points spent than earned by then.
    /// </summary>
    internal static Dictionary<int, RankCheck> Check(CharacterPlan plan, CompanionState state)
    {
        HashSet<SkillRef> planned = plan.Ranks.Values.SelectMany(s => s).ToHashSet();
        int spent = Skills.All().Where(s => state.Learned(s) && !planned.Contains(s)).Sum(s => Skills.Cost(s.Level));

        int[] reached = Skills.Trees.Select(t => state.Level(t)).ToArray();
        HashSet<SkillRef> seen = [];
        Dictionary<int, RankCheck> result = [];
        for (int rank = 1; rank <= Skills.MaxRank; rank++)
        {
            List<string> problems = [];
            foreach (SkillRef skill in plan.Ranks.GetValueOrDefault(rank) ?? [])
            {
                if (!seen.Add(skill))
                {
                    problems.Add($"{Skills.Name(skill)} is planned twice");
                    continue;
                }
                spent += Skills.Cost(skill.Level);
                if (reached[(int)skill.Tree] < skill.Level - 1)
                    problems.Add($"{Skills.Name(skill)} needs {skill.Tree} {skill.Level - 1} at this rank or before");
                reached[(int)skill.Tree] = System.Math.Max(reached[(int)skill.Tree], skill.Level);
            }
            int left = Skills.PointsBy(rank) - spent;
            if (left < 0)
                problems.Add($"{-left} SP short by this rank");
            result[rank] = new RankCheck(left, problems);
        }
        return result;
    }
}
