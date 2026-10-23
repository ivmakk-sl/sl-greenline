# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.0] - 2026-10-23

### Changed

- Tend All replaces the game's own Tend Garden of game 1.1.19611: no watering-can button in the plant list header, no hotkey G, no Tend ×N quick action, and no guide tip. With `EnablePatrol` off, Tend Garden works as without the mod.
- The Tend All button keeps its name, "Tend All" (Chinese 一键巡田): game 1.1.19611 gives the word that the button read before to its own Tend Garden.

### Removed

- Support for game versions before 1.1.19611: the area light of the pot grid reads the light of the weather as a decimal number, as game 1.1.19611 gives it.

## [1.3.0] - 2026-10-22

### Added

- The light and the temperature of each area at the right end of each floor line and zone line of the pot grid, with the sun and the thermometer of the HUD: the values of the game HUD for the area (the weather light times the light of the area; the temperature of the area with its powered heaters and cooking, less the cold of the weather), as the HUD writes them (`0.5`, `+1`, `-3`). They follow the weather within about a second. A lamp or a heat source of one pot is not in them, and they have no color of their own: the pot keeps the game's problem badge.
- The pin of the plant list at the right end of its last line, as in Trapline: a pinned list stays open when you open the trap list or the search, or press Escape, and opens again when the game shows the list again. The arrow of the list header unpins and closes it. The config entry `PinGrid` (default off) keeps it. The last line of the list now shows also without Tend All, to hold the pin.

### Changed

