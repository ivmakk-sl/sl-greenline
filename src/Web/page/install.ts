// The style node, the apply pass on the CoreUI1 frame, and install() and setPots() for C#.
import { findFrame, frameState, store } from './core';
import { applyGrid, updateCountdowns } from './grid';
import { updateHover, watchPointer } from './pointer';
import { applyTend, updateReplant } from './tend';
import type { CoreState, CoreWindow, PotsData } from './types';

// The styles are tokens.css and page.css, which C# sets as window.__greenlineCss before this script.
function ensureStyle(doc: Document): void {
  if (doc.getElementById('greenline-style')) return;
  const style = doc.createElement('style');
  style.id = 'greenline-style';
  style.textContent = window.__greenlineCss || '';
  doc.head.appendChild(style);
}

// A change inside a game row (the countdown text) or inside a mod cell (the mod's own countdown and
// content) needs no new pass. A popover built again (an expand) or a row added or removed does.
function needsApply(records: MutationRecord[]): boolean {
  for (let i = 0; i < records.length; i++) {
    let el: Node | null = records[i].target;
    if (el.nodeType !== 1) el = el.parentElement;
    if (el && (el as Element).closest && (el as Element).closest('.mature-item-row, .greenline-cell, .greenline-replant, .greenline-hover')) continue;
    return true;
  }
  return false;
}

// Applies the grid to one CoreUI1 window. Returns the "installed"/"missing:"/"error:" text that
// install() and setPots() hand back to C#.
function apply(w: CoreWindow): string {
  const doc = w.document;
  let state: CoreState;
  try { state = frameState(w); } catch (e) { return 'error: state not reachable (' + e + ')'; }

  ensureStyle(doc);
  watchPointer(w);
  const missing: string[] = [];
  try {
    const m1 = applyGrid(doc, state); if (m1) missing.push(m1);
    const m2 = applyTend(doc, state); if (m2) missing.push(m2);
  } catch (e) { return 'error: ' + e; }

  if (!w.__greenlineObserver) {
    const observer = new w.MutationObserver((records) => {
      if (!needsApply(records)) return;
      try {
        ensureStyle(doc);
        const s = frameState(w);
        applyGrid(doc, s);
        applyTend(doc, s);
      } catch (e) { /* a page update broke a part; the next install() or setPots() call reports it */ }
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
      try { stepFrame(doc, state); } catch (e) { /* reported by the next install() or setPots() */ }
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

// Whether the grid is still on the CoreUI1 frame: a frame that the game built again has no observer.
export function check(): string {
  const w = findFrame();
  if (!w) return 'no CoreUI1 frame';
  return w.__greenlineObserver ? 'ok' : 'not applied';
}

export function setPots(data: PotsData | null): string {
  store.data = data || null;
  const w = findFrame();
  if (!w) return 'no CoreUI1 frame';
  return apply(w);
}
