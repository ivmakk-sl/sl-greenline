// Runs plantpanel.js against the game's own PlantPanel.html (the planting window) and PlantingDetails.html
// (the window of a growing crop), so a game update that renames or removes a page part that the script
// depends on shows up here instead of only in the game.
//
// The windows are Vue-rendered in the game. The test does not run the page scripts: each HTML holds the
// Vue template as plain markup, so the parts that the script looks for exist.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import url from 'node:url';
import vm from 'node:vm';
import { JSDOM } from 'jsdom';
import { test } from 'vitest';

// The jsdom windows carry the game's page globals (state, eval) and the test's own frame hooks, which
// have no types.
type Any = any;

const HERE = path.dirname(url.fileURLToPath(import.meta.url));

const GAME_DIR = process.env.SL_GAME_DIR ||
  'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const UI_DIR = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI');
const FILES = {
  PlantPanel: path.join(UI_DIR, 'PlantPanel', 'PlantPanel.html'),
  PlantingDetails: path.join(UI_DIR, 'PlantingDetails', 'PlantingDetails.html')
};
const SRC_DIR = path.join(HERE, '..', '..', 'src', 'Web');
// The Vite bundle of src/Web/plantpanel/ (npm test builds it first).
const SCRIPT_PATH = path.join(HERE, '..', '..', 'obj', 'page', 'plantpanel.js');

