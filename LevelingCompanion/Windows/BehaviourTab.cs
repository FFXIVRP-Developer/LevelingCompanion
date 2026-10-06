using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;

namespace LevelingCompanion.Windows;

/// <summary>
///     The stance behaviour, laid out as: an on/off switch; a live card (the chocobo's stance, your HP bar with
///     the threshold marked, and what the plugin wants); the rule in two lines with the threshold slider; the
///     four stances to keep otherwise, as cards; the last changes.
/// </summary>
internal sealed class BehaviourTab(Configuration config, Behaviour behaviour)
{
    private static readonly Stance[] Stances = [Stance.Free, Stance.Attacker, Stance.Defender, Stance.Healer];

    private static float Scale => ImGuiHelpers.GlobalScale;

    internal void Draw()
    {
        this.DrawSwitch();
        ImGui.Spacing();
        this.DrawLiveCard();
        ImGui.Spacing();

        // Settings stay editable while off, only dimmed.
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, config.BehaviourEnabled ? 1f : 0.55f);
        this.DrawRule();
        ImGui.Spacing();
        this.DrawStances();
        ImGui.PopStyleVar();

        ImGui.Spacing();
        this.DrawRecent();
    }

    private void DrawSwitch()
    {
        bool enabled = config.BehaviourEnabled;
        ImGui.PushStyleColor(ImGuiCol.Button, enabled ? Theme.Alpha(Theme.Green, 0.35f) : Theme.Panel);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, enabled ? Theme.Alpha(Theme.Green, 0.5f) : Theme.PanelHigh);
        if (ImGui.Button(enabled ? "ON##behaviour" : "OFF##behaviour", new Vector2(64 * Scale, 0)))
        {
            config.BehaviourEnabled = !enabled;
            config.Save();
            behaviour.SetRunning(config.BehaviourEnabled && CompanionState.IsSummoned());
        }
        ImGui.PopStyleColor(2);
        ImGui.SameLine();
        ImGui.TextColored(Theme.Gold, "Switch my chocobo's stance by my HP");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Dim, enabled ? "" : "  (off: the chocobo keeps whatever stance it has)");
    }

    /// <summary>The chocobo's stance now, your HP against the threshold, and the stance the rule wants.</summary>
    private void DrawLiveCard()
    {
        ImGui.BeginChild("##live", new Vector2(0, 92 * Scale), true, ImGuiWindowFlags.NoScrollbar);
        float? health = Behaviour.HealthPercent();
        Stance? current = CompanionState.IsSummoned() ? Behaviour.Current() : null;

        DrawStanceIcon(current, 64 * Scale, current != null);
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(Theme.Gold, current is { } c ? Behaviour.Name(c) : CompanionState.IsSummoned() ? "Stance unknown" : "Chocobo not summoned");
        this.DrawHealthBar(health, ImGui.GetContentRegionAvail().X - 8 * Scale);
        ImGui.TextColored(Theme.Dim, this.StatusLine(health, current));
        ImGui.EndGroup();
        ImGui.EndChild();
    }

    private string StatusLine(float? health, Stance? current)
    {
        if (!CompanionState.IsSummoned())
            return "Nothing runs until your chocobo is out.";
        if (!config.BehaviourEnabled)
            return "Off.";
        if (health is not { } hp)
            return "";
        Stance wanted = behaviour.Wanted(hp);
        string pace = "checking 4×/s in combat, 1×/s out of it";
        return current == wanted ? $"In the right stance · {pace}" : $"Switching to {Behaviour.Name(wanted)} · {pace}";
    }

    /// <summary>Your HP as a bar, red below the threshold, with a gold marker where the threshold sits.</summary>
    private void DrawHealthBar(float? health, float width)
    {
        float height = 18 * Scale;
        Vector2 min = ImGui.GetCursorScreenPos();
        Vector2 max = min + new Vector2(width, height);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, Theme.U32(new Vector4(0.08f, 0.07f, 0.06f, 1)), 4);

        float hp = health ?? 0;
        bool low = health != null && hp < config.HealBelowPercent;
        if (health != null)
            draw.AddRectFilled(min, new Vector2(min.X + width * hp / 100f, max.Y), Theme.U32(low ? new Vector4(0.80f, 0.30f, 0.25f, 1) : new Vector4(0.35f, 0.70f, 0.30f, 1)), 4);

        float x = min.X + width * config.HealBelowPercent / 100f;
        draw.AddLine(new Vector2(x, min.Y - 3), new Vector2(x, max.Y + 3), Theme.U32(Theme.Gold), 2 * Scale);
        draw.AddRect(min, max, Theme.U32(Theme.Alpha(Theme.Trim, 0.8f)), 4);

        string label = health is { } h ? $"HP {h:0}%   ·   Healer below {config.HealBelowPercent}%" : "HP -";
        Vector2 size = ImGui.CalcTextSize(label);
        draw.AddText(min + new Vector2((width - size.X) / 2, (height - size.Y) / 2), Theme.U32(Theme.Text), label);
        ImGui.Dummy(new Vector2(width, height));
    }

    /// <summary>The rule in two lines: below the threshold → Healer; otherwise → the chosen stance.</summary>
    private void DrawRule()
    {
        ImGui.TextColored(Theme.Gold, "Rule");
        ImGui.Separator();
        float icon = 28 * Scale;

        DrawStanceIcon(Stance.Healer, icon, true);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("When my HP drops below");
        ImGui.SameLine();
        int percent = config.HealBelowPercent;
        ImGui.SetNextItemWidth(220 * Scale);
        if (ImGui.SliderInt("##heal", ref percent, 1, 99, "%d%%"))
        {
            config.HealBelowPercent = percent;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(Theme.Green, $"→ {Behaviour.Name(Stance.Healer)}");

        DrawStanceIcon(config.NormalStance, icon, true);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Otherwise");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Gold, $"→ {Behaviour.Name(config.NormalStance)}");
        ImGui.SameLine();
        ImGui.TextColored(Theme.Dim, "(choose below)");
    }

    /// <summary>The four stances as cards; the chosen one framed in gold.</summary>
    private void DrawStances()
    {
        float gap = ImGui.GetStyle().ItemSpacing.X;
        float width = (ImGui.GetContentRegionAvail().X - gap * 3) / 4;
        float icon = 48 * Scale;
        float height = icon + ImGui.GetTextLineHeightWithSpacing() * 3 + 16 * Scale;

        foreach (Stance stance in Stances)
        {
            if (stance != Stance.Free)
                ImGui.SameLine();
            bool selected = config.NormalStance == stance;
            Vector2 pos = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton($"##stance{stance}", new Vector2(width, height)))
            {
                config.NormalStance = stance;
                config.Save();
            }
            bool hovered = ImGui.IsItemHovered();
            if (hovered && Behaviour.Description(stance) is { Length: > 0 } description)
                ImGui.SetTooltip(description);

            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            draw.AddRectFilled(pos, pos + new Vector2(width, height), Theme.U32(selected ? Theme.PanelHigh : hovered ? Theme.Alpha(Theme.PanelHigh, 0.6f) : Theme.Panel), 6);
            draw.AddRect(pos, pos + new Vector2(width, height), Theme.U32(selected ? Theme.Gold : Theme.Alpha(Theme.Trim, 0.5f)), 6, ImDrawFlags.None, selected ? 2.5f : 1f);

            Vector2 iconAt = pos + new Vector2((width - icon) / 2, 8 * Scale);
            draw.AddImageRounded(IconHandle(stance), iconAt, iconAt + new Vector2(icon, icon), Vector2.Zero, Vector2.One,
                Theme.U32(selected ? Vector4.One : new Vector4(0.75f, 0.75f, 0.75f, 0.85f)), 6);

            string name = Behaviour.Name(stance);
            Vector2 nameSize = ImGui.CalcTextSize(name);
            draw.AddText(new Vector2(pos.X + (width - nameSize.X) / 2, iconAt.Y + icon + 6 * Scale), Theme.U32(selected ? Theme.Gold : Theme.Text), name);
            string note = selected ? "kept above the line" : stance == Stance.Healer ? "always healing" : "click to keep";
            Vector2 noteSize = ImGui.CalcTextSize(note);
            draw.AddText(new Vector2(pos.X + (width - noteSize.X) / 2, iconAt.Y + icon + 6 * Scale + ImGui.GetTextLineHeightWithSpacing()), Theme.U32(Theme.Dim), note);
        }
    }

    private void DrawRecent()
    {
        ImGui.TextColored(Theme.Gold, "Recent changes");
        ImGui.Separator();
        if (behaviour.Recent.Count == 0)
        {
            ImGui.TextColored(Theme.Dim, "None yet.");
            return;
        }
        foreach (string line in behaviour.Recent)
            ImGui.TextColored(Theme.Dim, line);
    }

    private static void DrawStanceIcon(Stance? stance, float size, bool lit)
    {
        uint icon = stance is { } s ? Behaviour.Icon(s) : 0;
        if (icon == 0)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }
        ImGui.Image(Svc.Texture.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty().Handle, new Vector2(size, size),
            Vector2.Zero, Vector2.One, lit ? Vector4.One : new Vector4(0.5f, 0.5f, 0.5f, 0.7f));
    }

    private static ImTextureID IconHandle(Stance stance) =>
        Svc.Texture.GetFromGameIcon(new GameIconLookup(Behaviour.Icon(stance))).GetWrapOrEmpty().Handle;
}
