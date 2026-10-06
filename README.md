# Leveling Companion

Dalamud plugin that learns your chocobo companion's skills for you, following a plan you set rank by rank.

## The plan

`/levelingcompanion` opens the window. The **Plan** tab lists ranks 1 to 20. Under each rank you add the skills to
learn once the chocobo reaches it, as many as you like, in the order they should be learned. Each rank stands on its
own: changing one rank never changes another. Plans are kept per character.

The game's rules, which the plan is checked against (from the game data, BuddySkill, and the game's own rules):

- Three trees, Defender, Attacker and Healer, ten skills each. A tree's skills are learned in order.
- The level-N skill of a tree costs N skill points (SP).
- Reaching rank N grants N SP up to rank 10, then 10, 10, 10, 10, 10, 10, 11, 12, 13 and 14 SP (165 in all, enough
  for every skill of every tree).
- Past rank 10, each rank needs a Thavnairian Onion fed at the stable to raise the cap.

Each rank shows the SP it grants and the SP left once everything planned up to it is paid for. Problems are shown in
red: a skill whose earlier level is not learned or planned by then, a skill planned twice, or more SP spent than earned
by that rank.

## What it does

Ranks are taken in order and skills in the order listed. A skill already learned is passed over (that is how the plan
keeps track of what is done: the game's own count of learned levels). A skill whose earlier level is not learned yet
is passed over too, and the window says why. A skill the SP do not cover yet stops there, so a later rank never spends
the points an earlier one is waiting for.

To learn a skill the plugin opens the Companion window on its Skills tab, clicks the skill, and presses Yes on the
game's "Spend N SP to acquire “skill”?" (Addon 4974), only when that prompt names the planned skill. It stops when the
chocobo leaves or combat starts, never starts in combat or while occupied, and gives up after 60 s (it tries again when
something changes, or on **Learn now**). Every skill it learns goes into the **Learned** tab with the rank it was
planned for, the companion's rank at the time and when.

It needs the companion ("My Little Chocobo") and its skills ("My Feisty Little Chocobo"); the window says which one is
missing.

## Dormant unless the chocobo is out

No per-frame work while idle: one check every 3 seconds reads only the summon timer. With the chocobo out, the
companion is read and the plan consulted, and only when rank, SP or learned levels changed since the last answer.
Per-frame work happens only while one skill is being learned, and stops with it. The recorder's hooks exist only while
recording.

## Not verified yet

- **The click on a skill.** What the Companion window sends for it is not documented. Until it is known the plugin
  opens the Skills tab and tells you which skill to click, and presses Yes for you. To build the click in: open the
  **Recorder** tab, **Start recording**, learn one skill by hand, **Stop recording**, and pass on the lines (also in the
  Dalamud log as `LevelingCompanion recorder:`).
- The Skills tab being tab 1 of the Companion window, and the order of the learned levels in the game's data
  (`CompanionInfo.Levels`) being Defender, Attacker, Healer: the status line shows the three levels to compare with the
  game's window.

## Build and load

```
powershell -File build.ps1
```

Output: `LevelingCompanion\bin\Release\LevelingCompanion.dll`. In Dalamud, Settings > Experimental > Dev Plugin
Locations, add that DLL's path, then enable Leveling Companion in the plugin installer.

ECommons is a submodule with one local commit: an alias for `ExcelPage` in `QuestDialogueText.cs`, which no longer
builds since ClientStructs added its own `ExcelPage`.
