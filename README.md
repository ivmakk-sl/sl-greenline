# Greenline

Greenline is a mod for the Steam game *Survival Log*. It replaces the HUD plant list with a pot grid and enables the game's hidden Tend All feature.

Tend All queues pot chores within the character's available Stamina. With Auto-replant and Auto-fertilize enabled, it also plants the same crop again with fertilizer.

Nexus page: https://www.nexusmods.com/survivallog/mods/17

## The pot grid

The expanded plant list groups pots by floor. Each floor starts with its name and has one cell for each pot, including empty pots. A pot keeps its cell while it stays placed. Each row holds up to 6 cells. Additional cells continue on the next row.

- Each cell shows the crop icon, or the pot icon if the pot is empty. A pot that needs the Till chore also shows a Till mark.
- A mature crop has a gold border.
- A crop with problems shows the game's icon for the most urgent problem on a red disc. Pest and Frost can kill the crop, so the cell pulses red. Weeds, Drought, and Low Light stop growth, so the cell has a red border.
- Each cell shows a countdown below its icon. This shows the time until death from Pest or Frost, the end of the harvest window, or crop maturity.
- If growth stops, a red pause mark replaces the countdown. It uses the same two bars as the game's pause icon. The game restores the paused growth time when you resolve the problem.
- Point at a cell to see its pot card next to the pointer. The card shows the pot and crop sizes, crop state, all problems, full times, fertilizer, and the pot's Auto-fertilize setting.
- Point at a pot in the game world to see the same card.
- Click a cell to move the camera to the pot and open its menu. This also works for empty pots.

## Tend All

Greenline enables the game's hidden Tend All feature. It requires planting level 3. The Tend All button appears in the plant list header when at least one chore is pending. The game's Tend ×N quick action also appears in the bottom bar.

Tend All uses the game's chores, chore order, and Stamina costs. It stops when the character's Stamina is too low. Chores include removing pests and weeds, watering, harvesting, tilling, and planting.

To disable Tend All, set `EnablePatrol` to `false` in the config file. Restart the game to apply the change. The pot grid still works. See [CONFIG.md](CONFIG.md).

## Replant and fertilize

The **Auto-replant** checkbox under the grid controls replanting for all pots. When enabled, Tend All tills harvested pots and plants the same crops again, subject to each pot's Auto-replant setting.

Greenline replants without requiring you to use the planting window. It uses the game's planting action, normal Stamina cost, and seeds from the same containers as the planting window.

Each pot also has two settings. They appear in the planting window header and below the timer in the window for a growing crop. Changes apply immediately.

- **Auto-replant** is enabled by default. When disabled, Tend All still harvests and tills that pot but leaves it empty.
- **Auto-fertilize** is disabled by default. When enabled, replanting uses the best fertilizer available in sufficient quantity for the pot: Premium Organic, then Compound, then Basic. If no fertilizer has sufficient quantity, Greenline replants without fertilizer and shows a message.

If Greenline cannot replant, it opens the planting window. A red message above the Plant button explains the reason:

- There are too few seeds.
- The crop does not fit the pot.
- There is not enough light.
- The temperature is too low.
- Greenline does not know the pot's last crop.

The window selects the last crop's seed if that seed appears in the list. You must click Plant to start planting.

When you open the planting window manually, Greenline also selects the last crop's seed if available and if the crop fits. You must still click Plant.

Greenline stores each pot's last crop, Auto-replant setting, and Auto-fertilize setting in the save, using the game's counter table. Restoring a day also restores these values. The game ignores them without Greenline, so you can still load the save after removing the mod.

## Requirements

The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Greenline.dll`

## Uninstall

Delete `Greenline.dll` from the `BepInEx\plugins` folder. On the next game start, the plant list returns to its appearance and behavior without Greenline. Tend All is disabled again.

## Configuration

The config file is `BepInEx\config\com.ivmakk.survivallog.greenline.cfg`. It controls Tend All, the pot card in the game world, background opacity, and debug logging. See [CONFIG.md](CONFIG.md) for the settings, defaults, and when changes apply.

## Troubleshooting

If a game update removes a required part of the plant list or crop windows, Greenline disables the affected feature. The remaining features continue to work.

Check `BepInEx\LogOutput.log` for the `Greenline loaded.` line. Check for warnings or errors from Greenline.

## Build

Greenline is a BepInEx 6 IL2CPP plugin. Building requires the .NET 8 SDK and a game installation with BepInEx. Start the game once after installing BepInEx to generate the IL2CPP interop assemblies. These assemblies come from the game and are not included in this repository.

```
dotnet build src/Greenline.csproj -c Release
```

`Directory.Build.props` sets `GameDir` to the default Steam installation path. For another location, set the `GameDir` environment variable or pass `-p:GameDir=...` to the build command. The output DLL is `src\bin\Release\Greenline.dll`.

Unit tests cover grid layout, badges, hover rules, page JSON, replant decisions, fertilizer selection, reason messages, save key migration, and opacity. The tested code has no game dependencies. It lives in the `*Logic.cs` files, `src/Web/PageJson.cs`, `src/Web/PageIncludes.cs`, and `src/Web/PageStyle.cs`.

```
dotnet test tests/Greenline.Tests
```

The page scripts have separate tests that use Node and jsdom. `src/Web/page.js` combines the files in `src/Web/page/` for the grid and pot card. `src/Web/plantpanel.js` controls the pot settings and reason message.

These tests use `CoreUI1.html`, `PlantPanel.html`, and `PlantingDetails.html` from the installed game. Run them after a game update. Set `SL_GAME_DIR` if the game uses another installation path. The lint command checks the CSS files.

```
cd tests/page
npm ci
npm test
npm run lint
```

## Package

Add `-p:Package=true` to a Release build to create `dist\Greenline-<version>.zip`. The zip contains `BepInEx\plugins\Greenline.dll`, ready to extract into the game folder. A plain build does not create the zip.

```
dotnet build src/Greenline.csproj -c Release -p:Package=true
```

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