- The pot grid groups the pots of each floor by area (the game's climate areas Interior, Sunroom, Terrace, Cold Storage, and Basement): a floor line with the floor name, the pots of the first area of the floor right under it, and a zone line with the game's area name for each other area. The cells start at the left edge, up to 7 in a row (6 before), so the list is about as wide as Trapline's trap list; the floor name column is gone. The lines use the compact look of Trapline (the shared library floor-grid 2.0.0).

### Fixed

- Stability: no call of Greenline to the web UI asks for a result any more. A result callback is a known cause of a crash of the game's web UI browser; the page now tells Greenline by a page message when it has no page script, lost the grid, or misses a part.

## [1.2.0] - 2026-10-13

### Added

- Greenline's own Tend All: the mod finds the chore of each pot (Remove Pests before Remove Weeds before Water for a growing crop, Harvest, Clean Up, Till; a Harvest with its Till and Plant chore while Auto-replant is on) within the Stamina of the character, from planting level 3, and queues each chore as a click in the menu of the pot does. A pot that already has a chore in the queue gets no second one.
- The Tend All route: the chores follow the shortest walk, the floor of the character first, then the other floors one by one; on each floor the shortest order through all pots (exact up to 12 pots), with walk lengths from the navigation mesh of the game, around walls. The kind of a chore and the time left of a crop do not change the order. With Stamina for only part of the chores, Tend All queues the first chores of the route.
- The last-crop line in the planting window, right of the PLANTING title: the last crop of the pot, and in red the reason when Auto-replant stopped (`seeds 0/1`, `not enough light`, `too cold`).
- Same as last crop in the Sowing Presets of the Farming GR-3, first after None for each pot size: the robot plants the last crop of each pot, and skips a pot with no known last crop, too few seeds, or its own Auto-replant off.
- A Stamina cut never splits the chores of a pot: a pot whose Harvest chain does not fit waits for a later Tend All, and the later pots whose chores fit still go.
- Tend All leaves a pot that a robot has claimed (`PlantChoreManager._claims`); the robot already skips a pot that the character has queued, so the two never work on one pot.
- A `Verbose` line with the seed count when the Plant chore of a replant runs.
- The chore order of a robot: each Till and Plant chore of a robot comes after Remove Pests and before each Harvest, so the robot finishes a harvested pot before its next harvest, also after a charge. Without it, the game sorts each Till and Plant after every other chore of the home, and a robot with fewer than 3 trips left harvests a pot with no Till and Plant. A native detour of `PlantChoreManager.TryResolveChore` changes only the rank of a Till or Plant candidate, with offsets from the il2cpp metadata; the game's seed, environment, and claim checks stay. When the target is missing, only this order turns off, with one warning.
- The config entry `EnableRobotReplant` (on by default) for both robot features, Same as last crop and the chore order: off, Greenline attaches no robot patch, and the robot works as without the mod.

### Changed

- The Auto-replant checkbox under the grid is Greenline's own setting in the save, on in a save with no value. A later off stays off. The game's own Replant value is no longer read.
- The pot card is pinned next to its grid cell, and above its pot in the game world as the card of a trap, each with an arrow.
- Better performance of the world card: it sends once when the pot under the pointer changes, with no callback, and nothing while the pointer moves.
- The page code is TypeScript built by Vite, with Vitest page tests and a dev harness (`npm run dev`); the mod texts use the shared i18n library.

### Removed

- The game's hidden Patrol and its Tend ×N quick action: Greenline no longer turns them on.
- The reason box above the Plant button, replaced by the last-crop line.
- The fit check of a replant: the last crop comes from the same pot, and a pot does not change its size.
- Support for game versions before 1.1.18599: Greenline 1.2.0 needs Survival Log 1.1.18599 or later. On an older game, the pot grid follows a language switch only at its next check, once a second, with one warning.

### Fixed

- A countdown of the pot grid stays inside its cell: a Chinese countdown with days (`1天17时`) uses a smaller text, and from 10 days a countdown shows the days only (`12d`, `12天`).
- The pot grid follows a language switch at once again on game 1.1.18599, which renamed the language switch (`ConfigManager.ApplyLanguageKey`).
- A community language pack of the game gives the mod texts of its language file: `zh.json` for a Chinese pack (for example `zh-TW`), else English, with no warning (i18n library 2.0.0). The page scripts have no English words of their own.

## [1.1.0] - 2026-09-29

### Added

- A part dose for Auto-fertilize: when the containers have less fertilizer than the pot size, the replant plants with what they have, as the game does since version 1.1.
- The Needs Fertilizer problem of a research crop (Overwinter No.1) in the pot grid and on the pot card: the Basic Fertilizer icon as the badge, the pause mark, and a red State row with the game's word.
- The dose of a part dose on the Fertilizer row of the pot card, for example `Premium Organic Fertilizer 1/2`. A long fertilizer name is cut with "…" so the row stays inside the card.

### Changed

- The fertilizer pick of Auto-fertilize: the fertilizer with the largest speed bonus for the pot, a part dose included, and the lower fertilizer on a tie. The pop text shows only when the containers have no fertilizer. A research crop gets no fertilizer at planting.
- A research crop in the pot grid and on the pot card: its Pest or Frost shows as a stop of growth (the red border and the pause mark, no red pulse, no time), and a mature research crop has no countdown and no harvest bar, because it has no harvest window.

### Removed

- Support for game version 1.0. Greenline 1.1.0 needs *Survival Log* 1.1.18153 or later.

## [1.0.1] - 2026-09-27

### Fixed

- A countdown of the pot grid stays inside its cell: a Chinese countdown with days (`1天17时`) uses a smaller text, and from 10 days a countdown shows the days only (`12d`, `12天`).
- Greenline reduces repeated work in the game's web UI during play. It sends its web page script only when the web page needs it, instead of with every pot update. It no longer resends pot data each second just because a crop with no problem gets closer to maturity. The countdowns and the pot card update four times per second instead of every frame. The pot grid and the pot card keep the same appearance and controls.

## [1.0.0] - 2026-09-26

### Added

- The open HUD plant list is a grid of pots: one line for each floor, one cell for each pot, also an empty pot. A cell shows the crop or the pot, a gold border for a mature crop, a red problem icon with a red pulse (Pest, Frost) or a red border (Weeds, Drought, Low Light), and a countdown, or a pause mark while the growth has stopped.
- A hover card on each cell with the pot, the crop, the state, the problems, the times, the fertilizer, and the Auto-fertilize value. A click on a cell goes to the pot and opens its menu.
- The same card next to the pointer when the pointer is on a pot in the game world (config `WorldHoverCard`).
- The game's hidden Tend All is turned on (config `EnablePatrol`, planting level 3), with a "Tend All" button in the plant list header while a chore waits.
- Auto-replant: with the global checkbox under the grid on, Tend All plants the same crop again after a harvest, with no planting window.
- Two options for each pot, in the planting window header and in the window of a growing crop: Auto-replant (on by default) and Auto-fertilize (off by default). Auto-fertilize adds the best fertilizer of which you have enough, or plants with none and shows a pop text.
- When the replant cannot plant, the planting window opens with the last crop's seed selected and a line with the reason. A planting window opened by hand also selects the last crop's seed.
- The last crop and the two options of each pot stay in the save.
- Config `PanelOpacity` and `CardOpacity`: the background opacity of the plant list (its header and the pot grid) and of the pot card. The icons and the text stay fully visible. The default of the plant list is 0.9, more solid than the game's 0.6. The default of the card is the game's 0.96.
