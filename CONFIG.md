# Configuration

Every setting lives in `BepInEx\config\com.ivmakk.survivallog.greenline.cfg`, written the first time you run the game with the mod installed. Edit it with any text editor while the game is closed. Changes apply at the next game start.

## General

| Setting | Default | Values | What it does |
|---|---|---|---|
| `EnablePatrol` | `true` | `true`, `false` | Tend All, from planting level 3. Off: no Tend All button and no replant by Tend All. The pot grid, the pot card, the last-crop line, and the Farming GR-3 features still work. |
| `WorldHoverCard` | `true` | `true`, `false` | The pot card above a pot when you point at it in the game world. Off: the card shows only for a grid cell. After an error it turns itself off until the next game start, with a warning in the log. |
| `EnableRobotReplant` | `true` | `true`, `false` | The Farming GR-3 features: Same as last crop in its Sowing Presets, and the robot tills and plants each empty pot before its next harvest, also after it stops to charge (only Remove Pests comes first). Off: the robot works as in the game without Greenline, and a robot set to Same as last crop plants nothing in that pot size until you pick a seed. |
| `PinGrid` | `false` | `true`, `false` | Keeps the HUD plant list open. The pin at the right end of the last line of the list sets it, so you do not need to edit it. |
| `PanelOpacity` | `0.9` | `0` (see-through) to `1` (solid); a number outside counts as the nearest limit | The background of the plant list header and the pot grid. The game uses `0.6`. Icons and text stay fully visible. |
| `CardOpacity` | `0.96` | `0` (see-through) to `1` (solid); a number outside counts as the nearest limit | The background of the pot card and the other Greenline tooltips. The game uses `0.96`. |
| `Verbose` | `false` | `true`, `false` | Detail lines in the log, for troubleshooting (see Troubleshooting in the README). Keep it off in normal play. |
