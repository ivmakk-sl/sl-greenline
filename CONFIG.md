# Configuration

## In the game

The options of each pot are in the game, not in the config file:

- The **Auto-replant** checkbox under the pot grid is the global switch of the replant.
- Each pot has its own **Auto-replant** (on by default) and **Auto-fertilize** (off by default). You find them in the header of the planting window and under the timer of the window of a growing crop. A click applies at once, and the values stay in the save.

## In the config file

The mod creates `BepInEx\config\com.ivmakk.survivallog.greenline.cfg` when you first start the game with the mod installed. All entries are in the `[General]` section and accept `true` or `false`.

To change an entry:

1. Quit the game.
2. Edit the value in a text editor.
3. Save the file.
4. Start the game.

The mod reads the file when the game starts, so an edit while the game runs applies only at the next start.

| Config key | Default | What it does |
|---|---|---|
| `EnablePatrol` | `true` | Turns on the game's own Tend All, which the released game keeps switched off. Tend All also needs planting level 3. With `false`, the mod leaves Tend All switched off, and the replant does not run, because Tend All does the replant. The pot grid, the hover card, and the seed selection of the planting window keep working. |
| `WorldHoverCard` | `true` | Shows the pot card next to the pointer when the pointer is on a pot in the game world. With `false`, only a cell of the pot grid shows the card. After an error, the world card turns off by itself until the next game start, with a warning in the log. The rest of the mod keeps working. |
| `Verbose` | `false` | Logs the pot grid, the replant, and the page scripts at Debug level. Leave it `false` during normal play. |

## Debug logging

To include the debug entries of `Verbose` in `BepInEx\LogOutput.log`, also add `Debug` to `LogLevels` under `[Logging.Disk]` in `BepInEx\config\BepInEx.cfg`.
