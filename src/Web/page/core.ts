// The pot data, the words, and the helpers that the features share.
import type { CoreState, CoreWindow, PlantRow, Pot, PotsData } from './types';

// The game selectors that each feature needs. A missing one is reported as a missing part, so a game
// update shows in the log and in the page tests.
export const FEATURES = {
  grid: ['.mature-popover', '.mature-item-row'],
  tend: ['.mature-panel', '.mature-toggle-row', '.mature-toggle-chevron']
};

// The English words, used when the pot data gives no word for a key.
const DEFAULT_WORDS: Record<string, string> = {
  stateGrowing: 'Growing',
  stateMature: 'Mature',
  stateWithered: 'Withered',
  statePoor: 'Poor',
  stateStalled: 'Stalled',
  stateEmpty: 'Empty',
  problemPest: 'Pest',
  problemFrost: 'Frost',
  problemLightLow: 'Low Light',
  problemLightHigh: 'Light Too Strong',
  problemWeed: 'Weed',
  problemDrought: 'Drought',
  problemNeedFert: 'Needs Fertilizer',
  labelState: 'State',
  labelGrowth: 'Growth',
  labelHarvest: 'Harvest',
  labelFertilizer: 'Fertilizer',
  valueNone: 'None',
  autoOn: '(auto: on)',
  autoOff: '(auto: off)',
  valueOn: 'On',
  valueOff: 'Off',
  tendAll: 'Tend All',
  autoReplant: 'Auto-replant',
  autoReplantTip: 'After a harvest, Tend All plants the same crop again, with no planting window.'
};

// The item icon of Basic Fertilizer: the game has no icon for Needs Fertilizer, so the badge and the
// card line of that problem use it.
export const FERT_ICON = '../../Res/Material/UI_Item_Icon_Mat_S_1500710801.png';

// The action icon of Till (action 1618).
export const TILL_ICON = '../../Res/icon/icon_line_tool_18.png';

// The pot data, set by setPots(); null until C# pushes pot data, and then the grid stays off.
export const store: { data: PotsData | null } = { data: null };

export function word(key: string): string {
  const words = store.data && store.data.words;
  if (words && typeof words[key] === 'string' && words[key]) return words[key];
  return DEFAULT_WORDS[key] || '';
}

export function partsMissing(doc: Document, feature: keyof typeof FEATURES): string[] {
  return FEATURES[feature].filter((part) => !doc.querySelector(part));
}

export function panelShouldRender(state: CoreState | undefined): boolean {
  return !!(state && state.maturePlantsActive && state.maturePlants && state.maturePlants.length > 0);
}

// The popover (and its rows) renders only while the game shows the plant list and it is expanded.
export function popoverShouldRender(state: CoreState | undefined): boolean {
  return panelShouldRender(state) && !!(state && state.maturePlantsExpanded);
}

// Writes one inline style property only when its value differs: the body observer and the
// panel's own observers see every write, even of an identical value.
export function setStyle(el: HTMLElement, prop: string, value: string): void {
  if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
}

export function place(el: HTMLElement, row: string, column: string): void {
  setStyle(el, 'grid-row', row);
  setStyle(el, 'grid-column', column);
}

export function hasPatrol(): boolean { return !!(store.data && store.data.patrol); }

function pad2(n: number): string { return n < 10 ? '0' + n : String(n); }

// The countdown under a cell: days and hours from 1 day, hours and minutes under 1 day, and minutes
// under 1 hour.
export function shortTime(seconds: number, lang: string): string {
  const total = Math.max(0, Math.floor(seconds));
  const days = Math.floor(total / 86400), hours = Math.floor(total % 86400 / 3600), mins = Math.floor(total % 3600 / 60);
  const zh = lang === 'zh';
  if (days > 0) return zh ? days + '天' + hours + '时' : days + 'd' + hours + 'h';
  if (hours > 0) return hours + ':' + pad2(mins);
  return zh ? mins + '分' : mins + 'm';
}

