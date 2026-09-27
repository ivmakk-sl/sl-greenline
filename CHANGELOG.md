# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
