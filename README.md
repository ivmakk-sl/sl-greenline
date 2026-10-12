# Greenline

Greenline is a mod for the Steam game *Survival Log*. It replaces the HUD plant list with a pot grid and adds Tend All: one click queues the chores of all pots.

Tend All queues pot chores within the character's available Stamina, in the order of the shortest walk. With Auto-replant and Auto-fertilize enabled, it also plants the same crop again with fertilizer. A Farming GR-3 robot can also replant the same crop.

Nexus page: https://www.nexusmods.com/survivallog/mods/17

## The pot grid

The expanded plant list groups pots by floor. Each floor starts with its name and has one cell for each pot, including empty pots. A pot keeps its cell while it stays placed. Each row holds up to 6 cells. Additional cells continue on the next row.

- Each cell shows the crop icon, or the pot icon if the pot is empty. A pot that needs the Till chore also shows a Till mark.
- A mature crop has a gold border.
- A crop with problems shows the game's icon for the most urgent problem on a red disc. Pest and Frost can kill the crop, so the cell pulses red. Weeds, Drought, Low Light, and Needs Fertilizer stop growth, so the cell has a red border. The game has no icon for Needs Fertilizer, so its badge is the Basic Fertilizer icon.
- A research crop (Overwinter No.1) never withers. Its Pest or Frost only stops growth, so its cell has the red border and the pause mark, not the red pulse. A mature research crop has no harvest window, so its cell shows no countdown.
- Each cell shows a countdown below its icon. This shows the time until death from Pest or Frost, the end of the harvest window, or crop maturity.
- If growth stops, a red pause mark replaces the countdown. It uses the same two bars as the game's pause icon. The game restores the paused growth time when you resolve the problem.
- Point at a cell to see its pot card next to the cell, with an arrow that points at the cell. The card shows the pot and crop sizes, crop state, all problems, full times, fertilizer, and the pot's Auto-fertilize setting. For a crop planted with less fertilizer than the pot size, the card shows the dose after the fertilizer name, for example `1/2`.
- Point at a pot in the game world to see the same card above the pot, as the card of a trap shows, with an arrow. The card moves with the pot when the camera moves.
- Click a cell to move the camera to the pot and open its menu. This also works for empty pots.

## Tend All

Tend All requires planting level 3. The Tend All button appears in the plant list header when at least one pot needs a chore. Click it to queue one chore for each pot: Remove Pests, Remove Weeds, Water, Harvest, Clean Up, or Till, and the replant (see below). A pot that already has a chore in the queue gets no second one, and Tend All leaves a pot that a Farming GR-3 is on its way to. The robot in turn skips a pot that your character has queued.

Greenline queues each chore as a click in the pot's menu does, so the Stamina cost, the chore time, and the result stay the same as by hand. If your Stamina does not cover all chores, Tend All queues only the chores that it covers and shows how many. It never starts a pot that it cannot finish: a mature pot whose Harvest, Till, and Plant do not all fit waits for a later Tend All.

The chores follow the shortest walk: first the pots of the floor you are on, then the other floors one by one. On each floor Tend All picks the order with the shortest walk through all pots, measured along the paths your character really walks, around walls and furniture. The kind of chore and the time left on a crop do not change the order. With Stamina for only part of the chores, Tend All goes along this route and queues each pot whose chores fit.

Greenline 1.1 and earlier turned on the game's hidden Tend All. Greenline now does this work itself, so the game's Tend ×N quick action no longer appears in the bottom bar.

To disable Tend All, set `EnablePatrol` to `false` in the config file. Restart the game to apply the change. The pot grid still works. See [CONFIG.md](CONFIG.md).

## Replant and fertilize

The **Auto-replant** checkbox under the grid controls replanting for all pots. It is enabled by default in each save. If you disable it, it stays disabled in that save. When enabled, Tend All tills harvested pots and plants the same crops again, subject to each pot's Auto-replant setting.

Greenline replants without requiring you to use the planting window. It uses the game's planting action, normal Stamina cost, and seeds from the same containers as the planting window. Greenline counts the seeds when the planting starts, not when you click Tend All, so seeds from a harvest earlier in the same Tend All run count.

Each pot also has two settings. They appear in the planting window header and below the timer in the window for a growing crop. Changes apply immediately.

- **Auto-replant** is enabled by default. When disabled, Tend All still harvests and tills that pot but leaves it empty.
- **Auto-fertilize** is disabled by default. When enabled, replanting uses the fertilizer that gives the crop the largest speed bonus. The game plants with a part dose when the containers have less fertilizer than the pot size, and the bonus shrinks with the dose. Greenline compares these part doses too. For example, in a pot of size 2, 1 Premium Organic Fertilizer (+50%) beats 2 Basic Fertilizer (+30%). On a tie, Greenline uses the lower fertilizer and keeps the better one. If the containers have no fertilizer, Greenline replants without fertilizer and shows a message.
- A research crop (Overwinter No.1) takes no fertilizer at planting, so Greenline adds none. Its fertilizer rounds stay your task: Tend All does not give them, and the pot grid shows Needs Fertilizer when a round is missed.

