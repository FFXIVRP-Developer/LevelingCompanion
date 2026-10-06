using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace LevelingCompanion.Windows;

/// <summary>Colours close to the game's own windows: dark brown panels, gold trim and headings.</summary>
internal static class Theme
{
    internal static readonly Vector4 Gold      = new(0.93f, 0.80f, 0.52f, 1);
    internal static readonly Vector4 Text      = new(0.92f, 0.90f, 0.86f, 1);
    internal static readonly Vector4 Dim       = new(0.60f, 0.57f, 0.52f, 1);
    internal static readonly Vector4 Green     = new(0.55f, 0.90f, 0.45f, 1);
    internal static readonly Vector4 Blue      = new(0.50f, 0.72f, 1.00f, 1);
    internal static readonly Vector4 Red       = new(1.00f, 0.45f, 0.40f, 1);
    internal static readonly Vector4 Yellow    = new(1.00f, 0.85f, 0.35f, 1);
    internal static readonly Vector4 Panel     = new(0.16f, 0.14f, 0.12f, 1);
    internal static readonly Vector4 PanelHigh = new(0.27f, 0.23f, 0.17f, 1);
    internal static readonly Vector4 Trim      = new(0.55f, 0.45f, 0.26f, 1);

    private static readonly (ImGuiCol Col, Vector4 Value)[] Colors =
    [
        (ImGuiCol.WindowBg,         new Vector4(0.11f, 0.10f, 0.09f, 0.97f)),
        (ImGuiCol.ChildBg,          new Vector4(0.14f, 0.12f, 0.10f, 1f)),
        (ImGuiCol.PopupBg,          new Vector4(0.12f, 0.11f, 0.10f, 0.98f)),
        (ImGuiCol.Border,           Trim),
        (ImGuiCol.TitleBg,          new Vector4(0.20f, 0.16f, 0.11f, 1f)),
        (ImGuiCol.TitleBgActive,    new Vector4(0.32f, 0.25f, 0.15f, 1f)),
        (ImGuiCol.Text,             Text),
        (ImGuiCol.Button,           new Vector4(0.30f, 0.25f, 0.18f, 1f)),
        (ImGuiCol.ButtonHovered,    new Vector4(0.45f, 0.37f, 0.23f, 1f)),
        (ImGuiCol.ButtonActive,     new Vector4(0.58f, 0.47f, 0.27f, 1f)),
        (ImGuiCol.Tab,              new Vector4(0.22f, 0.18f, 0.13f, 1f)),
        (ImGuiCol.TabHovered,       new Vector4(0.45f, 0.37f, 0.23f, 1f)),
        (ImGuiCol.TabActive,        new Vector4(0.40f, 0.32f, 0.19f, 1f)),
        (ImGuiCol.FrameBg,          new Vector4(0.20f, 0.17f, 0.13f, 1f)),
        (ImGuiCol.CheckMark,        Gold),
        (ImGuiCol.PlotHistogram,    new Vector4(0.85f, 0.68f, 0.30f, 1f)),
        (ImGuiCol.Separator,        Trim),
    ];

    internal static void Push()
    {
        foreach ((ImGuiCol col, Vector4 value) in Colors)
            ImGui.PushStyleColor(col, value);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6);
    }

    internal static void Pop()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(Colors.Length);
    }

    internal static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    internal static Vector4 Alpha(Vector4 color, float alpha) => color with { W = alpha };
}
