using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using ECommons.DalamudServices;

namespace LevelingCompanion;

/// <summary>What one character's chocobo learns at each rank, and the record of what the plugin learned.</summary>
public sealed class CharacterPlan
{
    /// <summary>Shown in the window only; the key in <see cref="Configuration.Characters" /> is the content id.</summary>
    public string Name { get; set; } = "";

    /// <summary>Rank (1..20) -> the skills to learn once that rank is reached, in order. Each rank stands alone.</summary>
    public Dictionary<int, List<SkillRef>> Ranks { get; set; } = [];

    /// <summary>Skills the plugin learned, oldest first.</summary>
    public List<LearnRecord> History { get; set; } = [];
}

/// <summary>One skill the plugin learned: which, for which planned rank, at what companion rank, when.</summary>
public sealed record LearnRecord(SkillRef Skill, int PlannedRank, int CompanionRank, DateTime When);

/// <summary>Saved settings.</summary>
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Learn planned skills by itself. Off: the window still shows the plan and the status.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Switch the chocobo's stance by your HP (Behaviour tab).</summary>
    public bool BehaviourEnabled { get; set; } = false;

    /// <summary>Your HP below this percentage: Healer stance.</summary>
    public int HealBelowPercent { get; set; } = 50;

    /// <summary>The stance at or above <see cref="HealBelowPercent" />.</summary>
    public Stance NormalStance { get; set; } = Stance.Free;

    /// <summary>Content id -> that character's plan.</summary>
    public Dictionary<ulong, CharacterPlan> Characters { get; set; } = [];

    public void Save() => Svc.PluginInterface.SavePluginConfig(this);
}
