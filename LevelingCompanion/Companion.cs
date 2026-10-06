using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace LevelingCompanion;

/// <summary>The companion as the game holds it right now (UIState.Buddy.CompanionInfo), read in one go.</summary>
internal readonly record struct CompanionState(
    bool Obtained,
    bool SkillsUnlocked,
    bool Summoned,
    int Rank,
    int SkillPoints,
    int Defender,
    int Attacker,
    int Healer)
{
    /// <summary>"My Little Chocobo", one per Grand Company: the companion itself.</summary>
    private static readonly uint[] ObtainQuests = [66236, 66237, 66238];

    /// <summary>"My Feisty Little Chocobo": ranks and skills.</summary>
    private const uint SkillsQuest = 66698;

    /// <summary>Levels learned in <paramref name="tree" />.</summary>
    internal int Level(Tree tree) => tree switch
    {
        Tree.Defender => this.Defender,
        Tree.Attacker => this.Attacker,
        _             => this.Healer,
    };

    internal bool Learned(SkillRef skill) => this.Level(skill.Tree) >= skill.Level;

    /// <summary>Only the summon timer; the cheap check the dormant watcher makes.</summary>
    internal static unsafe bool IsSummoned()
    {
        UIState* ui = UIState.Instance();
        return ui != null && Present(ref ui->Buddy.CompanionInfo);
    }

    /// <summary>
    ///     The chocobo is out: time left on its summon AND its object in the world. Entering a city sends it away
    ///     but keeps the timer, so the timer alone reads "summoned" in Limsa.
    /// </summary>
    private static unsafe bool Present(ref CompanionInfo info) =>
        info.TimeLeft > 0 && info.Companion != null && info.Companion->EntityId is not (0 or 0xE0000000);

    /// <summary>Name, EXP and summon time left: for the window only, kept out of the state the watcher compares.</summary>
    internal static unsafe (string Name, uint Exp, float TimeLeft) Details()
    {
        UIState* ui = UIState.Instance();
        if (ui == null)
            return ("", 0, 0);
        ref CompanionInfo info = ref ui->Buddy.CompanionInfo;
        return (info.NameString, info.CurrentXP, info.TimeLeft);
    }

    internal static unsafe CompanionState Read()
    {
        UIState* ui = UIState.Instance();
        if (ui == null)
            return default;

        ref CompanionInfo info = ref ui->Buddy.CompanionInfo;
        bool obtained = false;
        foreach (uint quest in ObtainQuests)
            obtained |= QuestManager.IsQuestComplete(quest);

        return new CompanionState(
            obtained,
            QuestManager.IsQuestComplete(SkillsQuest),
            Present(ref info),
            info.Rank,
            info.SkillPoints,
            info.Levels[(int)Tree.Defender],
            info.Levels[(int)Tree.Attacker],
            info.Levels[(int)Tree.Healer]);
    }
}
