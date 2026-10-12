// The last-crop line of the planting window: right of the title of the left column, a grey label with
// the crop icon and name, and under it, in red, why the Patrol replant stopped. Each part is written only
// when it differs, and a hover tip holds the full text of a cut line.
import { lineTitle, store } from './core';
import type { LastCrop, PageName } from './types';

function lineText(crop: LastCrop): string {
  return crop.label + ' ' + crop.name + (crop.red ? '\n' + crop.red : '');
}

function head(doc: Document): HTMLElement {
  const el = doc.createElement('div');
  el.className = 'greenline-last-crop-head';
  const label = doc.createElement('span');
  label.className = 'greenline-last-crop-label';
  el.appendChild(label);
  el.appendChild(doc.createElement('img'));
  const name = doc.createElement('span');
  name.className = 'greenline-last-crop-name';
  el.appendChild(name);
  return el;
}

function setText(el: Element, text: string): void {
  if (el.textContent !== text) el.textContent = text;
}

export function applyLastCrop(name: PageName, doc: Document): void {
  const title = lineTitle(name, doc);
  let line = doc.querySelector('.greenline-last-crop');
  const crop = store.current.lastCrop;
  if (!title || !crop) {
    if (line) line.remove();
    return;
  }
  if (!line) {
    line = doc.createElement('div');
    line.className = 'greenline-last-crop';
    line.appendChild(head(doc));
  }
  if (line.parentNode !== title || title.lastElementChild !== line) title.appendChild(line);

  const top = line.querySelector('.greenline-last-crop-head')!;
  setText(top.querySelector('.greenline-last-crop-label')!, crop.label);
  const img = top.querySelector('img')!;
  if (crop.icon) {
    if (img.getAttribute('src') !== crop.icon) img.setAttribute('src', crop.icon);
    if (img.hidden) img.hidden = false;
  } else if (!img.hidden) img.hidden = true;
  setText(top.querySelector('.greenline-last-crop-name')!, crop.name);

  let red = line.querySelector('.greenline-last-crop-red');
  if (!crop.red) {
    if (red) red.remove();
  } else {
    if (!red) {
      red = doc.createElement('div');
      red.className = 'greenline-last-crop-red';
      line.appendChild(red);
    }
    setText(red, crop.red);
  }
  const tip = lineText(crop);
  if (line.getAttribute('title') !== tip) line.setAttribute('title', tip);
}
