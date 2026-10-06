using FFXIVClientStructs.FFXIV.Client.UI;

namespace LevelingCompanion;

/// <summary>
///     The click on a skill in the Companion window's Skills tab, which makes the game ask
///     "Spend N SP to acquire “skill”?". What the window sends for it is not documented: until it is known
///     (one manual learn with the recorder on shows it), <see cref="Send" /> sends nothing and the player
///     clicks the skill, while the learner still presses Yes for the planned skill only.
/// </summary>
internal static class SkillClick
{
    /// <summary>Clicks <paramref name="skill" /> in the open Skills tab. False when the click is not known yet.</summary>
    internal static unsafe bool Send(AddonBuddy* buddy, SkillRef skill) => false;
}