if (!Object.values(FILES).every((f: Any) => fs.existsSync(f))) {
  test.skip('plantpanel.js against the game pages: ' + `game files not found under SL_GAME_DIR (${GAME_DIR})`, () => {});
} else {
  const script = fs.readFileSync(SCRIPT_PATH, 'utf8');
  // The CSS that C# hands to plantpanel.js (PlantPanelScript.SetState): the tokens, then the window rules.
  const plantCss = ['tokens.css', 'plantpanel.css'].map((f: Any) => fs.readFileSync(path.join(SRC_DIR, f), 'utf8')).join('\n');
  const html: Record<string, string> = {};
  for (const [kind, file] of Object.entries(FILES)) html[kind] = fs.readFileSync(file, 'utf8');

  async function loadPage(kind?: Any, text?: Any) {
    const win = new JSDOM(text || html[kind], { url: url.pathToFileURL((FILES as Any)[kind]).href }).window as Any;
    await new Promise((resolve: Any) => win.addEventListener('load', resolve));
    return win;
  }

  // A blank window that stands in for Root.html, with a mutable frame list.
  function makeRoot(...frames: Any[]) {
    const root = new JSDOM('<!doctype html><html><body></body></html>', { url: 'file:///Root.html' }).window as Any;
    const box = { current: frames };
    const qsa = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (sel: Any) => (sel === 'iframe' ? box.current.map((w: Any) => ({ contentWindow: w })) : qsa(sel));
    root.__setFrames = (...ws: Any[]) => { box.current = ws; };
    root.__posted = [];
    root.postMessage = (msg: Any) => root.__posted.push(msg);
    vm.createContext(root);
    vm.runInContext('window.__greenlinePlantCss=' + JSON.stringify(plantCss) + ';' + script, root, { filename: 'plantpanel.js' });
    return root;
  }

  const WORDS = { autoReplant: 'Auto-replant', autoFert: 'Auto-fertilize' };
  const STATE = { pot: '4294990630', replant: true, fert: false, lastCrop: null, words: WORDS };
  const LAST = { label: 'Last crop:', name: 'Watermelon', icon: '../../Res/Food/UI_Item_Icon_Food_xigua.png', red: '' };
  const STOPPED = { ...LAST, red: 'Replant stopped: seeds 0/1' };

  // The declarations of one rule in the CSS that C# hands to the script.
  function cssRule(selector: string): string {
    const css = plantCss.replace(/\/\*[\s\S]*?\*\//g, '');
    const start = css.indexOf(selector + ' {');
    assert.ok(start >= 0, `no rule ${selector} in plantpanel.css`);
    return css.slice(css.indexOf('{', start) + 1, css.indexOf('}', start));
  }

  async function setup(t?: Any, kind?: Any, state?: Any) {
    const page = await loadPage(kind);
    const root = makeRoot(page);
    t.onTestFinished(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState(state || STATE);
    const result = root.__greenlinePlant.install();
    return { page, root, doc: page.document, result };
  }

  const options = (doc: Any) => [...doc.querySelectorAll('.greenline-option')];
  const values = (doc: Any) => options(doc).map((o: Any) => [o.getAttribute('data-key'), o.querySelector('input').checked, o.textContent.trim()]);

  test('plantpanel: each window gets one style node with the CSS that C# hands to the script', async (t: Any) => {
    for (const kind of ['PlantPanel', 'PlantingDetails']) {
      const { doc, root } = await setup(t, kind);
      root.__greenlinePlant.install();

      const styles = doc.querySelectorAll('#greenline-plant-style');
      assert.equal(styles.length, 1, kind);
      assert.equal(styles[0].textContent, plantCss, kind);
    }
  });

  test('plantpanel: the planting window header has the two checkboxes before the close button', async (t: Any) => {
    const { doc, result } = await setup(t, 'PlantPanel');

    assert.equal(result, 'installed');
    const group = doc.querySelector('.greenline-options');
    assert.ok(group, 'no checkbox group');
    const header = doc.querySelector('.plant-system h1').parentNode.parentNode;
    assert.equal(group.parentNode, header);
    assert.equal(group.nextElementSibling.tagName, 'BUTTON', 'the group is right before the close button');
    assert.deepEqual(values(doc), [['replant', true, 'Auto-replant'], ['fert', false, 'Auto-fertilize']]);
    assert.ok(options(doc).every((o: Any) => o.hasAttribute('data-interactive')));
  });

  test('plantpanel: a click on a checkbox posts GREENLINE_POT_OPTION with the pot, the key, and the new value', async (t: Any) => {
    const { doc, root } = await setup(t, 'PlantPanel');

    options(doc)[1].querySelector('input').click();

    assert.deepEqual(root.__posted.map((m: Any) => [m.type, m.data.pot, m.data.key, m.data.on]),
      [['GREENLINE_POT_OPTION', '4294990630', 'fert', true]]);
  });

  test('plantpanel: a checkbox that the window builds again after a click shows the new value', async (t: Any) => {
    const { doc, root } = await setup(t, 'PlantPanel');

    options(doc)[1].querySelector('input').click();
    doc.querySelector('.greenline-options').remove();
    root.__greenlinePlant.install();

    assert.deepEqual(values(doc), [['replant', true, 'Auto-replant'], ['fert', true, 'Auto-fertilize']]);
  });

  test('plantpanel: the last-crop line goes at the end of the PLANTING title of the left column, with the crop and the red line', async (t: Any) => {
    const { doc, result } = await setup(t, 'PlantPanel', { ...STATE, lastCrop: STOPPED });

    assert.equal(result, 'installed');
    const title = doc.querySelector('.plant-system .panel-title');
    const line = doc.querySelector('.greenline-last-crop');
    assert.ok(line, 'no last-crop line');
    assert.equal(line.parentNode, title, 'the line is in the first panel title');
    assert.equal(line, title.lastElementChild);
    assert.equal(line.children.length, 2, 'at most two lines');
    assert.equal(line.querySelector('.greenline-last-crop-label').textContent, 'Last crop:');
    assert.equal(line.querySelector('.greenline-last-crop-head img').getAttribute('src'), STOPPED.icon);
    assert.equal(line.querySelector('.greenline-last-crop-name').textContent, 'Watermelon');
    assert.equal(line.querySelector('.greenline-last-crop-red').textContent, 'Replant stopped: seeds 0/1');
    assert.equal(line.getAttribute('title'), 'Last crop: Watermelon\nReplant stopped: seeds 0/1', 'the hover tip has the full text');
    assert.equal(doc.querySelector('.greenline-reason'), null, 'no reason line above the Plant button');
  });

  test('plantpanel: a planting window by hand shows the last crop with no red line', async (t: Any) => {
    const { doc } = await setup(t, 'PlantPanel', { ...STATE, lastCrop: LAST });

    const line = doc.querySelector('.greenline-last-crop');
    assert.equal(line.children.length, 1);
    assert.equal(line.querySelector('.greenline-last-crop-red'), null);
    assert.equal(line.getAttribute('title'), 'Last crop: Watermelon');
  });

  test('plantpanel: no last crop shows no line, and the line goes away when the next state has none', async (t: Any) => {
    const { doc, root } = await setup(t, 'PlantPanel', { ...STATE, lastCrop: STOPPED });
    assert.ok(doc.querySelector('.greenline-last-crop'));

    root.__greenlinePlant.setState(STATE);

    assert.equal(doc.querySelector('.greenline-last-crop'), null);
    assert.equal(doc.querySelector('.greenline-reason'), null);
  });

  test('plantpanel: the last-crop line keeps the height of the title row and cuts a long text with an ellipsis', () => {
    const line = cssRule('.greenline-last-crop');
    assert.match(line, /margin-block:\s*-\d+px/, 'negative block margins keep the box inside the title row');
    assert.match(line, /flex:\s*1 1 0/, 'the free width of the title row, right of the title');
    assert.match(line, /width:\s*0/, 'a long text never widens the column');
    assert.match(line, /white-space:\s*nowrap/);
    assert.match(line, /min-width:\s*0/);
    assert.match(cssRule('.greenline-last-crop > *'), /text-overflow:\s*ellipsis/);
    assert.match(cssRule('.greenline-last-crop-name'), /text-overflow:\s*ellipsis/);
  });

  test('plantpanel: a missing panel title turns off only the last-crop line, and is named', async (t: Any) => {
    const page = await loadPage('PlantPanel', html.PlantPanel.replace(/panel-title/g, 'panel-head'));
    const root = makeRoot(page);
    t.onTestFinished(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState({ ...STATE, lastCrop: STOPPED });

    assert.equal(root.__greenlinePlant.install(), 'installed; missing: PlantPanel .plant-system .panel-title');
    assert.equal(options(page.document).length, 2, 'the checkboxes stay');
    assert.equal(page.document.querySelector('.greenline-last-crop'), null);
  });

  test('plantpanel: the window of a growing crop has the two checkboxes under the timer', async (t: Any) => {
    const { doc, result } = await setup(t, 'PlantingDetails', { ...STATE, replant: false, fert: true, lastCrop: STOPPED });

    assert.equal(result, 'installed');
    const group = doc.querySelector('.greenline-options');
    assert.equal(group.parentNode, doc.querySelector('.plant-system .panel'));
    assert.equal(group, group.parentNode.lastElementChild);
    assert.deepEqual(values(doc), [['replant', false, 'Auto-replant'], ['fert', true, 'Auto-fertilize']]);
    assert.equal(doc.querySelector('.greenline-last-crop'), null, 'no last-crop line in this window');
  });

  test('plantpanel: a second setState with the same values writes nothing', async (t: Any) => {
    const { page, doc, root } = await setup(t, 'PlantPanel', { ...STATE, lastCrop: STOPPED });
    const records: Any[] = [];
    const observer = new page.MutationObserver((list: Any) => records.push(...list));
    observer.observe(doc.body, { childList: true, subtree: true, characterData: true, attributes: true });

    root.__greenlinePlant.setState({ ...STATE, lastCrop: { ...STOPPED } });
    await new Promise((r: Any) => setTimeout(r, 20));

    assert.equal(records.length, 0);
  });

  test('plantpanel: the install succeeds when the window frame appears after the first attempt', async (t: Any) => {
    const page = await loadPage('PlantPanel');
    const root = makeRoot();
    t.onTestFinished(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState(STATE);

    assert.equal(root.__greenlinePlant.install(), 'no frame');
    root.__setFrames(page);
    await new Promise((r: Any) => setTimeout(r, 400));

    assert.equal(options(page.document).length, 2);
  });

  test('plantpanel: a missing page part turns the script off for that window and is named', async (t: Any) => {
    const page = await loadPage('PlantPanel', html.PlantPanel.replace(/<h1 /g, '<h2 ').replace(/<\/h1>/g, '</h2>'));
    const root = makeRoot(page);
    t.onTestFinished(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState({ ...STATE, lastCrop: STOPPED });

    assert.equal(root.__greenlinePlant.install(), 'missing: PlantPanel .plant-system h1');
    assert.equal(page.document.querySelector('.greenline-options'), null);
  });
}
