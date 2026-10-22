// The grid of pot cells: the layout, the cell content, the countdowns, and the cell click.
import {
  FEATURES, FERT_ICON, TILL_ICON, hasCountdown, hasCrop, isStalled, isUrgent, noHarvestWindow, partsMissing,
  popoverShouldRender, rowsById, setImg, shortTime, store, toggleClass
} from './core';
import { applyLast } from './tend';
import { sunIcon, thermoIcon } from './icons';
import type { AreaValues, CoreState, PlantRow, Pot } from './types';
import { drawFloorGrid, placeCell, removeFloorGrid } from '../../Shared/floor-grid/web/floorGrid';

// The prefix of the floor grid library: its classes are gl-fg-*, its properties --gl-fg-*.
export const GRID_PREFIX = 'gl';

// Removes everything the grid adds, so the list shows as the game's plain list.
function clearGrid(doc: Document): void {
  const grids = doc.querySelectorAll<HTMLElement>('.greenline-grid');
  for (let g = 0; g < grids.length; g++) grids[g].classList.remove('greenline-grid', 'greenline-zh');
  removeFloorGrid(doc, GRID_PREFIX);
  const ours = doc.querySelectorAll('.greenline-cell, .greenline-last');
  for (let n = 0; n < ours.length; n++) ours[n].remove();
}

const BADGE_ICONS: Record<string, string> = {
  pest: '../../Res/PlantAnomaly/Pest.png',
  frost: '../../Res/PlantAnomaly/Frost.png',
  drought: '../../Res/PlantAnomaly/Drought.png',
  weed: '../../Res/PlantAnomaly/Weed.png',
  lightLow: '../../Res/PlantAnomaly/LightLow.png',
  needFert: FERT_ICON,
  withered: '../../Res/PlantAnomaly/Withered.png'
};

// The countdown text of a pot, from the game row that the game's own frame loop counts down, or ''.
function countdownText(pot: Pot, rows: Record<string, PlantRow>): string {
  if (isStalled(pot)) return '';
  if (!pot || !pot.rowId || pot.state === 'withered') return '';
  const row = rows[String(pot.rowId)];
  if (!row || !hasCountdown(row.status) || noHarvestWindow(row)) return '';
  return shortTime(row.remainGameSeconds, store.data!.lang);
}

// Keeps the countdown line of a cell: written only when the text changes, removed when empty.
function setTime(doc: Document, el: Element, text: string): void {
  let time = el.querySelector('.greenline-time');
  if (!text) {
    if (time) time.remove();
    return;
  }
  if (!time) {
    time = doc.createElement('div');
    time.className = 'greenline-time';
    el.appendChild(time);
  }
  if (time.textContent !== text) time.textContent = text;
}

// The pause mark of a stalled crop: two bars, as the game's pause icon of the speed control.
function setPause(doc: Document, el: Element, on: boolean): void {
  let mark = el.querySelector(':scope > .greenline-pause');
  if (!on) {
    if (mark) mark.remove();
    return;
  }
  if (mark) return;
  mark = doc.createElement('span');
  mark.className = 'greenline-pause';
  mark.appendChild(doc.createElement('i'));
  mark.appendChild(doc.createElement('i'));
  el.appendChild(mark);
}

// The frame step: the countdown under each cell, from the live game rows.
export function updateCountdowns(doc: Document, state: CoreState | undefined): void {
  if (!store.data) return;
  const cells = doc.querySelectorAll('.greenline-cell');
  if (!cells.length) return;
  const rows = rowsById(state);
  const pots = store.data.pots || {};
  for (let i = 0; i < cells.length; i++) {
    const pot = pots[cells[i].getAttribute('data-pot-id')!] || ({} as Pot);
    setTime(doc, cells[i], countdownText(pot, rows));
    setPause(doc, cells[i], isStalled(pot));
  }
}

// The content of a cell: the crop icon, or the pot icon for an empty or Poor pot, the badge, the
// Till mark of a Poor pot, the gold border of a mature crop, the red pulse of Pest and Frost (the
// crop dies), and the red border of a crop that stops growing.
function fillCell(doc: Document, el: Element, pot: Pot): void {
  setImg(doc, el, 'greenline-icon', hasCrop(pot) ? pot.cropIcon : pot.potIcon);
  setImg(doc, el, 'greenline-badge', BADGE_ICONS[pot.badge] || '');
  setImg(doc, el, 'greenline-till', pot.state === 'poor' ? TILL_ICON : '');
  toggleClass(el, 'greenline-mature', pot.state === 'mature');
  toggleClass(el, 'greenline-urgent', isUrgent(pot));
  toggleClass(el, 'greenline-problem', isStalled(pot));
}

