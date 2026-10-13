// The two option checkboxes of one window, and the apply pass of the window.
import { PAGES, store, word } from './core';
import { applyLastCrop } from './lastCrop';
import type { PageName } from './types';

// For each option: its key in the state and the message, and the name of its word.
const OPTIONS: [ 'replant' | 'fert', string][] = [['replant', 'autoReplant'], ['fert', 'autoFert']];

function onToggle(e: Event): void {
  const box = e.target as HTMLInputElement;
  const key = (box.parentNode as Element).getAttribute('data-key') as 'replant' | 'fert';
  // A later apply() (the observer rebuilding the group) must show the new value.
  store.current[key] = !!box.checked;
  window.postMessage({ type: 'GREENLINE_POT_OPTION', data: { pot: store.current.pot, key: key, on: !!box.checked }, sourcePageId: 'PlantPanel' }, '*');
}

function optionGroup(doc: Document): Element {
  let group = doc.querySelector('.greenline-options');
  if (group) return group;
  group = doc.createElement('div');
  group.className = 'greenline-options';
  for (let i = 0; i < OPTIONS.length; i++) {
    const row = doc.createElement('label');
    row.className = 'greenline-option';
    row.setAttribute('data-key', OPTIONS[i][0]);
    row.setAttribute('data-interactive', '');
    const box = doc.createElement('input');
    box.type = 'checkbox';
    box.addEventListener('change', onToggle);
    row.appendChild(box);
    row.appendChild(doc.createElement('span'));
    group.appendChild(row);
  }
  return group;
}

// Writes the parts of one window, each only when it differs, so a same setState writes nothing.
export function apply(name: PageName, doc: Document): void {
  if (!doc.getElementById('greenline-plant-style')) {
    const style = doc.createElement('style');
    style.id = 'greenline-plant-style';
    // tokens.css and plantpanel.css, which C# sets as window.__greenlinePlantCss before this script.
    style.textContent = window.__greenlinePlantCss || '';
    doc.head.appendChild(style);
  }

  const current = store.current;
  const group = optionGroup(doc);
  PAGES[name].place(doc, group);
  for (let i = 0; i < OPTIONS.length; i++) {
    const row = group.querySelector('[data-key="' + OPTIONS[i][0] + '"]')!;
    const text = word(OPTIONS[i][1]);
    const label = row.querySelector('span')!;
    if (label.textContent !== text) label.textContent = text;
    const on = !!current[OPTIONS[i][0]];
    const input = row.querySelector('input')!;
    if (input.checked !== on) input.checked = on;
  }

  applyLastCrop(name, doc);
}
