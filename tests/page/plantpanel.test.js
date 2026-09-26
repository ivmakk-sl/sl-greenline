// Runs plantpanel.js against the game's own PlantPanel.html (the planting window) and PlantingDetails.html
// (the window of a growing crop), so a game update that renames or removes a page part that the script
// depends on shows up here instead of only in the game.
//
// The windows are Vue-rendered in the game. The test does not run the page scripts: each HTML holds the
// Vue template as plain markup, so the parts that the script looks for exist.
'use strict';

const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const url = require('node:url');
const vm = require('node:vm');
const { JSDOM } = require('jsdom');

const GAME_DIR = process.env.SL_GAME_DIR ||
  'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Survival Log';
const UI_DIR = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI');
const FILES = {
  PlantPanel: path.join(UI_DIR, 'PlantPanel', 'PlantPanel.html'),
  PlantingDetails: path.join(UI_DIR, 'PlantingDetails', 'PlantingDetails.html')
};
const SRC_DIR = path.join(__dirname, '..', '..', 'src', 'Web');
const SCRIPT_PATH = path.join(SRC_DIR, 'plantpanel.js');

if (!Object.values(FILES).every((f) => fs.existsSync(f))) {
  test('plantpanel.js against the game pages', { skip: `game files not found under SL_GAME_DIR (${GAME_DIR})` }, () => {});
} else {
  const script = fs.readFileSync(SCRIPT_PATH, 'utf8');
  // The CSS that C# hands to plantpanel.js (PlantPanelScript.SetState): the tokens, then the window rules.
  const plantCss = ['tokens.css', 'plantpanel.css'].map((f) => fs.readFileSync(path.join(SRC_DIR, f), 'utf8')).join('\n');
  const html = {};
  for (const [kind, file] of Object.entries(FILES)) html[kind] = fs.readFileSync(file, 'utf8');

  async function loadPage(kind, text) {
    const win = new JSDOM(text || html[kind], { url: url.pathToFileURL(FILES[kind]).href }).window;
    await new Promise((resolve) => win.addEventListener('load', resolve));
    return win;
  }

  // A blank window that stands in for Root.html, with a mutable frame list.
  function makeRoot(...frames) {
    const root = new JSDOM('<!doctype html><html><body></body></html>', { url: 'file:///Root.html' }).window;
    const box = { current: frames };
    const qsa = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (sel) => (sel === 'iframe' ? box.current.map((w) => ({ contentWindow: w })) : qsa(sel));
    root.__setFrames = (...ws) => { box.current = ws; };
    root.__posted = [];
    root.postMessage = (msg) => root.__posted.push(msg);
    vm.createContext(root);
    vm.runInContext('window.__greenlinePlantCss=' + JSON.stringify(plantCss) + ';' + script, root, { filename: 'plantpanel.js' });
    return root;
  }

  const WORDS = { autoReplant: 'Auto-replant', autoFert: 'Auto-fertilize' };
  const STATE = { pot: '4294990630', replant: true, fert: false, reason: '', words: WORDS };

  async function setup(t, kind, state) {
    const page = await loadPage(kind);
    const root = makeRoot(page);
    t.after(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState(state || STATE);
    const result = root.__greenlinePlant.install();
    return { page, root, doc: page.document, result };
  }

  const options = (doc) => [...doc.querySelectorAll('.greenline-option')];
  const values = (doc) => options(doc).map((o) => [o.getAttribute('data-key'), o.querySelector('input').checked, o.textContent.trim()]);

  test('plantpanel: each window gets one style node with the CSS that C# hands to the script', async (t) => {
    for (const kind of ['PlantPanel', 'PlantingDetails']) {
      const { doc, root } = await setup(t, kind);
      root.__greenlinePlant.install();

      const styles = doc.querySelectorAll('#greenline-plant-style');
      assert.equal(styles.length, 1, kind);
      assert.equal(styles[0].textContent, plantCss, kind);
    }
  });

  test('plantpanel: the planting window header has the two checkboxes before the close button', async (t) => {
    const { doc, result } = await setup(t, 'PlantPanel');

    assert.equal(result, 'installed');
    const group = doc.querySelector('.greenline-options');
    assert.ok(group, 'no checkbox group');
    const header = doc.querySelector('.plant-system h1').parentNode.parentNode;
    assert.equal(group.parentNode, header);
    assert.equal(group.nextElementSibling.tagName, 'BUTTON', 'the group is right before the close button');
    assert.deepEqual(values(doc), [['replant', true, 'Auto-replant'], ['fert', false, 'Auto-fertilize']]);
    assert.ok(options(doc).every((o) => o.hasAttribute('data-interactive')));
  });

  test('plantpanel: a click on a checkbox posts GREENLINE_POT_OPTION with the pot, the key, and the new value', async (t) => {
    const { doc, root } = await setup(t, 'PlantPanel');

    options(doc)[1].querySelector('input').click();

    assert.deepEqual(root.__posted.map((m) => [m.type, m.data.pot, m.data.key, m.data.on]),
      [['GREENLINE_POT_OPTION', '4294990630', 'fert', true]]);
  });

  test('plantpanel: a checkbox that the window builds again after a click shows the new value', async (t) => {
    const { doc, root } = await setup(t, 'PlantPanel');

    options(doc)[1].querySelector('input').click();
    doc.querySelector('.greenline-options').remove();
    root.__greenlinePlant.install();

    assert.deepEqual(values(doc), [['replant', true, 'Auto-replant'], ['fert', true, 'Auto-fertilize']]);
  });

  test('plantpanel: the reason line shows above the Start button, and goes away with no reason', async (t) => {
    const { doc, root } = await setup(t, 'PlantPanel', { ...STATE, reason: 'Auto-replant stopped: 0 of 1 Strawberry Seeds' });

    const line = doc.querySelector('.greenline-reason');
    assert.ok(line);
    assert.equal(line.nextElementSibling, doc.querySelector('.plant-btn'));
    assert.equal(line.textContent, 'Auto-replant stopped: 0 of 1 Strawberry Seeds');

    root.__greenlinePlant.setState(STATE);
    assert.equal(doc.querySelector('.greenline-reason'), null);
  });

  test('plantpanel: the window of a growing crop has the two checkboxes under the timer', async (t) => {
    const { doc, result } = await setup(t, 'PlantingDetails', { ...STATE, replant: false, fert: true });

    assert.equal(result, 'installed');
    const group = doc.querySelector('.greenline-options');
    assert.equal(group.parentNode, doc.querySelector('.plant-system .panel'));
    assert.equal(group, group.parentNode.lastElementChild);
    assert.deepEqual(values(doc), [['replant', false, 'Auto-replant'], ['fert', true, 'Auto-fertilize']]);
    assert.equal(doc.querySelector('.greenline-reason'), null, 'no reason line in this window');
  });

  test('plantpanel: a second setState with the same values writes nothing', async (t) => {
    const { page, doc, root } = await setup(t, 'PlantPanel', { ...STATE, reason: 'x' });
    const records = [];
    const observer = new page.MutationObserver((list) => records.push(...list));
    observer.observe(doc.body, { childList: true, subtree: true, characterData: true, attributes: true });

    root.__greenlinePlant.setState({ ...STATE, reason: 'x' });
    await new Promise((r) => setTimeout(r, 20));

    assert.equal(records.length, 0);
  });

  test('plantpanel: the install succeeds when the window frame appears after the first attempt', async (t) => {
    const page = await loadPage('PlantPanel');
    const root = makeRoot();
    t.after(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState(STATE);

    assert.equal(root.__greenlinePlant.install(), 'no frame');
    root.__setFrames(page);
    await new Promise((r) => setTimeout(r, 400));

    assert.equal(options(page.document).length, 2);
  });

  test('plantpanel: a missing page part turns the script off for that window and is named', async (t) => {
    const page = await loadPage('PlantPanel', html.PlantPanel.replace(/plant-btn/g, 'start-btn'));
    const root = makeRoot(page);
    t.after(() => { page.close(); root.close(); });
    root.__greenlinePlant.setState({ ...STATE, reason: 'x' });

    assert.equal(root.__greenlinePlant.install(), 'missing: PlantPanel .plant-btn');
    assert.equal(page.document.querySelector('.greenline-options'), null);
  });
}
