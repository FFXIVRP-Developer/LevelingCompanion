using System.Collections.Generic;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace LevelingCompanion;

/// <summary>The companion's three skill trees, in the order the game lists them (CompanionInfo.Levels, the BuddySkill columns).</summary>
public enum Tree
{
    Defender = 0,
    Attacker = 1,
    Healer   = 2,
}

/// <summary>One skill: a tree and its level in that tree (1..10).</summary>
public readonly record struct SkillRef(Tree Tree, int Level);

/// <summary>
///     The rules of companion skills, from the game data (BuddySkill) and the game's own rules:
///     a tree's skills are learned in order, the level-N skill costs N skill points, and reaching rank N
///     grants N points up to rank 10, then 10, 10, 10, 10, 10, 10, 11, 12, 13, 14 (165 in all, every tree full).
///     Levels 1, 4, 7 and 10 are actions (Action sheet); the others are traits (Trait sheet).
/// </summary>
internal static class Skills
{
    internal const int MaxLevel = 10;

    internal const int MaxRank = 20;

    internal static readonly Tree[] Trees = [Tree.Defender, Tree.Attacker, Tree.Healer];

    private static readonly int[] PointsAfterTen = [10, 10, 10, 10, 10, 10, 11, 12, 13, 14];

    /// <summary>Skill points the level-<paramref name="level" /> skill of any tree costs.</summary>
    internal static int Cost(int level) => level;

    /// <summary>Skill points granted on reaching <paramref name="rank" />.</summary>
    internal static int PointsAt(int rank) => rank <= 10 ? rank : PointsAfterTen[rank - 11];

    /// <summary>All skill points earned by <paramref name="rank" />.</summary>
    internal static int PointsBy(int rank)
    {
        int sum = 0;
        for (int r = 1; r <= rank; r++)
            sum += PointsAt(r);
        return sum;
    }

    /// <summary>The skill's name in the client language.</summary>
    internal static string Name(SkillRef skill)
    {
        (uint id, bool action) = Row(skill);
        string? name = action
            ? Svc.Data.GetExcelSheet<Action>().GetRowOrDefault(id)?.Name.ExtractText()
            : Svc.Data.GetExcelSheet<Trait>().GetRowOrDefault(id)?.Name.ExtractText();
        return string.IsNullOrEmpty(name) ? $"{skill.Tree} {skill.Level}" : name;
    }

    /// <summary>The skill's icon id.</summary>
    internal static uint Icon(SkillRef skill)
    {
        (uint id, bool action) = Row(skill);
        return action
            ? Svc.Data.GetExcelSheet<Action>().GetRowOrDefault(id)?.Icon ?? 0u
            : (uint)(Svc.Data.GetExcelSheet<Trait>().GetRowOrDefault(id)?.Icon ?? 0);
    }

    /// <summary>The skill's description in the client language.</summary>
    internal static string Description(SkillRef skill)
    {
        (uint id, bool action) = Row(skill);
        return (action
            ? Svc.Data.GetExcelSheet<ActionTransient>().GetRowOrDefault(id)?.Description.ExtractText()
            : Svc.Data.GetExcelSheet<TraitTransient>().GetRowOrDefault(id)?.Description.ExtractText()) ?? "";
    }

    /// <summary>True for the skills the chocobo is ordered to use; false for traits.</summary>
    internal static bool IsAction(SkillRef skill) => Row(skill).Action;

    /// <summary>Every skill of every tree, tree by tree.</summary>
    internal static IEnumerable<SkillRef> All()
    {
        foreach (Tree tree in Trees)
            for (int level = 1; level <= MaxLevel; level++)
                yield return new SkillRef(tree, level);
    }

    private static (uint Id, bool Action) Row(SkillRef skill)
    {
        if (Svc.Data.GetExcelSheet<BuddySkill>().GetRowOrDefault((uint)skill.Level) is not { } r)
            return (0, false);
        uint id = skill.Tree switch
        {
            Tree.Defender => r.Defender.RowId,
            Tree.Attacker => r.Attacker.RowId,
            _             => r.Healer.RowId,
        };
        return (id, r.IsActive);
    }
}
