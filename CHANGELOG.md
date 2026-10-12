# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Greenline's own Tend All: the mod finds the chore of each pot (Remove Pests before Remove Weeds before Water for a growing crop, Harvest, Clean Up, Till; a Harvest with its Till and Plant chore while Auto-replant is on) within the Stamina of the character, from planting level 3, and queues each chore as a click in the menu of the pot does. A pot that already has a chore in the queue gets no second one.
- The Tend All route: the chores follow the shortest walk, the floor of the character first, then the other floors one by one; on each floor the shortest order through all pots (exact up to 12 pots), with walk lengths from the navigation mesh of the game, around walls. The kind of a chore and the time left of a crop do not change the order. With Stamina for only part of the chores, Tend All queues the first chores of the route.
- The last-crop line in the planting window, right of the PLANTING title: the last crop of the pot, and in red the reason when Auto-replant stopped (`seeds 0/1`, `not enough light`, `too cold`).
- Same as last crop in the Sowing Presets of the Farming GR-3, first after None for each pot size: the robot plants the last crop of each pot, and skips a pot with no known last crop, too few seeds, or its own Auto-replant off.
- A Stamina cut never splits the chores of a pot: a pot whose Harvest chain does not fit waits for a later Tend All, and the later pots whose chores fit still go.
- Tend All leaves a pot that a robot has claimed (`PlantChoreManager._claims`); the robot already skips a pot that the character has queued, so the two never work on one pot.
- A `Verbose` line with the seed count when the Plant chore of a replant runs.

### Changed

- The Auto-replant checkbox under the grid is Greenline's own setting in the save, on in a save with no value. A later off stays off. The game's own Replant value is no longer read.
- The pot card is pinned next to its grid cell, and above its pot in the game world as the card of a trap, each with an arrow. The world card sends once when the pot under the pointer changes, with no callback, and nothing while the pointer moves.
- The page code is TypeScript built by Vite, with Vitest page tests and a dev harness (`npm run dev`); the mod texts use the shared i18n library.

### Removed

- The game's hidden Patrol and its Tend ×N quick action: Greenline no longer turns them on.
- The reason box above the Plant button, replaced by the last-crop line.
- The fit check of a replant: the last crop comes from the same pot, and a pot does not change its size.

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
