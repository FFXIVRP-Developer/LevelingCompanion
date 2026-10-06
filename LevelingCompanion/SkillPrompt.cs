using System.Collections.Generic;
using System.Text;
using ECommons;
using ECommons.UIHelpers.AddonMasterImplementations;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using Lumina.Excel.Sheets;

namespace LevelingCompanion;

/// <summary>
///     The game's "Spend N SP to acquire “skill”?" yes/no window (Addon 4974). Recognised by the text pieces of
///     that row in the client language, plus the skill's name and cost, so Yes is only ever pressed for the one
///     planned skill.
/// </summary>
internal sealed class SkillPrompt
{
    private const uint PromptRow = 4974;

    private readonly List<string> pieces = [];

    internal SkillPrompt()
    {
        ReadOnlySeString text = Svc.Data.GetExcelSheet<Addon>().GetRow(PromptRow).Text;
        foreach (ReadOnlySePayload payload in text)
            if (payload.Type == ReadOnlySePayloadType.Text)
                this.pieces.Add(Encoding.UTF8.GetString(payload.Body.Span));
    }

    /// <summary>The text of the yes/no window on screen when it is this prompt (any skill), else null.</summary>
    internal unsafe string? Showing()
    {
        if (!GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addon) || !GenericHelpers.IsAddonReady(addon))
            return null;
        string text = new AddonMaster.SelectYesno(addon).Text ?? "";
        int at = 0;
        foreach (string piece in this.pieces)
        {
            at = text.IndexOf(piece, at, System.StringComparison.Ordinal);
            if (at < 0)
                return null;
            at += piece.Length;
        }
        return text;
    }

    /// <summary>True when the prompt on screen asks for <paramref name="skill" />.</summary>
    internal bool AsksFor(string? text, SkillRef skill) =>
        text != null && text.Contains(Skills.Name(skill)) && text.Contains(Skills.Cost(skill.Level).ToString());

    internal unsafe void Yes()
    {
        if (GenericHelpers.TryGetAddonByName("SelectYesno", out AtkUnitBase* addon))
            new AddonMaster.SelectYesno(addon).Yes();
    }
}
