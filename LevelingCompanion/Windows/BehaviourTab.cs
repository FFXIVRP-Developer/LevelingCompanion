using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;

namespace LevelingCompanion.Windows;

/// <summary>The stance behaviour: on/off, the HP percentage for Healer stance, and the stance to keep otherwise.</summary>
internal sealed class BehaviourTab(Configuration config, Behaviour behaviour)
{
    private static readonly Stance[] Stances = [Stance.Free, Stance.Attacker, Stance.Defender, Stance.Healer];

    internal void Draw()
    {
        ImGui.TextColored(Theme.Dim, "While your chocobo is out: your HP below the line → Healer stance; at or above it → the stance you choose.");
        ImGui.Spacing();

        bool enabled = config.BehaviourEnabled;
        if (ImGui.Checkbox("Switch stance by my HP", ref enabled))
        {
            config.BehaviourEnabled = enabled;
            config.Save();
            behaviour.SetRunning(enabled && CompanionState.IsSummoned());
        }
        ImGui.Spacing();

        ImGui.TextColored(Theme.Gold, "Healer stance when my HP is below");
        int percent = config.HealBelowPercent;
        ImGui.SetNextItemWidth(320 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt("##heal", ref percent, 1, 99, "%d%%"))
        {
            config.HealBelowPercent = percent;
            config.Save();
        }
        ImGui.Spacing();

        ImGui.TextColored(Theme.Gold, "Otherwise keep");
        this.DrawStances();
        ImGui.Spacing();
        ImGui.Separator();
        this.DrawStatus();
    }

    private void DrawStances()
    {
        float icon = 48 * ImGuiHelpers.GlobalScale;
        foreach (Stance stance in Stances)
        {
            if (stance != Stance.Free)
                ImGui.SameLine(0, 16);
            ImGui.BeginGroup();
            Vector2 pos = ImGui.GetCursorScreenPos();
            bool selected = config.NormalStance == stance;
            if (ImGui.InvisibleButton($"##stance{stance}", new Vector2(icon + 8, icon + 8)))
            {
                config.NormalStance = stance;
                config.Save();
            }
            bool hovered = ImGui.IsItemHovered();
            ImDrawListPtr draw = ImGui.GetWindowDrawList();
            if (selected || hovered)
                draw.AddRectFilled(pos, pos + new Vector2(icon + 8, icon + 8), Theme.U32(Theme.Alpha(Theme.Gold, selected ? 0.18f : 0.08f)), 6);
            draw.AddImage(Svc.Texture.GetFromGameIcon(new GameIconLookup(Behaviour.Icon(stance))).GetWrapOrEmpty().Handle,
                pos + new Vector2(4, 4), pos + new Vector2(4 + icon, 4 + icon), Vector2.Zero, Vector2.One, Theme.U32(selected ? Vector4.One : new Vector4(0.7f, 0.7f, 0.7f, 0.8f)));
            draw.AddRect(pos + new Vector2(3, 3), pos + new Vector2(5 + icon, 5 + icon), Theme.U32(selected ? Theme.Gold : Theme.Alpha(Theme.Dim, 0.5f)), 5, ImDrawFlags.None, selected ? 2.5f : 1f);
            ImGui.TextColored(selected ? Theme.Gold : Theme.Dim, Behaviour.Name(stance));
            ImGui.EndGroup();
        }
    }

    private void DrawStatus()
    {
        float? health = Behaviour.HealthPercent();
        Stance? current = Behaviour.Current();
        ImGui.TextUnformatted($"Your HP: {(health is { } h ? $"{h:0}%" : "-")}");
        ImGui.SameLine(0, 24);
        ImGui.TextUnformatted($"Chocobo stance: {(current is { } c ? Behaviour.Name(c) : "-")}");
        if (health is { } hp)
        {
            ImGui.SameLine(0, 24);
            ImGui.TextColored(Theme.Green, $"Wanted: {Behaviour.Name(behaviour.Wanted(hp))}");
        }

        if (!CompanionState.IsSummoned())
            ImGui.TextColored(Theme.Dim, "Chocobo not summoned: nothing runs.");
        else if (!config.BehaviourEnabled)
            ImGui.TextColored(Theme.Dim, "Off.");
        else
            ImGui.TextColored(behaviour.Running ? Theme.Green : Theme.Dim, behaviour.Running ? "Watching your HP (4 times a second in combat, once a second out of it)." : "Starts within a few seconds.");
        if (behaviour.Last != "")
            ImGui.TextColored(Theme.Dim, $"Last change: {behaviour.Last}");
    }
}
