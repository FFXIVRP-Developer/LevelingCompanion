using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace LevelingCompanion;

/// <summary>
///     The click on a skill in the Companion window's Skills tab, which makes the game ask
///     "Spend N SP to acquire “skill”?". Recorded in game (learning Healer level 2 by hand): the window sends
///     the Buddy agent event 0 with [14, tree, undefined], tree being 0 Defender, 1 Attacker, 2 Healer.
///     No level is sent: the game offers the next skill of that tree, so a skill is never learned before the
///     earlier ones.
/// </summary>
internal static class SkillClick
{
    private const int LearnCommand = 14;

    /// <summary>Clicks the next skill of <paramref name="skill" />'s tree. The caller makes sure it is that tree's next level.</summary>
    internal static unsafe bool Send(SkillRef skill)
    {
        AgentInterface* agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy);
        if (agent == null)
            return false;

        AtkValue* values = stackalloc AtkValue[3];
        values[0].Type = AtkValueType.Int;
        values[0].Int  = LearnCommand;
        values[1].Type = AtkValueType.Int;
        values[1].Int  = (int)skill.Tree;
        values[2].Type = AtkValueType.Undefined;
        values[2].Int  = 0;

        AtkValue result;
        agent->ReceiveEvent(&result, values, 3, 0);
        return true;
    }
}
