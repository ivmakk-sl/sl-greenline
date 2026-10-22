// The Tend All button of the list header, and the last line of the list with the Auto-replant checkbox.
import { FEATURES, hasPatrol, panelShouldRender, partsMissing, setStyle, store, toggleClass, word } from './core';
import type { CoreState } from './types';

// The Tend All button of the header, in the style of Trapline's Take All: shown only while Patrol is
// on and a chore waits. A click starts Greenline's own Patrol. While Patrol is on, the game's own header
// button and Replant checkbox are hidden (the greenline-patrol class on the panel), so a game update
// that turns on the game's own Patrol shows no second button.
export function applyTend(doc: Document, state: CoreState | undefined): string {
  const panel = doc.querySelector(FEATURES.tend[0]);
  if (!panelShouldRender(state) || !panel) return '';
  const missing = partsMissing(doc, 'tend');
  let btn = doc.querySelector<HTMLButtonElement>('.greenline-tend-btn');
  if (missing.length) {
    if (btn) btn.remove();
    return 'tend(' + missing.join(',') + ')';
  }
  toggleClass(panel, 'greenline-patrol', hasPatrol());
  if (!hasPatrol() || !(store.data!.pendingChores > 0)) {
    if (btn) btn.remove();
    return '';
  }
  const row = doc.querySelector(FEATURES.tend[1])!;
  const chevron = doc.querySelector(FEATURES.tend[2]);
  if (!btn) {
    btn = doc.createElement('button');
    btn.type = 'button';
    btn.className = 'greenline-tend-btn';
    btn.setAttribute('data-interactive', '');
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      window.postMessage({ type: 'GREENLINE_TEND', data: {}, sourcePageId: 'CoreUI1' }, '*');
    });
  }
  if (btn.parentNode !== row || btn.nextSibling !== chevron) row.insertBefore(btn, chevron);
  const text = word('tendAll');
  if (btn.textContent !== text) btn.textContent = text;
  return '';
}

// The free place at the right end of the last line, which the pin fills.
export const LAST_END = 'greenline-last-end';

// The last line of the expanded list, always under the grid at the given row: the Auto-replant checkbox at its left
// end while Patrol is there, and a free place at its right end for the pin. The checkbox shows Greenline's own
// Auto-replant option (replantOn of the pot data, kept in the save by the mod); a click posts GREENLINE_REPLANT with
// the new value.
export function applyLast(doc: Document, popover: HTMLElement, row: number): void {
  let last = popover.querySelector<HTMLElement>(':scope > .greenline-last');
  if (!last) {
    last = doc.createElement('div');
    last.className = 'greenline-last';
    const end = doc.createElement('div');
    end.className = LAST_END;
    last.appendChild(end);
    popover.appendChild(last);
  }
  setStyle(last, '--gl-fg-row', String(row));
  setStyle(last, 'grid-column', '1 / -1');

  let el = last.querySelector<HTMLElement>(':scope > .greenline-replant');
  if (!hasPatrol()) {
    if (el) el.remove();
    return;
  }
  if (!el) {
    el = doc.createElement('label');
    el.className = 'greenline-replant has-entry-tip';
    el.setAttribute('data-interactive', '');
    const box = doc.createElement('input');
    box.type = 'checkbox';
    box.addEventListener('click', (e) => { e.stopPropagation(); });
    box.addEventListener('change', (e) => {
      window.postMessage({ type: 'GREENLINE_REPLANT', data: { on: (e.target as HTMLInputElement).checked }, sourcePageId: 'CoreUI1' }, '*');
    });
    el.appendChild(box);
    const text = doc.createElement('span');
    text.className = 'greenline-replant-text';
    el.appendChild(text);
    const tip = doc.createElement('div');
    tip.className = 'entry-tip greenline-tip';
    el.appendChild(tip);
    last.insertBefore(el, last.firstChild);
  }
  const label = el.querySelector('.greenline-replant-text')!;
  if (label.textContent !== word('autoReplant')) label.textContent = word('autoReplant');
  const tipEl = el.querySelector('.entry-tip')!;
  if (tipEl.textContent !== word('autoReplantTip')) tipEl.textContent = word('autoReplantTip');
  updateReplant(doc);
}

// The checkbox follows the option of the pot data. `checked` is a property, so this writes no DOM
// attribute.
export function updateReplant(doc: Document): void {
  const box = doc.querySelector<HTMLInputElement>('.greenline-replant input');
  const on = !!(store.data && store.data.replantOn);
  if (box && box.checked !== on) box.checked = on;
}
