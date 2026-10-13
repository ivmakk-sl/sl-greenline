// The windows, the page parts that the script needs, the current pot values, and the frame lookup.
import type { PageName, PlantState } from './types';
import { word as modWord } from '../../Shared/i18n/web/words';

interface PageSpec {
  parts: string[];
  // The title that the last-crop line goes into, or '' for a window with no line. A missing title turns
  // off only the line.
  lineTitle: string;
  place(doc: Document, group: Element): void;
}

// For each window: the page parts that the script needs (a game update that renames one turns the
// script off for that window), where the checkboxes go, and where the last-crop line goes.
export const PAGES: Record<PageName, PageSpec> = {
  PlantPanel: {
    parts: ['.plant-system h1'],
    // The first panel title is the one of the left column (envLabel, the game's "Planting").
    lineTitle: '.plant-system .panel-title',
    // The header row holds the title block and the close button; the checkboxes go between them.
    place(doc, group) {
      const header = doc.querySelector('.plant-system h1')!.parentNode!.parentNode as Element;
      const close = header.lastElementChild === group ? group.previousElementSibling : header.lastElementChild;
      if (group.parentNode !== header || group.nextElementSibling !== close) header.insertBefore(group, close);
    }
  },
  PlantingDetails: {
    parts: ['.plant-system .panel'],
    lineTitle: '',
    place(doc, group) {
      const panel = doc.querySelector('.plant-system .panel')!;
      if (panel.lastElementChild !== group) panel.appendChild(group);
    }
  }
};

// The pot values of the windows, set by setState().
export const store: { current: PlantState } = {
  current: { pot: '', replant: true, fert: false, lastCrop: null, words: {} }
};

// The mod text of a name that C# sent with setState, or the name. C# calls setState before each install, so
// the page has no word of its own.
export function word(key: string): string {
  return modWord(store.current.words, key);
}

// The loaded frames of the two windows, as [page name, document].
export function windowDocs(): [PageName, Document][] {
  const frames = document.querySelectorAll('iframe'), found: [PageName, Document][] = [];
  for (let i = 0; i < frames.length; i++) {
    let w: Window | null;
    try { w = frames[i].contentWindow; } catch (e) { continue; }
    const m = w && String(w.location).match(/(PlantPanel|PlantingDetails)\.html/i);
    if (!m) continue;
    const doc = w!.document;
    if (!doc || !doc.body || doc.readyState === 'loading') continue;
    found.push([m[1] as PageName, doc]);
  }
  return found;
}

export function missingParts(name: PageName, doc: Document): string[] {
  return PAGES[name].parts.filter((part) => !doc.querySelector(part));
}

// The title of the last-crop line, or null when the window has no line or the game page lost it.
export function lineTitle(name: PageName, doc: Document): Element | null {
  return PAGES[name].lineTitle ? doc.querySelector(PAGES[name].lineTitle) : null;
}
