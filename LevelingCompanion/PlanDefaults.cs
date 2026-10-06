using System.Collections.Generic;
using System.Linq;

namespace LevelingCompanion;

/// <summary>
///     The default plan: a character with nothing planned starts from a copy of it, so one plan set once reaches every
///     character and every profile the settings are copied to.
/// </summary>
internal static class PlanDefaults
{
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