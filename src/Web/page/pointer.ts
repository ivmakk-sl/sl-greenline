// The pot card, pinned next to its anchor: beside a grid cell under the pointer, or above the pot under the
// pointer in the game world.
import { cardModel, setCard } from './card';
import { findFrame, frameState, rowsById, setStyle, store, toggleClass } from './core';
import type { CoreState, CoreWindow } from './types';

interface Anchor {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

// The pot of the world card, as the key of the pots of the pot data, or '', and the place of its top on the
// screen as fractions from the top-left corner.
let worldPot = '';
const worldPlace = { x: 0, y: 0 };

// The grid cell under the pointer and its pot, or none.
let cellPot = '';
let cellEl: Element | null = null;

// The space between the anchor and the card, where the arrow sits.
const GAP = 10;

// Places the card right of the anchor with the arrow at the middle of the anchor, on the left side when it
// would pass the right edge of the window, and inside the window at the top and the bottom. The anchor is
// in window pixels; the body of CoreUI1 has a CSS zoom (HUD Scale changes it), which scales the node's
// left and top, so the anchor is divided by it.
export function placeCard(w: Window, node: HTMLElement, anchor: Anchor): void {
  const zoom = (node as HTMLElement & { currentCSSZoom?: number }).currentCSSZoom || 1;
  const width = node.offsetWidth, height = node.offsetHeight;
  const mid = (anchor.top + anchor.bottom) / 2 / zoom;
  let left = anchor.right / zoom + GAP;
  const leftSide = left + width > w.innerWidth / zoom;
  if (leftSide) left = anchor.left / zoom - GAP - width;
  const top = Math.round(Math.min(Math.max(0, mid - height / 2), Math.max(0, w.innerHeight / zoom - height)));
  setStyle(node, 'left', Math.round(Math.max(0, left)) + 'px');
  setStyle(node, 'top', top + 'px');
  setStyle(node, '--gl-arrow-y', Math.round(mid - top) + 'px');
  toggleClass(node, 'greenline-hover-left', leftSide);
  toggleClass(node, 'greenline-hover-above', false);
  toggleClass(node, 'greenline-hover-below', false);
}

// Places the card above a point of the game world, as the game's card of a trap: its middle over the point
// and the arrow down at it, under the point when it would pass the top of the window, and inside the window
// at the sides (the arrow then moves along the card to stay at the point).
export function placeCardAbove(w: Window, node: HTMLElement, x: number, y: number): void {
  const zoom = (node as HTMLElement & { currentCSSZoom?: number }).currentCSSZoom || 1;
  const width = node.offsetWidth, height = node.offsetHeight;
  const px = x / zoom, py = y / zoom;
  const left = Math.round(Math.min(Math.max(0, px - width / 2), Math.max(0, w.innerWidth / zoom - width)));
  let top = py - GAP - height;
  const below = top < 0;
  if (below) top = py + GAP;
  setStyle(node, 'left', left + 'px');
  setStyle(node, 'top', Math.round(top) + 'px');
  setStyle(node, '--gl-arrow-x', Math.round(Math.min(Math.max(0, px - left), width)) + 'px');
  toggleClass(node, 'greenline-hover-left', false);
  toggleClass(node, 'greenline-hover-above', !below);
  toggleClass(node, 'greenline-hover-below', below);
}

function onEnter(e: Event): void {
  const el = e.target as Element;
  if (!el || !el.classList || !el.classList.contains('greenline-cell')) return;
  cellEl = el;
  cellPot = el.getAttribute('data-pot-id') || '';
  update(el.ownerDocument);
}

function onLeave(e: Event): void {
  if (e.target !== cellEl) return;
  cellEl = null;
  cellPot = '';
  update((e.target as Element).ownerDocument);
}

function update(doc: Document): void {
  try { updateHover(doc, frameState(doc.defaultView as CoreWindow)); } catch (err) { /* reported by the next setHover() */ }
}

// The cell under the pointer: mouseenter and mouseleave do not bubble, so the listeners take them in the
// capture phase. A move on the same cell changes nothing.
export function watchPointer(w: CoreWindow): void {
  if (w.__greenlinePointer) return;
  w.__greenlinePointer = true;
  const doc = w.document;
  doc.addEventListener('mouseenter', onEnter, true);
  doc.addEventListener('mouseleave', onLeave, true);
  // The pointer leaves the frame (a game window is another frame).
  doc.addEventListener('mouseout', (e) => {
    if (e.relatedTarget || !cellEl) return;
    cellEl = null;
    cellPot = '';
    update(doc);
  }, true);
}

// The card: one mod node at the end of the frame body, which takes no pointer, so each click still
// reaches the world. It shows the card of the cell under the pointer, else of the world pot. It moves
// with its cell and its pot, so it has the game's data-no-sync marker: CoreUI1 sends the places of its HUD
// parts to the game after each style change outside such a node.
export function updateHover(doc: Document, state: CoreState | undefined): void {
  let node = doc.querySelector<HTMLElement>('.greenline-hover');
  const pots = store.data && store.data.pots;
  const pot = !pots ? null : cellPot ? pots[cellPot] : worldPot ? pots[worldPot] : null;
  if (!pot) {
    if (node && !node.hidden) node.hidden = true;
    return;
  }
  if (!node) {
    node = doc.createElement('div');
    node.className = 'greenline-hover';
    node.setAttribute('data-no-sync', '');
    doc.body.appendChild(node);
  }
  if (node.hidden) node.hidden = false;
  const rows = rowsById(state);
  setCard(doc, node, cardModel(pot, pot.rowId ? rows[String(pot.rowId)] : null, state));
  const w = doc.defaultView!;
  if (cellPot && cellEl) placeCard(w, node, cellEl.getBoundingClientRect());
  else placeCardAbove(w, node, worldPlace.x * w.innerWidth, worldPlace.y * w.innerHeight);
}

// C# calls this when the pot under the pointer in the game world changes (its PotId, or 0 for none), and
// when its place on the screen changes while its card shows, with the place of its top as fractions of the screen
// from the top-left corner. 0 also closes the card of a cell: C# sends it when the pointer leaves the web UI,
// and the game gives the page no pointer move over the world. `keepCell` is true when the pointer went from a
// world pot onto the web UI: then 0 closes only the world card, and a cell that the pointer entered keeps its
// card. Returns 'ok', 'no CoreUI1 frame', or
// 'error: <text>'; C# reads no result, so an error also goes to C# as a message, which turns the world card
// off.
export function setHover(potId: number, x?: number, y?: number, keepCell?: boolean): string {
  try {
    worldPot = potId ? String(potId) : '';
    if (!potId && !keepCell) {
      cellPot = '';
      cellEl = null;
    }
    if (typeof x === 'number' && typeof y === 'number') {
      worldPlace.x = x;
      worldPlace.y = y;
    }
    const w = findFrame();
    if (!w) return 'no CoreUI1 frame';
    updateHover(w.document, frameState(w));
    return 'ok';
  } catch (e) {
    const text = e && (e as Error).message ? (e as Error).message : String(e);
    const vuplex = (window as Window & { vuplex?: { postMessage(m: string): void } }).vuplex;
    try { if (vuplex) vuplex.postMessage('slmod|greenline|hover error: ' + text); } catch (err) { /* the result still names the error */ }
    return 'error: ' + text;
  }
}
