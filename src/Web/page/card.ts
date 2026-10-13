// The pot card: its model from the pot data and the game row, and its one node.
import { FERT_ICON, ROW_STATUS, TILL_ICON, barTime, fullTime, hasCrop, noHarvestWindow, store, toggleClass, word } from './core';
import type { CoreState, PlantRow, Pot } from './types';

const MATURE_ICON = '../../Res/PlantAnomaly/MatureReady.png';

// The action icon of Plant (action 1610), for a growing crop.
const GROWING_ICON = '../../Res/icon/icon_line_nature_18.png';

// For each problem: its word, its icon, and the game status of a row that counts its time down (0 for
// none).
const PROBLEM_LINES: Record<string, [string, string, number]> = {
  pest: ['problemPest', '../../Res/PlantAnomaly/Pest.png', ROW_STATUS.pest],
  frost: ['problemFrost', '../../Res/PlantAnomaly/Frost.png', ROW_STATUS.frost],
  drought: ['problemDrought', '../../Res/PlantAnomaly/Drought.png', 0],
  weed: ['problemWeed', '../../Res/PlantAnomaly/Weed.png', 0],
  lightLow: ['problemLightLow', '../../Res/PlantAnomaly/LightLow.png', 0],
  // No game icon exists for too strong light; the light icon stands for both light problems.
  lightHigh: ['problemLightHigh', '../../Res/PlantAnomaly/LightLow.png', 0],
  needFert: ['problemNeedFert', FERT_ICON, 0]
};

const STATE_LINES: Record<string, [string, string]> = {
  growing: ['stateGrowing', GROWING_ICON],
  mature: ['stateMature', MATURE_ICON],
  withered: ['stateWithered', '../../Res/PlantAnomaly/Withered.png'],
  poor: ['statePoor', TILL_ICON],
  empty: ['stateEmpty', '']
};

// A bar of a card row: fill from 0 to 1.
interface Bar {
  fill: number;
  kind: string;
  text: string;
  pause: boolean;
}

interface RowPart {
  text: string;
  cls: string;
}

interface CardRow {
  key: string;
  icon?: string;
  text?: string;
  cls?: string;
  bar?: Bar;
  parts?: RowPart[];
}

interface CardModel {
  head: { icon: string; name: string; size: number; sub: string; subIcon: string; subSize: number };
  rows: CardRow[];
}

// A bar fill from 0 to 1, in whole percent, so the card changes only when the bar visibly moves.
function fillOf(x: number): number {
  return Math.round(Math.max(0, Math.min(1, x)) * 100) / 100;
}

// A game time word ('Harvest', '收获 ') trimmed, or the fallback.
function gameWord(text: string | undefined, fallback: string): string {
  const w = (text || '').trim();
  return w || fallback;
}

