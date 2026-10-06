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

    /// <summary>The skill's name in the client language, from BuddySkill and the Action sheet.</summary>
    internal static string Name(SkillRef skill)
    {
        BuddySkill? row = Svc.Data.GetExcelSheet<BuddySkill>().GetRowOrDefault((uint)skill.Level);
        if (row is not { } r)
            return $"{skill.Tree} {skill.Level}";
        uint action = skill.Tree switch
        {
            Tree.Defender => r.Defender.RowId,
            Tree.Attacker => r.Attacker.RowId,
            _             => r.Healer.RowId,
        };
        return Svc.Data.GetExcelSheet<Action>().GetRowOrDefault(action)?.Name.ExtractText() ?? $"{skill.Tree} {skill.Level}";
    }

    /// <summary>Every skill of every tree, tree by tree.</summary>
    internal static IEnumerable<SkillRef> All()
    {
        foreach (Tree tree in Trees)
            for (int level = 1; level <= MaxLevel; level++)
                yield return new SkillRef(tree, level);
    }
}
