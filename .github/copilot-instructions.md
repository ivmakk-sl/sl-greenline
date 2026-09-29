# Copilot code-review instructions

This repo is a BepInEx 6 (IL2CPP) Harmony mod for *Survival Log*. Plugins derive from `BasePlugin` and call the game through Il2CppInterop proxy assemblies. Review with these traps in mind; a general C# review misses most of them.

## IL2CPP Harmony traps

- **Getter/setter patches often never fire.** il2cpp inlines trivial accessors, so a `MethodType.Getter`/`.Setter` patch silently does nothing. Flag a new getter patch used as the only mechanism. The reliable change is mutating the backing field at a load hook.
- **No `is`/`as` across the interop boundary.** Flag `is`, `as`, or a direct cast on a game type. The correct form is `x.TryCast<T>()` then a null check.
- **No `foreach` over game collections.** The interop enumerator lacks the pattern. Expect `GetEnumerator()` / `MoveNext()` / `Current`, or a count plus an indexer.
- **Never read an `Il2CppSystem.ValueTuple<...>` result of a game method,** direct or as a list element. The interop layer reads the fields wrongly and gives garbage with no error. Expect a method that returns a class, a dictionary, or an `Il2CppStructArray`, or the value calculated in the mod.
- **Guard game lookups.** Singletons and config lookups return null often. Flag an unchecked dereference inside a patch.
- **A patch must not break the game.** Each patch body sits in a try/catch that logs the error, so the HUD and the action queue fall back to the game's own behavior.

## Structure and tests

- **Feature folders.** Each feature has its own folder under `src/` (`Grid/`, `Hover/`, `Replant/`, `PotOptions/`, `Web/`) with its patches, its game-facing code, and its game-free `*Logic.cs` file. `Plugin.cs` holds only the config, the patch list, and the per-frame tick. Flag a new patch that is not in the patch list of `Plugin.Load`: each patch class is attached on its own, so a target missing after a game update turns off only its feature.
- **Pure logic is separated and tested.** Logic that does not need the running game (the grid layout, the badges, the hover rules, the replant decision, the fertilizer choice, the save keys, the page JSON, the page script includes, the opacity rule) lives in the `*Logic.cs` files, `src/Web/PageJson.cs`, `src/Web/PageIncludes.cs`, and `src/Web/PageStyle.cs`, with no BepInEx or Il2Cpp reference, unit-tested under `tests/Greenline.Tests`. Flag new pure logic in a game-facing file, and new pure logic with no test.
- **Patches** prefer a postfix, and tie the `Harmony` instance to the plugin GUID. A Prefix returns `false` only in the mod's own case: the mod's own `GREENLINE_POT_OPTION` page message, and a Plant chore that Patrol queues for a pot whose Auto-replant is off.
- **Normal game actions only.** Tend All is the game's own Patrol. The replant plants through the game's own planting action, with the normal Stamina cost and the seeds and fertilizer from the same containers as the planting window. Flag code that gives items, crops, or EXP directly, skips a Stamina cost, or changes a Plant chore that the player started by hand.
- **The fertilizer pick follows the game's part dose.** The game plants with `min(have, pot size)` of the chosen fertilizer and gives its speed bonus times that dose over the pot size. `ReplantLogic.PickFertilizer` picks the largest such bonus, reading each `SpeedRate` from the planting window, and a tie within 0.0001 goes to the lower `SpeedRate`. The pop text shows only when the containers have no fertilizer, and a seed with `NoBaseFert` (a research crop) gets no fertilizer. Flag a hard-coded fertilizer order or speed value, or a pick that needs a full dose.
- **The research crop comes from the game's own facts.** The mod reads `PlantComponent.NeverWithers()` (the `neverWithers` pot data field), `Data_Web_PlantPanel_Seed.NoBaseFert`, the anomaly bit 256 (Needs Fertilizer), and a game row with `harvestTotalSeconds <= 0` (no harvest window). Flag a copy of the research crop's plant id in the mod.
- **The save stays loadable without the mod.** The pot values go in the game's counter table under `greenline.` keys, which the game ignores. Flag a new save field or a write outside those keys.
- **Page scripts.** `src/Web/page.js` joins the files of `src/Web/page/` by its `// @include` lines, and runs in the root page with the grid and the pot card in the `CoreUI1` iframe. `src/Web/plantpanel.js` adds the pot options to the planting windows. The styles are in the `.css` files of `src/Web/`, with the shared values in `tokens.css`. A script writes to the DOM only when a value differs, and it turns off a feature whose page part is missing. `tests/page` runs the scripts against the game's own pages, and `npm run lint` checks the CSS.
- **Light load on the web UI.** A push of the pot data sends only `window.__greenline.setPots(json)`; the full page script goes only when the page answers `'no script'` (a new root page), with one full send in flight at a time. No `install()` after `setPots`: one push is one pass, and a push with no CoreUI1 frame is retried by the next check of `PotGrid`. The pot data has no time to mature for a crop with no problem (`growRemainSeconds` is 0), so it changes only on a real change; `fertDose` (from `PlantFertMissing`) and `neverWithers` change only at a planting; the card reads that time from the game row, and only while the pot data and the row (status 0) both say the crop has no problem. The page updates on a 250 ms `setInterval` step, never a `requestAnimationFrame` loop. Flag a change that sends the script or the data on each check, puts a changing value into the pot data, or adds a per-frame loop.

## Release and config hygiene

- **Verbose ships off.** The `Verbose` config binds with default `false`. Diagnostic tracing goes on `LogDebug` behind it; `LogInfo` stays quiet apart from the load line.
- **The plugin GUID never changes.** It is `com.ivmakk.survivallog.greenline`, the BepInEx identity and the config file name. Flag any edit to it.
- **The version is in two places that must agree:** `<Version>` in the csproj and the `BepInPlugin` attribute.
- **Config docs match the code.** A change to a `Config.Bind` default, name, or effect also changes `CONFIG.md`.
- **No committed build output.** Flag `bin/`, `obj/`, `dist/`, `node_modules/`, or a game DLL in the diff. Game `<Reference>` entries keep `<Private>false</Private>`.
- **Changelog matches the change.** A player-visible change adds an `[Unreleased]` entry to `CHANGELOG.md` in player-facing wording. An internal-only refactor gets none.
