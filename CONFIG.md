# Configuration

## In the game

Use the in-game checkboxes to control replanting and fertilizer:

- **Auto-replant below the pot grid** controls replanting for all pots. It is enabled by default in each save. If you disable it, it stays disabled in that save.
- **Auto-replant for each pot** is enabled by default. Disable it to leave that pot empty after Tend All harvests and tills it.
- **Auto-fertilize for each pot** is disabled by default. Enable it to use fertilizer when Tend All replants that pot.

Replanting requires both the global Auto-replant checkbox and the pot's Auto-replant checkbox to be enabled.

Each pot's checkboxes appear in the planting window header and below the timer in the window for a growing crop. Changes apply immediately. Each pot's settings remain in the save.

## In the config file

Greenline creates `BepInEx\config\com.ivmakk.survivallog.greenline.cfg` when you first start the game with the mod installed. All settings are in the `[General]` section.

To change a setting:

1. Quit the game.
2. Open the config file in a text editor.
3. Edit the setting's value.
4. Save the file.
5. Start the game.

Greenline reads the file when the game starts. Changes made while the game runs apply at the next game start.

`EnablePatrol`, `WorldHoverCard`, `EnableRobotReplant`, and `Verbose` accept `true` or `false`.

The opacity settings accept numbers from `0` to `1`. BepInEx applies the nearest limit for values outside this range. For example, `PanelOpacity = 5` applies as `1`. BepInEx writes the limit to the file only if this changes the current value. The file can therefore retain a number outside the range.

| Config key | Mod default | Effect |
|---|---|---|
| `EnablePatrol` | `true` | Enables Tend All, which requires planting level 3. Set to `false` to remove the Tend All button and its automatic replanting. The pot grid, pot card, last-crop line and seed selection in the planting window, and Same as last crop and the chore order of the Farming GR-3 still work. |
| `WorldHoverCard` | `true` | Shows the pot card above the pot when you point at a pot in the game world. Set to `false` to show the card only when you point at a grid cell. If the world card encounters an error, Greenline disables it until the next game start and logs a warning. The remaining features continue to work. |
| `EnableRobotReplant` | `true` | Enables the Farming GR-3 features: Same as last crop in its Sowing Presets, and the chore order that makes the robot till and plant each empty pot before its next harvest, also after it stops to charge. Only Remove Pests comes first. A pot with no seed for its size in the Sowing Presets, or with too few seeds, waits. Set to `false` for the robot as in the game without Greenline: no Same as last crop, and the game's order, where the robot harvests every mature pot and does every other chore before it tills or plants. A robot set to Same as last crop then plants nothing in that pot size until you pick a seed. If a game update removes the part of the game that one of the two features needs, only that feature turns off and Greenline logs a warning. |
| `PanelOpacity` | `0.9` | Sets the background opacity of the plant list header and pot grid. Use `0` for transparent or `1` for solid. The game value is `0.6`. Icons, text, and borders remain fully visible. |
| `CardOpacity` | `0.96` | Sets the background opacity of the pot card and other Greenline tooltips. Use `0` for transparent or `1` for solid. The game value is `0.96`. Only the background changes. |
| `Verbose` | `false` | Logs details about the pot grid, replanting, and page scripts at Debug level. Leave this disabled during normal play. |

## Debug logging

To include debug entries in `BepInEx\LogOutput.log`:

1. Quit the game.
2. Set `Verbose = true` in Greenline's config file.
3. Open `BepInEx\config\BepInEx.cfg`.
4. Add `Debug` to `LogLevels` under `[Logging.Disk]`.
5. Save both files.
6. Start the game.