// The countdown of the pot card, with no part before the first one that is not zero.
export function fullTime(seconds: number, lang: string): string {
  const total = Math.max(0, Math.floor(seconds));
  const days = Math.floor(total / 86400), hours = Math.floor(total % 86400 / 3600), mins = Math.floor(total % 3600 / 60);
  const zh = lang === 'zh';
  const parts: string[] = [];
  if (days > 0) parts.push(zh ? days + '天' : days + ' d');
  if (days > 0 || hours > 0) parts.push(zh ? hours + '小时' : hours + ' h');
  parts.push(zh ? mins + '分' : mins + ' min');
  return parts.join(zh ? '' : ' ');
}

// The text of a card bar: at most two groups, so no minutes when a day or more is left.
export function barTime(seconds: number, lang: string): string {
  const total = Math.max(0, Math.floor(seconds));
  if (total < 86400) return fullTime(total, lang);
  const days = Math.floor(total / 86400), hours = Math.floor(total % 86400 / 3600);
  return lang === 'zh' ? days + '天' + hours + '小时' : days + ' d ' + hours + ' h';
}

// The game statuses of a row that the game counts down (plantHasCountdown of CoreUI1): growing,
// mature, Pest, and Frost. Weed, Drought, and Low Light stop the growth.
export function hasCountdown(status: number): boolean {
  return status === 0 || status === 1 || status === 3 || status === 4;
}

// A mature crop with no harvest window (a research crop, harvestTotalSeconds 0): the game shows only
// the Harvest word, with no time.
export function noHarvestWindow(row: PlantRow): boolean {
  return row.status === 1 && !(row.harvestTotalSeconds > 0);
}

export function rowsById(state: CoreState | undefined): Record<string, PlantRow> {
  const rows: Record<string, PlantRow> = {};
  const list = (state && state.maturePlants) || [];
  for (let i = 0; i < list.length; i++) rows[String(list[i].showInstanceId)] = list[i];
  return rows;
}

export function toggleClass(el: Element, cls: string, on: boolean): void {
  if (el.classList.contains(cls) !== on) el.classList.toggle(cls, on);
}

// Keeps one <img> of the class in the cell with the source, or none when the source is empty.
export function setImg(doc: Document, el: Element, cls: string, src: string): void {
  let img = el.querySelector('img.' + cls);
  if (!src) {
    if (img) img.remove();
    return;
  }
  if (!img) {
    img = doc.createElement('img');
    img.className = cls;
    el.appendChild(img);
  }
  if (img.getAttribute('src') !== src) img.setAttribute('src', src);
}

export function hasCrop(pot: Pot): boolean {
  return pot.state === 'growing' || pot.state === 'mature' || pot.state === 'withered';
}

// Pest or Frost kills the crop, except a research crop, which never withers.
export function isUrgent(pot: Pot): boolean {
  const problems = pot.problems || [];
  return hasCrop(pot) && pot.state !== 'withered' && !pot.neverWithers
    && (problems.indexOf('pest') >= 0 || problems.indexOf('frost') >= 0);
}

// A growing crop with a problem that only stops its growth (Weed, Drought, Low Light, Needs
// Fertilizer, and Pest or Frost of a research crop): the game keeps no timer for these, so the cell
// shows the pause mark in place of the countdown.
export function isStalled(pot: Pot): boolean {
  return !!pot && pot.state === 'growing' && !!pot.badge && !isUrgent(pot);
}

// The CoreUI1 frame once it has loaded, looked up fresh on every call: a WebView reload gives it a new
// contentWindow, so nothing here caches it across calls.
export function findFrame(): CoreWindow | null {
  const frames = document.querySelectorAll('iframe');
  for (let i = 0; i < frames.length; i++) {
    const w = frames[i].contentWindow as CoreWindow | null;
    if (!w) continue;
    try { if (!/CoreUI1\.html/i.test(String(w.location))) continue; } catch (e) { continue; }
    if (w.document.readyState !== 'complete') continue;
    return w;
  }
  return null;
}

// The page state of the CoreUI1 frame: one const object for the life of the window.
export function frameState(w: CoreWindow): CoreState {
  return w.eval('state') as CoreState;
}
