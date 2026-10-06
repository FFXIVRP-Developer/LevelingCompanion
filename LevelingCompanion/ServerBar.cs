using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace LevelingCompanion;

/// <summary>
///     The plugin's entry in the server info bar, shown wherever a chocobo can be summoned (field areas) or
///     while one is out. Its icon is the stance: Defender = tank, Attacker = DPS, Healer = healer, Free = any
///     class; no icon while not summoned. Left click opens the window, right click summons the chocobo with
///     Gysahl Greens. Hover: the plan for the next rank, nothing once the chocobo is at the top rank.
///     Refreshed by the watcher's slow check and on zone changes, never per frame.
/// </summary>
internal sealed class ServerBar : IDisposable
{
    private const uint GysahlGreens = 4868;

    /// <summary>TerritoryIntendedUse 1: the field areas, where companions can be summoned.</summary>
    private const uint Overworld = 1;

    private readonly IDtrBarEntry entry;
    private readonly Func<CharacterPlan?> plan;
    private string shown = "";

    internal ServerBar(Func<CharacterPlan?> plan, System.Action open)
    {
        this.plan  = plan;
        this.entry = Svc.DtrBar.Get("Leveling Companion");
        this.entry.Shown = false;
        this.entry.OnClick = e =>
        {
            if (e.ClickType == MouseClickType.Right)
                Summon();
            else
                open();
        };
        Svc.ClientState.TerritoryChanged += this.OnTerritoryChanged;
    }

    /// <summary>Shows, hides and refreshes the entry; only touches it when something changed.</summary>
    internal void Update()
    {
        CompanionState state = Svc.ClientState.IsLoggedIn ? CompanionState.Read() : default;
        bool show = state.Obtained && (state.Summoned || InField());
        if (!show)
        {
            this.Set("", null, null, false);
            return;
        }

        Stance? stance = state.Summoned ? Behaviour.Current() : null;
        string label = state.Summoned ? $"Rank {state.Rank}" : "Chocobo";
        string? tooltip = this.Tooltip(state);
        this.Set($"{stance}|{label}|{tooltip}", stance, label, true, tooltip);
    }

    public void Dispose()
    {
        Svc.ClientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.entry.Remove();
    }

    private void OnTerritoryChanged(uint territory) => this.Update();

    private void Set(string key, Stance? stance, string? label, bool show, string? tooltip = null)
    {
        if (key == this.shown && this.entry.Shown == show)
            return;
        this.shown = key;
        this.entry.Shown = show;
        if (!show)
            return;

        SeStringBuilder text = new();
        if (stance is { } s)
            text.AddIcon(Icon(s)).AddText(" ");
        text.AddText(label ?? "");
        this.entry.Text    = text.Build();
        this.entry.Tooltip = tooltip == null ? null : new SeStringBuilder().AddText(tooltip).Build();
    }

    /// <summary>The plan for the next rank; null once the chocobo is at the top rank.</summary>
    private string? Tooltip(CompanionState state)
    {
        if (state.Rank >= Skills.MaxRank)
            return null;
        int next = state.Rank + 1;
        List<SkillRef> picks = this.plan()?.Ranks.GetValueOrDefault(next) ?? [];
        string head = $"Rank {next} plan";
        string body = picks.Count == 0
            ? "Nothing planned."
            : string.Join("\n", picks.Select(p => $"• {Skills.Name(p)} ({p.Tree} {p.Level}, {Skills.Cost(p.Level)} SP)"));
        string summon = state.Summoned ? "" : "\nRight click: summon your chocobo.";
        return $"{head}\n{body}\n\nLeft click: open Leveling Companion.{summon}";
    }

    private static BitmapFontIcon Icon(Stance stance) => stance switch
    {
        Stance.Defender => BitmapFontIcon.Tank,
        Stance.Attacker => BitmapFontIcon.DPS,
        Stance.Healer   => BitmapFontIcon.Healer,
        _               => BitmapFontIcon.AnyClass,
    };

    private static bool InField() =>
        Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(Svc.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId == Overworld;

    private static unsafe void Summon()
    {
        if (CompanionState.IsSummoned())
            return;
        if (InventoryManager.Instance()->GetInventoryItemCount(GysahlGreens) == 0)
        {
            Svc.Chat.PrintError("[Leveling Companion] No Gysahl Greens to summon your chocobo.");
            return;
        }
        AgentInventoryContext.Instance()->UseItem(GysahlGreens);
    }
}