If Greenline cannot replant, it opens the planting window. Next to the PLANTING title, a grey line shows the pot's last crop, and a red line below it gives the reason:

- `seeds 0/1`: too few seeds, with the count you have and the count the pot needs.
- `not enough light`
- `too cold`

If Greenline does not know the pot's last crop, the window opens with no line. The window selects the last crop's seed if that seed appears in the list. You must click Plant to start planting.

When you open the planting window manually, the grey line shows the pot's last crop, and Greenline selects its seed if available. You must still click Plant.

## Farming GR-3

With a Sowing Module, the Farming GR-3 robot replants from its Sowing Presets, one seed for each pot size. Greenline adds **Same as last crop** to the list of each pot size, right after None. With it, the robot plants the last crop of each pot again. It skips a pot when Greenline does not know the pot's last crop, when there are too few seeds, or when the pot's own Auto-replant setting is disabled. The robot's fertilizer preset stays as you set it.

Greenline stores the Auto-replant checkbox under the grid, and each pot's last crop, Auto-replant setting, and Auto-fertilize setting in the save, using the game's counter table. Restoring a day also restores these values. The game ignores them without Greenline, so you can still load the save after removing the mod.

## Requirements

- *Survival Log* version 1.1.18153 or later. For game version 1.0, use Greenline 1.0.1.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Extract this mod's zip into the game folder (the folder with the game .exe). The DLL lands in `BepInEx\plugins`. Full path example:
   - Steam: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Greenline.dll`

## Uninstall

Delete `Greenline.dll` from the `BepInEx\plugins` folder. On the next game start, the plant list returns to its appearance and behavior without Greenline, and Tend All is gone. A Farming GR-3 set to Same as last crop plants nothing in that pot size until you pick a seed in its Sowing Presets.

## Configuration

The config file is `BepInEx\config\com.ivmakk.survivallog.greenline.cfg`. It controls Tend All, the pot card in the game world, background opacity, and debug logging. See [CONFIG.md](CONFIG.md) for the settings, defaults, and when changes apply.

## Troubleshooting

If a game update removes a required part of the plant list or crop windows, Greenline disables the affected feature. The remaining features continue to work.

Check `BepInEx\LogOutput.log` for the `Greenline loaded.` line. Check for warnings or errors from Greenline.

## Build

Greenline is a BepInEx 6 IL2CPP plugin. Building requires the .NET 8 SDK, Node 22, and a game installation with BepInEx. Start the game once after installing BepInEx to generate the IL2CPP interop assemblies. These assemblies come from the game and are not included in this repository. The page scripts use TypeScript in `src/Web/page/` (the grid and the pot card) and `src/Web/plantpanel/` (the pot options of the planting windows). Vite builds each into one file, which the DLL embeds. For [mise](https://mise.jdx.dev) users, `mise.toml` specifies Node 22.

Run these commands from the mod root:

1. If you use mise, run `mise trust` once after cloning the repo.
2. Install the npm packages from the lock file with `npm ci`.
3. Build the mod with `dotnet build src/Greenline.csproj -c Release`.

The build runs Vite when a page source file changes. If the npm packages are missing, the build stops with a message that explains how to install them.

`Directory.Build.props` sets `GameDir` to the default Steam installation path. For another location, set the `GameDir` environment variable or pass `-p:GameDir=...` to the build command. The output DLL is `src\bin\Release\Greenline.dll`.

Unit tests cover grid layout, badges, hover rules, page JSON, the Tend All chores and route, the robot presets, replant decisions, fertilizer selection, reason messages, save key migration, and opacity. The tested code has no game dependencies. It lives in the `*Logic.cs` files, `src/Web/PageJson.cs`, and `src/Web/PageStyle.cs`. The route order and the walk length are a copy of a shared library in `src/Shared/route/`, whose own tests are in the modding workspace.

```
dotnet test tests/Greenline.Tests
```

The page tests use Vitest with jsdom. They run the built scripts against `CoreUI1.html`, `PlantPanel.html`, and `PlantingDetails.html` from the installed game. Run them after a game update. Set `SL_GAME_DIR` if the game uses another installation path. Run the page tests, the CSS lint, and the type check from the mod root:

```
npm test
npm run lint
npm run typecheck
```

Run `npm run dev` to preview the plant list with simulated pots, and the planting window, in a browser. The preview reloads when a page file changes. Use it to check appearance and layout. Check behavior in the game.

## Package

Add `-p:Package=true` to a Release build to create `dist\Greenline-<version>.zip`. The zip contains `BepInEx\plugins\Greenline.dll`, ready to extract into the game folder. A plain build does not create the zip.

```
dotnet build src/Greenline.csproj -c Release -p:Package=true
```

## License

Licensed under the GNU General Public License v3.0. Copyright (C) 2026 ivmakk. See [LICENSE](LICENSE).

You may reuse and modify this mod, but you must keep it open under the same license and give credit. Do not reupload it without credit.
