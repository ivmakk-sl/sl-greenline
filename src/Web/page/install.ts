// The style node, the apply pass on the CoreUI1 frame, and install() and setPots() for C#.
import { findFrame, frameState, postMod, store } from './core';
import { GRID_PREFIX, applyGrid, updateCountdowns } from './grid';
import { PIN_PREFIX, applyPin } from './pin';
import { updateHover, watchPointer } from './pointer';
import { applyTend, updateReplant } from './tend';
import type { CoreState, CoreWindow, PotsData } from './types';
import { floorGridCss } from '../../Shared/floor-grid/web/floorGrid';
import { hudPinCss } from '../../Shared/hud-pin/web/hudPin';

// The one style node of the mod in the frame: the tokens, the rules of the floor grid library and of the HUD pin
// library, then the page rules. C# sets tokens.css and page.css as window.__greenlineCss before this script.
function ensureStyle(doc: Document): void {
  if (doc.getElementById('greenline-style')) return;
  const css = window.__greenlineCss || { tokens: '', page: '' };
  const style = doc.createElement('style');
  style.id = 'greenline-style';
  style.textContent = css.tokens + '\n' + floorGridCss(GRID_PREFIX) + '\n' + hudPinCss(PIN_PREFIX) + '\n' + css.page;
  doc.head.appendChild(style);
}

// A change inside a game row (the countdown text), inside a mod cell (the mod's own countdown and
// content), inside a floor line or zone line (its name and values), or inside the last line (the
// checkbox and the pin) needs no new pass. A popover built again (an expand) or a row added or
// removed does.
function needsApply(records: MutationRecord[]): boolean {
  for (let i = 0; i < records.length; i++) {
    let el: Node | null = records[i].target;
    if (el.nodeType !== 1) el = el.parentElement;
    if (el && (el as Element).closest && (el as Element).closest('.mature-item-row, .greenline-cell, .greenline-last, .greenline-hover, .' + GRID_PREFIX + '-fg-line')) continue;
    return true;
  }
  return false;
}

// The apply passes since the last Verbose counts: by install() and setPots(), and by the observer.
let applies = 0, observerPasses = 0, countsAt = 0;
const COUNTS_MS = 60000;

// Tells C# by a page message what it must act on: no CoreUI1 frame (a new push), a missing part, or an error (a
// warning). A result with no problem posts nothing.
function report(result: string): void {
  const missing = 'installed; missing: ';
  if (result === 'no CoreUI1 frame') postMod('no frame');
  else if (result.indexOf(missing) === 0) postMod('missing ' + result.slice(missing.length));
  else if (result.indexOf('error: ') === 0) postMod('error ' + result.slice('error: '.length));
}

// Applies the grid to one CoreUI1 window. Returns the "installed"/"missing:"/"error:" text that
// install() and setPots() hand back to C#.
function apply(w: CoreWindow): string {
  const doc = w.document;
  applies++;
  let state: CoreState;
  try { state = frameState(w); } catch (e) { return 'error: state not reachable (' + e + ')'; }

  ensureStyle(doc);
  watchPointer(w);
  const missing: string[] = [];
  try {
    const m1 = applyGrid(doc, state); if (m1) missing.push(m1);
    const m2 = applyTend(doc, state); if (m2) missing.push(m2);
    const m3 = applyPin(w, state); if (m3) missing.push(m3);
  } catch (e) { return 'error: ' + e; }

  if (!w.__greenlineObserver) {
    const observer = new w.MutationObserver((records) => {
      if (!needsApply(records)) return;
      observerPasses++;
      try {
        ensureStyle(doc);
        const s = frameState(w);
        applyGrid(doc, s);
        applyTend(doc, s);
        applyPin(w, s);
      } catch (e) { postMod('error ' + e); }
    });
    // childList only: the game's countdown writes text on each frame, and the mod's own writes of
    // styles and attributes must not start a new pass.
    observer.observe(doc.body, { childList: true, subtree: true });
    w.__greenlineObserver = observer;
  }

  // The page's state is one const object for the life of the window, so the step keeps it. The shown
  // times change at most once each game minute, so 4 steps each second are enough; each part returns
  // at once while nothing of the mod shows.
  if (!w.__greenlineTick) {
    w.__greenlineTick = true;
    w.setInterval(() => {
      try { stepFrame(doc, state); } catch (e) { postMod('error ' + e); }
    }, 250);
  }

  return missing.length ? ('installed; missing: ' + missing.join(', ')) : 'installed';
}

// The countdowns of the cells, the Auto-replant value, and the pot card, from the game state.
function stepFrame(doc: Document, state: CoreState): void {
  updateCountdowns(doc, state);
  updateReplant(doc);
  updateHover(doc, state);
}

// One step on the CoreUI1 frame, for the page tests.
export function step(): void {
  const w = findFrame();
  if (w) stepFrame(w.document, frameState(w));
}

function attempt(retries: number): string {
  const w = findFrame();
  if (!w) {
    if (retries > 0) setTimeout(() => { attempt(retries - 1); }, 300);
    return 'no CoreUI1 frame';
  }
  return apply(w);
}

export function install(): string { return attempt(10); }

// Whether the grid is still on the CoreUI1 frame: a frame that the game built again has no observer. Posts
// "check <result>" when it is not ok, so the next check of C# pushes the pot data again. With Verbose, it also posts
// the counts of the apply passes once each real minute.
export function check(verbose?: boolean): string {
  const w = findFrame();
  const result = !w ? 'no CoreUI1 frame' : (w.__greenlineObserver ? 'ok' : 'not applied');
  if (result !== 'ok') postMod('check ' + result);
  const now = Date.now();
  if (verbose && now - countsAt >= COUNTS_MS) {
    countsAt = now;
    postMod('counts applies ' + applies + ' observer ' + observerPasses);
    applies = 0;
    observerPasses = 0;
  }
  return result;
}

export function setPots(data: PotsData | null): string {
  store.data = data || null;
  const w = findFrame();
  const result = w ? apply(w) : 'no CoreUI1 frame';
  report(result);
  return result;
}
