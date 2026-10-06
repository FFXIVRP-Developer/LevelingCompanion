# Leveling Companion

Dalamud plugin that learns your chocobo companion's skills for you, following a plan you set rank by rank.

## The plan

`/levelingcompanion` opens the window, styled after the game's Companion window. The **Skills** tab is the plan, laid
out like the game's Skills screen: Defender, Attacker and Healer side by side, ten skills each from level 1 down, with
their icons, names and descriptions (levels 1, 4, 7 and 10 are actions; the rest are traits). The rank strip on top
(1 to 20) picks which rank the board edits. Under each rank you click the skills to learn once the chocobo reaches it,
as many as you like; the order you click is the order they are learned. Each rank stands on its own, and plans are
kept per character.

The plan is a whole plan from rank 1, built on its own: every skill can be planned whether the chocobo knows it
already or not, and the SP checks count the plan only. A skill the chocobo knows carries a gold mark and the word
**learned**; when learning, the plugin passes it over.

What a skill shows at the chosen rank, and what a click does:

- **rank N** (blue, earlier rank): already planned before this rank. Not selectable again.
- **this rank** (green): planned here. A click removes it, together with the higher skills of its tree planned
  anywhere, which would be left without it.
- **available**: the next skill of its tree by this rank. A click adds it.
- **rank N** (grey, later rank): a click moves it to this rank when its earlier skill is ready by now.
- **locked**: its earlier skill is not planned by this rank. A skill is never learned without the ones
  before it.
- **not enough SP**: the points earned do not cover it at this rank (or would leave a later rank short).

The game's rules behind this (from the game data, BuddySkill, and the game's own rules):

- A tree's skills are learned in order; the level-N skill costs N skill points (SP).
- Reaching rank N grants N SP up to rank 10, then 10, 10, 10, 10, 10, 10, 11, 12, 13 and 14 SP (165 in all, enough
  for every skill of every tree).
- Past rank 10, each rank needs a Thavnairian Onion fed at the stable to raise the cap.

Each rank shows the SP it grants, the SP to spend and the SP left after it; the strip marks ranks with picks (green
dot), ranks reached (gold) and ranks with a problem (red).

## What it does

Ranks are taken in order and skills in the order listed. A skill already learned is passed over (that is how the plan
keeps track of what is done: the game's own count of learned levels). A skill whose earlier level is not learned yet
is passed over too, and the window says why. A skill the SP do not cover yet stops there, so a later rank never spends
the points an earlier one is waiting for.

To learn a skill the plugin opens the Companion window on its Skills tab and clicks the skill the way the window does:
event 0 to the Buddy agent with `[14, tree, undefined]` (tree 0 Defender, 1 Attacker, 2 Healer; recorded in game).
The game offers that tree's next skill, and the plugin presses Yes on "Spend N SP to acquire “skill”?" (Addon 4974)
only when that prompt names the planned skill. No prompt within 3 s: it clicks again. It stops when the chocobo leaves
or combat starts, never starts in combat or while occupied, and gives up after 60 s (it tries again when something
changes, or on **Learn now**). Every skill it learns goes into the **Learned** tab with the rank it was planned for,
the companion's rank at the time and when.

It needs the companion ("My Little Chocobo") and its skills ("My Feisty Little Chocobo"); the window says which one is
missing.

## Behaviour

The **Behaviour** tab switches the chocobo's stance by your own HP: below the percentage you set it orders Healer
stance; at or above it, the stance you pick (Free, Attacker, Defender or Healer; the game's own stance commands,
BuddyAction rows 4 to 7). It is off until you tick **Switch stance by my HP**. It acts only when the stance the game
reports (`CompanionInfo.ActiveCommand`) differs from the one wanted, never while you ride the chocobo, are mounted or
busy, and orders the same stance again at most every 10 seconds.

The tab, first in the window once a skill is planned (Skills comes first while nothing is): an ON/OFF switch; a live
card with the chocobo's stance icon, your HP bar (red below the threshold, a gold marker at it) and whether the
chocobo is in the wanted stance; the rule in two lines with the threshold slider; the four stances as cards (the kept
one framed in gold, the game's description on hover); and the last five changes, also logged.

## Server info bar

An entry in the server info bar appears once you have the companion.

- **Summoned:** a chocobo in the stance's colour (the game's own stance icons, BuddyAction: Free red-gold, Defender
  orange, Attacker dark red, Healer green) and `Rank N`; at rank 20 the icon alone.
- **Not summoned, where a chocobo can be summoned** (field areas, TerritoryIntendedUse 1): Gysahl Greens alone, saying
  a click summons the chocobo; greyed when you have none.
- **Anywhere else** (cities, inns, housing, duties): a sleeping chocobo alone (the company chocobo dimmed, with
  "z Z"), no text and no tooltip; a left click opens the window.

- **Left click:** summons the chocobo with Gysahl Greens when it is not out (a chat error when you have none); opens
  the Leveling Companion window when it is.
- **Right click:** always opens the game's Companion window.
- **Hover:** the plan for the next rank; no tooltip once the chocobo is at rank 20.

The bar only takes text, so the entry keeps room with spaces and the icon is drawn over it, each frame only while the
entry is shown (and not while the game's UI or the bar is hidden). The entry itself is refreshed on the 3-second check
and on zone changes. Hide it like any entry in Dalamud's server info bar settings.

## Dormant unless the chocobo is out

No per-frame work while idle: one check every 3 seconds reads only the summon timer. With the chocobo out, the
companion is read and the plan consulted, and only when rank, SP or learned levels changed since the last answer.
Per-frame work happens only while one skill is being learned, and stops with it. The stance behaviour checks your HP
four times a second in combat and once a second out of it, each check scheduling the next (no per-frame callback),
only while the chocobo is out and the behaviour is on.

## Clearing

**Clear rank N** removes that rank's skills (and the higher skills of those trees planned later, which would be left
without them). **Clear whole plan** (hold Ctrl and click) removes every planned skill. Neither touches what the chocobo
already learned; to unlearn skills in game, use a Reagan Pepper (Grand Company quartermaster or Hunt billmaster),
which refunds every SP.

## Not verified yet

- The automatic click (built from one manual learn of Healer level 2) has not been seen running by itself yet.
- The Skills tab being tab 1 of the Companion window.
- `CompanionInfo.ActiveCommand` holding the current stance as its BuddyAction row (4 to 7): the Behaviour tab shows
  "Chocobo stance"; if it stays "-" while a stance is set, the game keeps it elsewhere, and the 10-second re-order
  limit keeps the plugin from spamming the command.

## Build and load

```
powershell -File build.ps1
```

Output: `LevelingCompanion\bin\Release\LevelingCompanion.dll`. In Dalamud, Settings > Experimental > Dev Plugin
Locations, add that DLL's path, then enable Leveling Companion in the plugin installer.

ECommons is a submodule with one local commit: an alias for `ExcelPage` in `QuestDialogueText.cs`, which no longer
builds since ClientStructs added its own `ExcelPage`.
