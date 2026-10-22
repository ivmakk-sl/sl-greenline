// The pin of the HUD plant list, at the right end of the last line of the open list (the HUD pin library). While the
// list is pinned, it stays open when the game would close it (the trap list, the search, Escape), and it opens again
// after the game rebuilt the web UI. A click on the chevron of a pinned list unpins it, and the game's own click
// handler then closes the list.
import { FEATURES, frameState, panelShouldRender, partsMissing, store } from './core';
import { LAST_END } from './tend';
import type { CoreState, CoreWindow } from './types';
import { drawPin, removePin } from '../../Shared/hud-pin/web/hudPin';

// The prefix of the HUD pin library: its classes are gl-pin*, its variable --gl-pin-color.
export const PIN_PREFIX = 'gl';

// The field of the CoreUI1 state that opens the plant list.
const FIELD = 'maturePlantsExpanded';

type VueWindow = CoreWindow & { Vue?: { watch: Parameters<typeof drawPin>[0]['watch'] } };

function post(on: boolean): void {
  window.postMessage({ type: 'GREENLINE_PIN', data: { on }, sourcePageId: 'CoreUI1' }, '*');
}

// Draws the pin and keeps the list open while it is pinned. Returns the missing parts as 'pin(<part>)', or ''.
export function applyPin(w: VueWindow, state: CoreState): string {
  const pinned = !!(store.data && store.data.pin);
  if (!store.data || !panelShouldRender(state)) return '';
  const doc = w.document;
  const missing = partsMissing(doc, 'pin');
  if (!w.Vue) missing.push('Vue');
  if (missing.length) {
    removePin(doc, PIN_PREFIX, state);
    return 'pin(' + missing.join(',') + ')';
  }
  watchChevron(w);
  // A pinned list that the game shows closed (a new frame, or the list back after no pot) opens; Vue then renders the
  // list, and the next pass draws the pin in its last line.
  const s = state as unknown as Record<string, unknown>;
  if (pinned && !s[FIELD]) s[FIELD] = true;
  const place = doc.querySelector('.mature-popover .' + LAST_END);
  // With no last line yet (the list is still rendering) the pin draws at the next pass.
  if (place) drawPin({ prefix: PIN_PREFIX, place, state, field: FIELD, watch: w.Vue!.watch, pinned, select: post });
  return '';
}

// A click on the header row of a pinned list (its chevron, not the Tend All button) unpins it before the game's own
// handler toggles the list, so that handler closes it. The Tend All button stops its click from bubbling, but this
// listener runs in the capture phase, so it leaves that button out itself.
function watchChevron(w: VueWindow): void {
  if (w.__greenlineChevron) return;
  w.__greenlineChevron = true;
  w.document.addEventListener('click', (e) => {
    const target = e.target as Element | null;
    if (!store.data || !store.data.pin || !target || !target.closest) return;
    if (!target.closest(FEATURES.pin[0]) || target.closest('.greenline-tend-btn')) return;
    store.data.pin = false;
    let state: CoreState;
    try { state = frameState(w); } catch (err) { return; }
    const place = w.document.querySelector('.mature-popover .' + LAST_END);
    if (place && w.Vue) drawPin({ prefix: PIN_PREFIX, place, state, field: FIELD, watch: w.Vue.watch, pinned: false, select: post });
    else removePin(w.document, PIN_PREFIX, state);
    post(false);
  }, true);
}
