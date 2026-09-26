# Greenline

A mod for the Steam game *Survival Log* that shows all your pots at a glance and automates their care. The HUD plant list becomes a grid of pots, and one "Tend All" click does the chores of every pot: remove pests and weeds, water, harvest, till, and plant the same crop again, with the fertilizer you choose.

## The pot grid

The open plant list is a compact grid, with one line for each floor. The line starts with the floor name, and each pot of that floor has one cell, also an empty pot. A pot keeps its cell while it stays placed. A line has at most 6 cells, and more cells continue on the next line.

- A cell shows the crop icon, or the pot icon for an empty pot. A pot that needs a Till shows the pot icon with a Till mark.
- A mature crop has a gold border.
- A crop with a problem shows the game's icon for the most urgent problem on a red disc. Pest and Frost can kill the crop, so their cell pulses red. Weeds, Drought, and Low Light only stop the growth, so their cell has a red border.
- Under each cell is a countdown: the time until the crop dies of Pest or Frost, the time until the harvest window ends, or the time until the crop is mature. A crop whose growth has stopped shows a red pause mark in its place, the same two bars as the game's pause icon. The game gives the stopped time back when you clear the problem.
- The hover card of a cell shows the pot and the crop with their sizes, the state, each problem, the full times, the fertilizer, and the Auto-fertilize value of the pot.
- The card shows next to the pointer, for a cell and when the pointer is on a pot in the game world.
- A click on a cell moves the camera to the pot and opens its menu, also for an empty pot.

## Tend All

The game has a "Tend All" feature that queues the chores of all your pots, but the released game keeps it switched off. Greenline turns it on. It needs planting level 3. A "Tend All" button shows in the header of the plant list while at least one chore waits, and the game's own "Tend ×N" quick action shows in the bottom bar.

Tend All is the game's own feature: the chores, their order, their Stamina cost, and the stop when your Stamina runs low are the game's. To keep the game without it, set `EnablePatrol` to false in the config (see [CONFIG.md](CONFIG.md)) and restart the game. The grid works either way.

## Replant and fertilize

The "Auto-replant" checkbox under the grid is the global switch: with it on, Tend All tills a harvested pot and plants the same crop again. Greenline plants it with no planting window, through the game's own planting action, so it takes the normal Stamina and the seeds from the same containers as the planting window.

Each pot also has its own two options. You find them in the header of the planting window and under the timer of the window of a growing crop, and a click applies at once:

- Auto-replant (on by default): with it off, Tend All still harvests and tills that pot, but leaves it empty for you.
- Auto-fertilize (off by default): with it on, the replant adds the best fertilizer of which you have enough for the pot (Premium Organic, then Compound, then Basic). When no fertilizer is enough, the pot is replanted with no fertilizer, and a pop text tells you.

When the replant cannot plant (too few seeds, the crop does not fit the pot, not enough light, too cold, or an unknown last crop), the planting window opens as usual, with the last crop's seed selected and a red line above the Plant button with the reason. A planting window that you open by hand also selects the seed of the pot's last crop. You still click Plant yourself.

The last crop, Auto-replant, and Auto-fertilize of each pot go in the save, in the game's own counter table, so they go back with a day restore of the game. The game ignores these values, so a save still loads when you remove the mod.

## Requirements

The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Greenline.dll`

## Uninstall

Delete `Greenline.dll` from the `BepInEx\plugins` folder. The plant list looks and works as without the mod on the next start, and Tend All is switched off again.

## Configuration

The config file `BepInEx\config\com.ivmakk.survivallog.greenline.cfg` turns Tend All and the world hover card on or off, and turns on debug logging. See [CONFIG.md](CONFIG.md) for the entries, their defaults, and when an edit applies.

## Troubleshooting

If a game update changes the plant list or the plant windows, Greenline turns off only the feature that needs the missing part and keeps the rest working. Look in `BepInEx\LogOutput.log` for the `Greenline loaded.` line and for a warning or an error from Greenline.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies are game-derived and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/Greenline.csproj -c Release
```

`Directory.Build.props` sets `GameDir` to the default Steam install path. If the game is in another place, override it without an edit of the file: set a `GameDir` environment variable, or pass `-p:GameDir=...` on the build. The output DLL is at `src\bin\Release\Greenline.dll`.

The grid layout, the badges, the hover rules, the JSON for the pages, the replant decision, the fertilizer choice, the reason line, and the save key move are game-free code (the `*Logic.cs` files, `src/Web/PageJson.cs`, and `src/Web/PageIncludes.cs`) with unit tests that do not need the game:

```
dotnet test tests/Greenline.Tests
```

The page scripts `src/Web/page.js` (the grid and the pot card, joined from the files of `src/Web/page/`) and `src/Web/plantpanel.js` (the pot option checkboxes and the reason line) have their own tests, which run them against the real `CoreUI1.html`, `PlantPanel.html`, and `PlantingDetails.html` of the installed game (Node with jsdom). Run them after a game update. They need the game install, and `SL_GAME_DIR` overrides the default Steam path. `npm run lint` checks the CSS files:

```
cd tests/page
npm ci
npm test
npm run lint
```

## Package

Add `-p:Package=true` to a Release build to also write the ready-to-install zip at `dist\Greenline-<version>.zip`, laid out as `BepInEx\plugins\Greenline.dll` so a user extracts it at the game root. A plain build skips this step.

```
dotnet build src/Greenline.csproj -c Release -p:Package=true
```

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
