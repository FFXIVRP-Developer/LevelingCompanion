using System.Collections.Generic;
using System.Linq;

namespace LevelingCompanion;

/// <summary>
///     The default plan: a character with nothing planned starts from a copy of it, so one plan set once reaches every
///     character and every profile the settings are copied to.
/// </summary>
internal static class PlanDefaults
{
    private static SkillRef H(int level) => new(Tree.Healer, level);
    private static SkillRef A(int level) => new(Tree.Attacker, level);
    private static SkillRef D(int level) => new(Tree.Defender, level);

    /// <summary>
    ///     The built-in default plan (user, 2026-10-07: a slave's plan made "the default plan"): Healer 1-8 first, one a rank, then
    ///     Attacker and Defender, Healer 9-10 last; every tree to 10 by rank 20, nothing short. Read only: copied when used.
    /// </summary>
    internal static readonly IReadOnlyDictionary<int, IReadOnlyList<SkillRef>> BuiltInRanks = new Dictionary<int, IReadOnlyList<SkillRef>>
    {
        [1] = [H(1)], [2] = [H(2)], [3] = [H(3)], [4] = [H(4)], [5] = [H(5)], [6] = [H(6)], [7] = [H(7)], [8] = [H(8)],
        [9] = [A(1), A(2), A(3)],
        [10] = [A(4), D(1), D(2), D(3)],
        [11] = [D(4), A(5)],
        [12] = [A(6), A(7)],
        [13] = [A(8)],
        [14] = [A(9)],
        [15] = [A(10)],
        [16] = [D(5), D(6)],
        [17] = [D(7)],
        [18] = [D(8), D(9)],
        [19] = [D(10)],
        [20] = [H(9), H(10)],
    };

    /// <summary>A fresh copy of the built-in default plan.</summary>
    internal static Dictionary<int, List<SkillRef>> BuiltIn => BuiltInRanks.ToDictionary(r => r.Key, r => r.Value.ToList());

    /// <summary>The default plan in use: the one saved in the settings ("Use this plan as the default"), else the built-in one.</summary>
    internal static Dictionary<int, List<SkillRef>> Of(Configuration config) => config.DefaultRanks.Count > 0 ? config.DefaultRanks : BuiltIn;

    /// <summary>
    ///     The default replaces the character's plan (a copy), whenever the user wants ("Apply the default plan"). What the chocobo
    ///     learned stays learned, and so does the history: the planner then catches up with the plan as points come (no reset).
    /// </summary>
    internal static void Apply(CharacterPlan plan, Dictionary<int, List<SkillRef>> defaults) => plan.Ranks = Copy(defaults);

    /// <summary>The character's plan is the default already (the same skills at the same ranks).</summary>
    internal static bool Same(CharacterPlan plan, Dictionary<int, List<SkillRef>> defaults) =>
        plan.Ranks.Count == defaults.Count && defaults.All(r => plan.Ranks.TryGetValue(r.Key, out var mine) && mine.SequenceEqual(r.Value));

    /// <summary>A copy of the ranks (the lists are not shared).</summary>
    internal static Dictionary<int, List<SkillRef>> Copy(Dictionary<int, List<SkillRef>> ranks) =>
        ranks.ToDictionary(r => r.Key, r => r.Value.ToList());

    /// <summary>
    ///     Fills <paramref name="plan" /> from the default when nothing is planned in it yet; a plan of its own is never
    ///     touched. True when it was filled (the settings are saved then).
    /// </summary>
    internal static bool FillFromDefault(CharacterPlan plan, Dictionary<int, List<SkillRef>> defaults)
    {
        if (plan.Ranks.Count > 0 || defaults.Count == 0)
            return false;
        plan.Ranks = Copy(defaults);
        return true;
    }

    /// <summary>The plan becomes the default (a copy, so later edits to the plan leave the default as it was).</summary>
    internal static void UseAsDefault(Configuration config, CharacterPlan plan) => config.DefaultRanks = Copy(plan.Ranks);

    /// <summary>The default plan, in a few words, for the window.</summary>
    internal static string Describe(Dictionary<int, List<SkillRef>> defaults) =>
        defaults.Count == 0 ? "no default plan" : $"default plan: {defaults.Values.Sum(s => s.Count)} skill(s) over {defaults.Count} rank(s)";
}