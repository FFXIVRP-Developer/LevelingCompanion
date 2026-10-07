using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace LevelingCompanion;

/// <summary>
///     The plugin's entry in the server info bar, shown once you have the companion, only where it can be summoned
///     (field areas) or while it is out (<see cref="Shows" />). Its icon: the game's own stance icon (a chocobo in the
///     stance's colour) while summoned; otherwise Gysahl Greens (greyed when you have none), which a left click uses. The bar
///     only takes text, so the entry keeps room with spaces and the icon is drawn over it, each frame only
///     while the entry is shown. Left click: summons the chocobo when it is not out, else opens the plugin
///     window. Right click: always the game's Companion window. Hover: the plan for the next rank, nothing
///     once the chocobo is at the top rank. The entry itself is refreshed by the watcher's slow check and on
///     zone changes.
/// </summary>
internal sealed class ServerBar : IDisposable
{
    private const string Title = "Leveling Companion";

    private const uint GysahlGreens = 4868;

    /// <summary>TerritoryIntendedUse 1: the field areas, where companions can be summoned.</summary>
    private const uint Overworld = 1;

    /// <summary>Room left in the text for the icon.</summary>
    private const string IconRoom = "      ";

    /// <summary>The whole text when there is no label: just the icon's width.</summary>
    private const string IconOnly = "     ";

    private readonly IDtrBarEntry entry;
    private readonly Func<CharacterPlan?> plan;
    private string shown = "";
    private uint icon;
    private bool greyed, drawing;

    internal ServerBar(Func<CharacterPlan?> plan, System.Action open)
    {
        this.plan  = plan;
        this.entry = Svc.DtrBar.Get(Title);
        this.entry.Shown = false;
        this.entry.OnClick = e =>
        {
            if (e.ClickType == MouseClickType.Right)
                OpenCompanionWindow();
            else if (CompanionState.IsSummoned() || !InField())
                open();
            else
                Summon();
        };
        Svc.ClientState.TerritoryChanged += this.OnTerritoryChanged;
    }

    /// <summary>Shows, hides and refreshes the entry; only touches it when something changed.</summary>
    internal void Update()
    {
        CompanionState state = Svc.ClientState.IsLoggedIn ? CompanionState.Read() : default;
        if (!Shows(state.Obtained, state.Summoned, InField()))
        {
            this.Set("", false, "", null);
            return;
        }

        Stance? stance = state.Summoned ? Behaviour.Current() : null;
        (this.icon, this.greyed) = state.Summoned
            ? (stance is { } s ? Behaviour.Icon(s) : CompanyChocoboIcon(), false)
            : (GreensIcon(), !HasGreens());
        // Not summoned: the greens alone. At the top rank there is nothing left to level: the stance icon alone.
        string label = !state.Summoned || state.Rank >= Skills.MaxRank ? "" : $"Rank {state.Rank}";
        string? tooltip = this.Tooltip(state);
        this.Set($"{this.icon}|{this.greyed}|{label}|{tooltip}", true, label, tooltip);
    }

    /// <summary>
    ///     Shown once you have the companion, only where it can be summoned (field areas) or while it is out (user, 2026-10-07:
    ///     "the chocobo icon actually should only show in areas it can be summoned"; it was a sleeping chocobo everywhere else).
    /// </summary>
    internal static bool Shows(bool obtained, bool summoned, bool inField) => obtained && (summoned || inField);

    public void Dispose()
    {
        Svc.ClientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.SetDrawing(false);
        this.entry.Remove();
    }

    private void OnTerritoryChanged(uint territory) => this.Update();

    private void Set(string key, bool show, string label, string? tooltip)
    {
        this.SetDrawing(show);
        if (key == this.shown && this.entry.Shown == show)
            return;
        this.shown = key;
        this.entry.Shown = show;
        if (!show)
            return;
        this.entry.Text    = new SeStringBuilder().AddText(label == "" ? IconOnly : IconRoom + label).Build();
        this.entry.Tooltip = tooltip == null ? null : new SeStringBuilder().AddText(tooltip).Build();
    }

    private void SetDrawing(bool draw)
    {
        if (draw == this.drawing)
            return;
        this.drawing = draw;
        if (draw)
            Svc.PluginInterface.UiBuilder.Draw += this.DrawIcon;
        else
            Svc.PluginInterface.UiBuilder.Draw -= this.DrawIcon;
    }

    /// <summary>The chocobo over the room kept at the start of the entry, as tall as the entry.</summary>
    private unsafe void DrawIcon()
    {
        if (Svc.GameGui.GameUiHidden || !GenericHelpers.TryGetAddonByName("_DTR", out AtkUnitBase* bar) || !bar->IsVisible)
            return;
        IReadOnlyDtrBarEntry? read = this.entry as IReadOnlyDtrBarEntry ?? Svc.DtrBar.Entries.FirstOrDefault(e => e.Title == Title);
        if (read is not { Shown: true, UserHidden: false })
            return;

        (Vector2 min, Vector2 max) = read.ScreenBounds;
        float size = max.Y - min.Y;
        if (size <= 0)
            return;
        Vector2 at = new(min.X + 2, min.Y);
        Vector4 tint = this.greyed ? new Vector4(0.55f, 0.55f, 0.55f, 0.8f) : Vector4.One;
        ImGui.GetForegroundDrawList().AddImageRounded(
            Svc.Texture.GetFromGameIcon(new GameIconLookup(this.icon)).GetWrapOrEmpty().Handle,
            at, at + new Vector2(size, size), Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint), size * 0.2f);
    }

    /// <summary>The plan for the next rank; null once the chocobo is at the top rank.</summary>
    private string? Tooltip(CompanionState state)
    {
        if (state.Rank >= Skills.MaxRank)
            return null;
        int next = state.Rank + 1;
        List<SkillRef> picks = this.plan()?.Ranks.GetValueOrDefault(next) ?? [];
        string body = picks.Count == 0
            ? "Nothing planned."
            : string.Join("\n", picks.Select(p => $"• {Skills.Name(p)} ({p.Tree} {p.Level}, {Skills.Cost(p.Level)} SP)"));
        string summon = state.Summoned ? "" : HasGreens() ? "\n\nClick: summon your chocobo." : "\n\nNo Gysahl Greens to summon your chocobo.";
        return $"Rank {next} plan\n{body}{summon}";
    }

    /// <summary>The company chocobo (Mount 1) icon.</summary>
    private static uint CompanyChocoboIcon() => Svc.Data.GetExcelSheet<Mount>().GetRowOrDefault(1)?.Icon ?? 0u;

    private static uint GreensIcon() => Svc.Data.GetExcelSheet<Item>().GetRowOrDefault(GysahlGreens)?.Icon ?? 0u;

    private static unsafe bool HasGreens() => InventoryManager.Instance()->GetInventoryItemCount(GysahlGreens) > 0;

    private static bool InField() =>
        Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(Svc.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId == Overworld;

    /// <summary>Right click: the game's Companion window.</summary>
    private static unsafe void OpenCompanionWindow() => AgentModule.Instance()->GetAgentByInternalId(AgentId.Buddy)->Show();

    /// <summary>Left click while the chocobo is not out: summon it with Gysahl Greens.</summary>
    private static unsafe void Summon()
    {
        if (!HasGreens())
        {
            Svc.Chat.PrintError("[Leveling Companion] No Gysahl Greens to summon your chocobo.");
            return;
        }
        AgentInventoryContext.Instance()->UseItem(GysahlGreens);
    }
}
