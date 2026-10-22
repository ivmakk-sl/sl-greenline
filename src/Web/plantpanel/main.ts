// Runs in the root page. Adds the pot's two options, Auto-replant and Auto-fertilize, as checkboxes to the
// two windows of a pot: the planting window (PlantPanel), in its header before the close button, and the
// window of a growing crop (PlantingDetails), under the timer. The planting window also gets the last-crop
// line right of the title of its left column: the last crop of the pot and, in red, why the replant
// stopped. The C# side sends the values with setState()
// and then calls install(). A reopened window is a new iframe, so install() finds the frame again each
// time, and it retries every 300 ms for 3 seconds when the frame does not exist yet. A frame observer puts
// the parts back when Vue renders them away.
// install() returns "installed", "installed; missing: <page> <title>" (only the last-crop line is off),
// "no frame", "missing: <page> <parts>", or "error: <text>", and posts each result with a problem to C# as a page
// message (C# asks for no result).
//
// Vite builds this entry into one IIFE file (obj/page/plantpanel.js). C# sends it with each setState, and
// a second run keeps the first interface and its state.
import { missingParts, store, windowDocs } from './core';
import { attempt } from './install';
import { apply } from './options';
import type { PlantState } from './types';

function setState(state: Partial<PlantState> | null): void {
  const s = state || {};
  store.current = {
    pot: String(s.pot || ''), replant: s.replant !== false, fert: !!s.fert,
    lastCrop: s.lastCrop || null, words: s.words || {}
  };
  const docs = windowDocs();
  for (let i = 0; i < docs.length; i++) if (!missingParts(docs[i][0], docs[i][1]).length) apply(docs[i][0], docs[i][1]);
}

window.__greenlinePlant = window.__greenlinePlant || { install: () => attempt(10), setState };