// The pot card: a header with the crop (or the pot) and the pot under it, then key rows.
// The game row gives the live times (the decay of Pest and Frost, the harvest window, and the growth
// time of a crop with no problem); the pot data gives the still growth time of a crop with a problem.
export function cardModel(pot: Pot, row: PlantRow | null | undefined, state: CoreState | undefined): CardModel {
  const lang = store.data!.lang;
  const status = row ? row.status : -1;
  const remain = row ? row.remainGameSeconds : 0;
  const crop = hasCrop(pot);
  const model: CardModel = {
    head: crop
      ? { icon: pot.cropIcon, name: pot.cropName, size: pot.cropSize, sub: pot.potName, subIcon: pot.potIcon, subSize: pot.potSize }
      : { icon: pot.potIcon, name: pot.potName, size: pot.potSize, sub: '', subIcon: '', subSize: 0 },
    rows: []
  };
  const rows = model.rows, keyState = word('labelState');

  const problems = (pot.state === 'growing' || pot.state === 'mature') ? (pot.problems || []) : [];
  let shown = 0;
  for (let i = 0; i < problems.length; i++) {
    const p = PROBLEM_LINES[problems[i]];
    if (!p) continue;
    let text = word(p[0]);
    if (p[2] && p[2] === status) text += '  ' + fullTime(remain, lang);
    rows.push({ key: keyState, icon: p[1], text: text, cls: 'greenline-red' });
    shown++;
  }
  // An empty pot needs no State row: its header already shows the pot, and nothing waits on it.
  if (!shown && pot.state !== 'empty') {
    const st = STATE_LINES[pot.state] || STATE_LINES.empty;
    rows.push({
      key: keyState, icon: st[1] || '', text: word(st[0]),
      cls: pot.state === 'mature' ? 'greenline-gold' : ''
    });
  }

  // The growth time runs from the game row only while the pot data and the row both say the crop has
  // no problem. With a problem in the pot data, the still time of the pot data shows; with no problem
  // in the pot data but a problem in the row (the next push brings it), no bar shows.
  let left = -1;
  if (problems.length) left = pot.growRemainSeconds || 0;
  else if (status === ROW_STATUS.growing) left = remain;
  if (pot.state === 'growing' && pot.growTotalSeconds > 0 && left >= 0) {
    rows.push({
      key: word('labelGrowth'),
      bar: {
        fill: fillOf(1 - left / pot.growTotalSeconds),
        kind: problems.length ? 'greenline-stalled' : '', text: barTime(left, lang), pause: problems.length > 0
      }
    });
  }
  if (pot.state === 'mature' && status === ROW_STATUS.mature && !noHarvestWindow(row!)) {
    rows.push({
      key: gameWord(state && state.i18n_plantHarvest, word('labelHarvest')),
      bar: { fill: fillOf(remain / row!.harvestTotalSeconds), kind: 'greenline-ripe', text: barTime(remain, lang), pause: false }
    });
  }

  // Fertilizer: the name (a grey None with no icon), the dose of a part dose ('1/4'), then the auto
  // value, white when on, grey when off.
  const fertParts: RowPart[] = [{ text: pot.fertName || word('valueNone'), cls: 'greenline-fert-name' + (pot.fertName ? '' : ' greenline-muted') }];
  if (pot.fertName && pot.fertDose) fertParts.push({ text: pot.fertDose, cls: 'greenline-fert-dose' });
  fertParts.push({ text: word(pot.autoFert ? 'autoOn' : 'autoOff'), cls: 'greenline-fert-auto' + (pot.autoFert ? '' : ' greenline-muted') });
  rows.push({ key: word('labelFertilizer'), icon: pot.fertName ? (pot.fertIcon || '') : '', cls: '', parts: fertParts });
  rows.push({
    key: word('autoReplant'), icon: '', text: word(pot.autoReplant === false ? 'valueOff' : 'valueOn'),
    cls: pot.autoReplant === false ? 'greenline-muted' : ''
  });
  return model;
}

function sizeChip(doc: Document, size: number): HTMLElement {
  const chip = doc.createElement('span');
  chip.className = 'greenline-chip';
  const cells = doc.createElement('span');
  cells.className = 'cells s' + size;
  for (let n = 0; n < size; n++) {
    const sq = doc.createElement('span');
    sq.className = 'cell';
    cells.appendChild(sq);
  }
  chip.appendChild(cells);
  return chip;
}

// A header line: the name and its size chip. The pot line under a crop (with an icon) is one pill of
// the pot icon, the name, and the size chip, so the pot reads as one tag.
function namedLine(doc: Document, cls: string, text: string, size: number, icon?: string): HTMLElement {
  const line = doc.createElement('span');
  line.className = cls;
  let box = line;
  if (icon) {
    box = doc.createElement('span');
    box.className = 'greenline-card-pill';
    line.appendChild(box);
    const img = doc.createElement('img');
    img.setAttribute('src', icon);
    box.appendChild(img);
  }
  const t = doc.createElement('span');
  t.className = 'greenline-card-text';
  t.textContent = text || '';
  box.appendChild(t);
  if (size > 0) box.appendChild(sizeChip(doc, size));
  return line;
}