// One value of a line: the icon, then the text. Written only where it differs.
function setValue(doc: Document, end: HTMLElement, cls: string, makeIcon: (d: Document) => SVGSVGElement, text: string): void {
  let el = end.querySelector<HTMLElement>(':scope > .' + cls);
  if (!el) {
    el = doc.createElement('span');
    el.className = cls;
    el.appendChild(makeIcon(doc));
    el.appendChild(doc.createElement('b'));
    end.appendChild(el);
  }
  const b = el.querySelector('b')!;
  if (b.textContent !== text) b.textContent = text;
}

// The values of an area in the free place of its line, as the HUD shows them: the sun with the area light, the
// thermometer with the area temperature. No values (a failed read) leave the place empty. The values have no color of
// their own: a pot with a problem keeps the game's badge.
function setAreaValues(doc: Document, end: HTMLElement, values: AreaValues | undefined): void {
  if (!values) {
    while (end.firstChild) end.firstChild.remove();
    return;
  }
  setValue(doc, end, 'greenline-area-light', sunIcon, values.light);
  setValue(doc, end, 'greenline-area-temp', thermoIcon, values.temp);
}

// Posts what a game row posts on a click (UnitySendEvent('PLANT_MATURE_CLICK') from the CoreUI1
// iframe to its parent, the root page, where this script runs). The game finds the pot by the id, not
// in its plant list, so the click id of a pot with no game row (an empty or Poor pot) also works.
function onCellClick(e: Event): void {
  e.stopPropagation();
  const pot = store.data && store.data.pots && store.data.pots[(e.currentTarget as Element).getAttribute('data-pot-id')!];
  if (!pot || !pot.clickId) return;
  window.postMessage({ type: 'PLANT_MATURE_CLICK', data: { instanceId: pot.clickId }, sourcePageId: 'CoreUI1' }, '*');
}

export function applyGrid(doc: Document, state: CoreState | undefined): string {
  const data = store.data;
  if (!data) {
    clearGrid(doc);
    return '';
  }
  // A collapse keeps the popover in the DOM for its leave transition, so the grid stays as it is.
  if (!popoverShouldRender(state)) return '';
  const missing = partsMissing(doc, 'grid');
  if (missing.length) {
    clearGrid(doc);
    return 'grid(' + missing.join(',') + ')';
  }

  const popover = doc.querySelector<HTMLElement>(FEATURES.grid[0])!;
  // The Chinese countdown has a smaller size of its own (page.css).
  toggleClass(popover, 'greenline-zh', data.lang === 'zh');
  const grid = data.grid || { cols: 1, rows: 0, lines: [], cells: [] };
  // The floor lines and zone lines at their rows (the floor grid library), and one cell for each pot at its row and
  // column, as GridLogic.Layout gives them.
  const ends = drawFloorGrid(doc, popover, {
    prefix: GRID_PREFIX,
    columns: grid.cols,
    lines: grid.lines.map((l) => ({ key: l.key, kind: l.kind, row: l.row, name: l.name })),
  });
  // The area light and the area temperature of the area of each line in its free place at the right edge.
  for (const l of grid.lines) {
    const end = ends.get(l.key);
    if (end) setAreaValues(doc, end, (data.areas || {})[String(l.area)]);
  }
  const keep: Record<string, boolean> = {};
  for (const c of grid.cells) {
    const potId = String(c.pot);
    let el = popover.querySelector<HTMLElement>('.greenline-cell[data-pot-id="' + potId + '"]');
    if (!el) {
      el = doc.createElement('div');
      el.className = 'greenline-cell';
      el.setAttribute('data-pot-id', potId);
      // CoreUI1 sends a click on an element without [data-interactive] to the game world.
      el.setAttribute('data-interactive', '');
      el.addEventListener('click', onCellClick);
      popover.appendChild(el);
    }
    placeCell(el, GRID_PREFIX, c.row, c.col);
    fillCell(doc, el, (data.pots || {})[potId] || ({} as Pot));
    keep[potId] = true;
  }

  const ours = popover.querySelectorAll('.greenline-cell');
  for (let o = 0; o < ours.length; o++) {
    if (!keep[ours[o].getAttribute('data-pot-id')!]) ours[o].remove();
  }

  applyLast(doc, popover, grid.rows + 1);
  updateCountdowns(doc, state);
  if (!popover.classList.contains('greenline-grid')) popover.classList.add('greenline-grid');
  return '';
}
