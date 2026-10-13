// The Tend All button and the Auto-replant line of the list header.
import { FEATURES, hasPatrol, panelShouldRender, partsMissing, place, store, toggleClass, word } from './core';
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

// The Auto-replant line: the last grid line of the expanded list, with a checkbox for Greenline's own
// Auto-replant option (replantOn of the pot data, kept in the save by the mod). A click posts
// GREENLINE_REPLANT with the new value.
export function applyReplant(doc: Document, popover: Element, line: number): void {
  let el = popover.querySelector<HTMLElement>('.greenline-replant');
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
    popover.appendChild(el);
  }
  place(el, String(line), '1 / -1');
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