function barEl(doc: Document, bar: Bar): HTMLElement {
  const el = doc.createElement('span');
  el.className = 'greenline-bar' + (bar.kind ? ' ' + bar.kind : '');
  const fill = doc.createElement('span');
  fill.className = 'greenline-bar-fill';
  fill.style.width = Math.round(bar.fill * 100) + '%';
  el.appendChild(fill);
  const label = doc.createElement('span');
  label.className = 'greenline-bar-text';
  if (bar.pause) {
    const mark = doc.createElement('span');
    mark.className = 'greenline-pause';
    mark.appendChild(doc.createElement('i'));
    mark.appendChild(doc.createElement('i'));
    label.appendChild(mark);
  }
  label.appendChild(doc.createTextNode(bar.text));
  el.appendChild(label);
  return el;
}

type CardNode = HTMLElement & { __greenlineCard?: string };

// One card shows at a time, so the frame keeps one pot card node, in the card node next to the
// pointer, and only it is built. It is built again only when its model changes (at most once each
// game minute), so the frame step does not write the DOM on each frame.
export function setCard(doc: Document, el: HTMLElement, model: CardModel): void {
  let tip = doc.querySelector<CardNode>('.greenline-pot-card');
  if (!tip) {
    tip = doc.createElement('div') as CardNode;
    tip.className = 'entry-tip greenline-tip greenline-pot-card';
  }
  if (!el.classList.contains('has-entry-tip')) el.classList.add('has-entry-tip');
  if (tip.parentNode !== el) el.appendChild(tip);
  const key = JSON.stringify(model);
  if (tip.__greenlineCard === key) return;
  tip.__greenlineCard = key;
  toggleClass(tip, 'greenline-zh', store.data!.lang === 'zh');
  while (tip.firstChild) tip.removeChild(tip.firstChild);

  const head = doc.createElement('div');
  head.className = 'greenline-card-head';
  // The large icon sits in the game's icon slot (the icon-wrapper of the planting window).
  const slot = doc.createElement('span');
  slot.className = 'greenline-card-slot';
  const img = doc.createElement('img');
  if (model.head.icon) img.setAttribute('src', model.head.icon);
  slot.appendChild(img);
  head.appendChild(slot);
  const names = doc.createElement('span');
  names.className = 'greenline-card-names' + (model.head.sub ? '' : ' greenline-card-one');
  names.appendChild(namedLine(doc, 'greenline-card-name', model.head.name, model.head.size));
  if (model.head.sub) names.appendChild(namedLine(doc, 'greenline-card-sub', model.head.sub, model.head.subSize, model.head.subIcon));
  head.appendChild(names);
  tip.appendChild(head);

  const grid = doc.createElement('div');
  grid.className = 'greenline-card-rows';
  for (let i = 0; i < model.rows.length; i++) {
    const r = model.rows[i];
    const rowEl = doc.createElement('div');
    rowEl.className = 'greenline-card-row';
    const k = doc.createElement('span');
    k.className = 'greenline-card-key';
    k.textContent = r.key;
    rowEl.appendChild(k);
    const v = doc.createElement('span');
    v.className = 'greenline-card-value' + (r.cls ? ' ' + r.cls : '');
    if (r.bar) {
      v.appendChild(barEl(doc, r.bar));
    } else {
      if (r.icon) {
        const icon = doc.createElement('img');
        icon.setAttribute('src', r.icon);
        v.appendChild(icon);
      }
      if (r.parts) {
        for (let n = 0; n < r.parts.length; n++) {
          const part = doc.createElement('span');
          part.className = r.parts[n].cls;
          part.textContent = r.parts[n].text;
          v.appendChild(part);
        }
      } else {
        v.appendChild(doc.createTextNode(r.text!));
      }
    }
    rowEl.appendChild(v);
    grid.appendChild(rowEl);
  }
  tip.appendChild(grid);
}
