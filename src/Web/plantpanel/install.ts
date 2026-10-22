// The frame observer and the install pass with its retry.
import { PAGES, lineTitle, missingParts, postMod, store, windowDocs } from './core';
import { apply } from './options';
import type { PageName } from './types';

function watch(name: PageName, doc: Document): void {
  if (doc.__greenlineObserver) return;
  const Observer = doc.defaultView && (doc.defaultView as Window & typeof globalThis).MutationObserver;
  if (!Observer) return;
  doc.__greenlineObserver = new Observer(() => {
    if (missingParts(name, doc).length) return;
    const needsLine = !!store.current.lastCrop && !!lineTitle(name, doc) && !doc.querySelector('.greenline-last-crop');
    if (!doc.querySelector('.greenline-options') || needsLine) apply(name, doc);
  });
  doc.__greenlineObserver.observe(doc.body, { childList: true, subtree: true });
}

// One install pass, also each retry. A result with a problem (a missing part, an error) goes to C# as the page
// message "plant panel <result>"; "installed" and "no frame" (the frame is not loaded yet) post nothing.
export function attempt(retries: number): string {
  const result = run(retries);
  if (result !== 'installed' && result !== 'no frame') postMod('plant panel ' + result);
  return result;
}

function run(retries: number): string {
  try {
    const docs = windowDocs();
    if (!docs.length) {
      if (retries > 0) setTimeout(() => { attempt(retries - 1); }, 300);
      return 'no frame';
    }
    const missing: string[] = [], lineMissing: string[] = [];
    for (let i = 0; i < docs.length; i++) {
      const m = missingParts(docs[i][0], docs[i][1]);
      if (m.length) { missing.push(docs[i][0] + ' ' + m.join(', ')); continue; }
      apply(docs[i][0], docs[i][1]);
      watch(docs[i][0], docs[i][1]);
      const title = PAGES[docs[i][0]].lineTitle;
      if (title && !lineTitle(docs[i][0], docs[i][1])) lineMissing.push(docs[i][0] + ' ' + title);
    }
    if (missing.length) return 'missing: ' + missing.join('; ');
    return lineMissing.length ? 'installed; missing: ' + lineMissing.join('; ') : 'installed';
  } catch (e) {
    return 'error: ' + (e && (e as Error).message ? (e as Error).message : String(e));
  }
}
