// Runs page.js against the game's own CoreUI1.html, so a game update that renames or removes a page
// part page.js depends on shows up here instead of only in the game. The pot data is the fixture that
// the xUnit test of GridLogic.ToJson compares with, so both sides of the JSON contract use one file.
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
const CORE_UI1_HTML = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI', 'CoreUI1', 'CoreUI1.html');
const SRC_DIR = path.join(__dirname, '..', '..', 'src', 'Web');
const PAGE_JS_PATH = path.join(SRC_DIR, 'page.js');
const FIXTURE_PATH = path.join(__dirname, '..', 'fixtures', 'pots.json');

const gameFileExists = fs.existsSync(CORE_UI1_HTML);

if (!gameFileExists) {
  test('page.js against the game page', { skip: `game files not found under SL_GAME_DIR (${GAME_DIR}); set SL_GAME_DIR to the game folder` }, () => {});
} else {
  // page.js with each "// @include <file>" line replaced by that file of page/, as C# joins it
  // (PageIncludes.Join).
  const INCLUDE_LINE = /^[ \t]*\/\/ @include (\S+)[ \t\r]*$/gm;
  const pageWrapper = fs.readFileSync(PAGE_JS_PATH, 'utf8');
  const pageJs = pageWrapper.replace(INCLUDE_LINE,
    (_, name) => fs.readFileSync(path.join(SRC_DIR, 'page', name), 'utf8').replace(/\s+$/, ''));
  // The CSS that C# hands to page.js (PageScript.Css): the tokens, then the page rules.
  const pageCss = ['tokens.css', 'page.css'].map((f) => fs.readFileSync(path.join(SRC_DIR, f), 'utf8')).join('\n');
  const coreUi1Html = fs.readFileSync(CORE_UI1_HTML, 'utf8');

  function fixture() { return JSON.parse(fs.readFileSync(FIXTURE_PATH, 'utf8')); }

  // Pulls the page part names out of the FEATURES table of page.js, so this test never copies the names.
  function featureNames() {
    const table = pageJs.match(/var FEATURES = \{([\s\S]*?)\};/);
    assert.ok(table, 'FEATURES table not found in page.js');
    const names = [];
    const quoted = /'([^']+)'/g;
    let m;
    while ((m = quoted.exec(table[1]))) names.push(m[1]);
    assert.ok(names.length > 0, 'no part names parsed out of the FEATURES table');
    return names;
  }

  test('static: every FEATURES part name of page.js exists in CoreUI1.html', () => {
    for (const name of featureNames()) {
      const present = name.replace(/^[#.]/, '').split('.').every((piece) => coreUi1Html.includes(piece));
      assert.ok(present, `page part "${name}" not found in CoreUI1.html`);
    }
  });

  test('static: page.js includes each file of page/ once', () => {
    const included = [...pageWrapper.matchAll(INCLUDE_LINE)].map((m) => m[1]).sort();
    assert.deepEqual(included, fs.readdirSync(path.join(SRC_DIR, 'page')).filter((f) => f.endsWith('.js')).sort());
  });

  // CoreUI1.html starts its own requestAnimationFrame loops, so each window is closed after its test.
  async function loadCoreWindow(t) {
    const dom = new JSDOM(coreUi1Html, {
      url: url.pathToFileURL(CORE_UI1_HTML).href,
      runScripts: 'dangerously',
      resources: 'usable',
      pretendToBeVisual: true
    });
    t.after(() => dom.window.close());
    await new Promise((resolve, reject) => {
      dom.window.addEventListener('load', resolve);
      setTimeout(() => reject(new Error('CoreUI1.html did not fire load within 5s')), 5000);
    });
    return dom.window;
  }

  // A blank window that stands in for the root page (Root.html), which holds the CoreUI1 iframe.
  function makeRootWindow(t, coreWindow) {
    const root = new JSDOM('<!doctype html><html><body></body></html>', {
      url: 'file:///Root.html',
      pretendToBeVisual: true
    }).window;
    t.after(() => root.close());
    const frameBox = { current: coreWindow || null };
    const originalQSA = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (selector) =>
      selector === 'iframe' ? (frameBox.current ? [{ contentWindow: frameBox.current }] : []) : originalQSA(selector);
    vm.createContext(root);
    return root;
  }

  function runPageJs(root, callExpr) {
    const css = 'window.__greenlineCss=' + JSON.stringify(pageCss) + ';';
    return vm.runInContext(css + pageJs + ';' + callExpr, root, { filename: 'page.js' });
  }

  function setPots(root, data) {
    return runPageJs(root, 'window.__greenline.setPots(' + JSON.stringify(data) + ')');
  }

  function wait(win, ms) {
    return new Promise((resolve) => win.setTimeout(resolve, ms));
  }

  // The plant list message that the game sends (WebUI_CoreUI1_PlantMatureMsg).
  async function postPlants(coreWindow, plants) {
    coreWindow.postMessage({
      type: 'WebUI_CoreUI1_PlantMatureMsg',
      data: { containerActive: true, plants }
    }, '*');
    await wait(coreWindow, 30);
  }

  function plant(rowId, status, totalSeconds, elapsedSeconds) {
    return {
      showInstanceId: rowId, plantName: 'Crop ' + rowId, status,
      harvestTotalSeconds: totalSeconds || 0, harvestElapsedSeconds: elapsedSeconds || 0, dispatchUtcMs: 0
    };
  }

  // The game rows of the fixture pots: 5001 growing with Pest (status 3), 5002 mature, 5004 withered.
  const FIXTURE_ROWS = [plant(5001, 3, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)];

  // Waits until Vue's enter transition of the popover ends, so its class writes are not counted as
  // writes of the mod.
  async function expand(coreWindow) {
    coreWindow.eval('state.maturePlantsExpanded = true');
    for (let i = 0; i < 50; i++) {
      await wait(coreWindow, 20);
      const popover = coreWindow.document.querySelector('.mature-popover');
      if (popover && !/mature-popover-enter/.test(popover.className)) return;
    }
    throw new Error('the popover enter transition did not end within 1s');
  }

  async function setup(t, opts) {
    opts = opts || {};
    const core = await loadCoreWindow(t);
    await postPlants(core, opts.rows || FIXTURE_ROWS);
    if (opts.expanded !== false) await expand(core);
    const root = makeRootWindow(t, core);
    const result = setPots(root, opts.pots || fixture());
    return { core, root, result, doc: core.document };
  }

  function cell(doc, potId) {
    return doc.querySelector('.greenline-cell[data-pot-id="' + potId + '"]');
  }

  function gridPlace(el) {
    return [el.style.getPropertyValue('grid-row'), el.style.getPropertyValue('grid-column')];
  }

  test('jsdom: pot data turns the popover into a grid and hides the game rows', async (t) => {
    const { doc, core, result } = await setup(t);
    assert.equal(result, 'installed');

    const popover = doc.querySelector('.mature-popover');
    assert.ok(popover.classList.contains('greenline-grid'));
    const rows = doc.querySelectorAll('.mature-item-row');
    assert.equal(rows.length, 3);
    for (const row of rows) assert.equal(core.getComputedStyle(row).display, 'none');
  });

  test('jsdom: the style node of the frame holds the CSS that C# hands to the script', async (t) => {
    const { doc } = await setup(t);

    const styles = doc.querySelectorAll('#greenline-style');
    assert.equal(styles.length, 1);
    assert.equal(styles[0].textContent, pageCss);
  });

  // The selectors of each page.css rule whose value uses the token.
  function selectorsUsing(token) {
    const css = fs.readFileSync(path.join(SRC_DIR, 'page.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
    const out = [];
    for (const m of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
      if (m[2].includes('var(' + token + ')')) out.push(...m[1].split(',').map((s) => s.trim()));
    }
    return out;
  }

  test('jsdom: the background opacity rules reach the header, the grid, and the pot card of the game page', async (t) => {
    const { doc } = await setup(t);
    hoverCell(doc, 1001);

    const panel = selectorsUsing('--gl-panel-opacity');
    const cardRule = selectorsUsing('--gl-card-opacity');
    assert.deepEqual([panel.length, cardRule.length], [2, 1]);
    assert.ok(doc.querySelector(panel[0]).classList.contains('mature-toggle-row'), panel[0]);
    assert.ok(doc.querySelector(panel[1]).classList.contains('mature-popover'), panel[1]);
    const potCard = doc.querySelector('.greenline-pot-card');
    assert.ok(potCard && potCard.matches(cardRule[0]), cardRule[0]);
  });

  test('jsdom: each floor has a label in column 1 that spans its lines', async (t) => {
    const { doc } = await setup(t);

    const upper = doc.querySelector('.greenline-floor-label[data-floor-id="102"]');
    const home = doc.querySelector('.greenline-floor-label[data-floor-id="1"]');
    assert.equal(upper.textContent, '2F');
    assert.equal(home.textContent, 'Home');
    assert.deepEqual(gridPlace(upper), ['1 / span 1', '1']);
    assert.deepEqual(gridPlace(home), ['2 / span 1', '1']);
  });

  test('jsdom: each pot has one cell, placed by its floor and cell index', async (t) => {
    const { doc } = await setup(t);

    assert.equal(doc.querySelectorAll('.greenline-cell').length, 5);
    assert.deepEqual(gridPlace(cell(doc, 1001)), ['1', '2']);
    assert.deepEqual(gridPlace(cell(doc, 1002)), ['1', '3']);
    assert.deepEqual(gridPlace(cell(doc, 1003)), ['2', '2']);
    assert.deepEqual(gridPlace(cell(doc, 1004)), ['2', '3']);
    assert.deepEqual(gridPlace(cell(doc, 1005)), ['2', '4']);
    for (const c of doc.querySelectorAll('.greenline-cell')) assert.ok(c.hasAttribute('data-interactive'));
  });

  // 8 empty pots on the home floor, after the 2 pots of the 2nd floor.
  function eightPotFloor() {
    const data = fixture();
    const ids = [];
    for (let i = 0; i < 8; i++) {
      const id = 2000 + i;
      ids.push(id);
      data.pots[id] = Object.assign({}, data.pots['1003']);
    }
    data.floors[1].cells = ids;
    return data;
  }

  test('jsdom: a floor with 8 pots has 6 cells on its first line and 2 on the next', async (t) => {
    const { doc } = await setup(t, { pots: eightPotFloor() });

    assert.deepEqual(gridPlace(doc.querySelector('.greenline-floor-label[data-floor-id="1"]')), ['2 / span 2', '1']);
    assert.deepEqual(gridPlace(cell(doc, 2005)), ['2', '7']);
    assert.deepEqual(gridPlace(cell(doc, 2006)), ['3', '2']);
    assert.deepEqual(gridPlace(cell(doc, 2007)), ['3', '3']);
  });

  test('jsdom: the popover has cell columns for the longest line only', async (t) => {
    const small = await setup(t);
    assert.match(small.doc.querySelector('.mature-popover').style.getPropertyValue('grid-template-columns'), /repeat\(3,/);

    const big = await setup(t, { pots: eightPotFloor() });
    assert.match(big.doc.querySelector('.mature-popover').style.getPropertyValue('grid-template-columns'), /repeat\(6,/);
  });

  test('jsdom: a second setPots with the same data writes nothing to the DOM', async (t) => {
    const { doc, core, root } = await setup(t);
    // The first frames write the first countdown texts.
    await wait(core, 60);

    const records = [];
    const observer = new core.MutationObserver((list) => records.push(...list));
    observer.observe(doc.documentElement, { childList: true, subtree: true, attributes: true, attributeOldValue: true, characterData: true });
    setPots(root, fixture());
    await wait(core, 30);
    observer.disconnect();

    // The game's own countdown writes the text of its rows on each frame; only the mod's writes count.
    const ours = records.filter((r) => {
      const el = r.target.nodeType === 1 ? r.target : r.target.parentElement;
      return !(el && el.closest('.mature-item-row'));
    });
    assert.equal(ours.length, 0, `unexpected writes: ${ours.map((r) => r.type + ' ' + r.attributeName + ' old=' + r.oldValue + ' ' + (r.target.className || r.target.nodeName)).join(', ')}`);
  });

  async function collapse(coreWindow) {
    coreWindow.eval('state.maturePlantsExpanded = false');
    for (let i = 0; i < 50; i++) {
      await wait(coreWindow, 20);
      if (!coreWindow.document.querySelector('.mature-popover')) return;
    }
    throw new Error('the popover leave transition did not end within 1s');
  }

  function cellPlaces(doc) {
    const places = {};
    for (const c of doc.querySelectorAll('.greenline-cell')) {
      const id = c.getAttribute('data-pot-id');
      assert.equal(places[id], undefined, `pot ${id} has more than one cell`);
      places[id] = gridPlace(c);
    }
    return places;
  }

  test('jsdom: a collapse and an expand bring the grid back', async (t) => {
    const { doc, core } = await setup(t);
    const before = cellPlaces(doc);

    await collapse(core);
    await expand(core);

    assert.ok(doc.querySelector('.mature-popover').classList.contains('greenline-grid'));
    assert.deepEqual(cellPlaces(doc), before);
    assert.equal(doc.querySelectorAll('.greenline-floor-label').length, 2);
  });

  test('jsdom: a row added or removed by the game keeps each cell in its place', async (t) => {
    const { doc, core } = await setup(t);
    const before = cellPlaces(doc);

    await postPlants(core, [plant(5001, 3, 7200, 0), plant(5004, 2)]);
    assert.deepEqual(cellPlaces(doc), before);
    await postPlants(core, [plant(5001, 3, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2), plant(5009, 0, 9000, 0)]);
    assert.deepEqual(cellPlaces(doc), before);
  });

  test('jsdom: when the game list goes away, no mod node stays', async (t) => {
    const { doc, core } = await setup(t);

    await postPlants(core, []);

    assert.equal(doc.querySelector('.mature-panel'), null);
    assert.equal(doc.querySelectorAll('.greenline-cell, .greenline-floor-label').length, 0);
  });

  test('jsdom: a countdown text write of the game triggers no work of the observer', async (t) => {
    const { core } = await setup(t);
    const originalEval = core.eval.bind(core);
    let stateReads = 0;
    core.eval = (code) => { if (code === 'state') stateReads++; return originalEval(code); };

    // The withered row: the game's own countdown loop does not rewrite its text.
    originalEval('state.maturePlants.find((p) => p.status === 2).remainText = "9:99"');
    await wait(core, 50);

    const texts = [...core.document.querySelectorAll('.mature-item-row .mature-item-remain')].map((e) => e.textContent);
    assert.ok(texts.includes('9:99'), `the game did not write the text: ${texts}`);
    assert.equal(stateReads, 0);
  });

  function iconSrc(el, selector) {
    const img = el.querySelector(selector);
    return img ? img.getAttribute('src') : null;
  }

  test('jsdom: a crop cell has the crop icon, and an empty or Poor pot cell has the pot icon', async (t) => {
    const { doc } = await setup(t);
    const pots = fixture().pots;

    assert.equal(iconSrc(cell(doc, 1001), '.greenline-icon'), pots['1001'].cropIcon);
    assert.equal(iconSrc(cell(doc, 1003), '.greenline-icon'), pots['1003'].potIcon);
    assert.equal(iconSrc(cell(doc, 1005), '.greenline-icon'), pots['1005'].potIcon);
    assert.equal(cell(doc, 1003).querySelector('.greenline-till'), null);
    assert.ok(iconSrc(cell(doc, 1005), '.greenline-till'), 'a Poor pot cell has the Till mark');
    assert.equal(cell(doc, 1001).querySelector('.greenline-till'), null);
  });

  test('jsdom: a cell with a badge has the game icon of that problem', async (t) => {
    const { doc } = await setup(t);

    assert.equal(iconSrc(cell(doc, 1001), '.greenline-badge'), '../../Res/PlantAnomaly/Pest.png');
    assert.equal(iconSrc(cell(doc, 1004), '.greenline-badge'), '../../Res/PlantAnomaly/Withered.png');
    assert.equal(cell(doc, 1002).querySelector('.greenline-badge'), null);
    assert.equal(cell(doc, 1003).querySelector('.greenline-badge'), null);
  });

  test('jsdom: only a mature cell has the gold border, and only a Pest or Frost cell is urgent', async (t) => {
    const { doc } = await setup(t);

    const mature = [...doc.querySelectorAll('.greenline-cell.greenline-mature')].map((c) => c.getAttribute('data-pot-id'));
    const urgent = [...doc.querySelectorAll('.greenline-cell.greenline-urgent')].map((c) => c.getAttribute('data-pot-id'));
    assert.deepEqual(mature, ['1002']);
    assert.deepEqual(urgent, ['1001']);
  });

  test('jsdom: a Frost crop is urgent, and a crop with Drought only is not', async (t) => {
    const data = fixture();
    data.pots['1001'].problems = ['frost'];
    data.pots['1001'].badge = 'frost';
    data.pots['1002'].state = 'growing';
    data.pots['1002'].problems = ['drought'];
    data.pots['1002'].badge = 'drought';
    const { doc } = await setup(t, { pots: data });

    assert.ok(cell(doc, 1001).classList.contains('greenline-urgent'));
    assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
    assert.equal(iconSrc(cell(doc, 1002), '.greenline-badge'), '../../Res/PlantAnomaly/Drought.png');
  });

  test('jsdom: a growing crop with a problem that only stops its growth has the red border, not the pulse', async (t) => {
    const data = fixture();
    data.pots['1002'].state = 'growing';
    data.pots['1002'].problems = ['weed'];
    data.pots['1002'].badge = 'weed';
    const { doc } = await setup(t, { pots: data });

    assert.ok(cell(doc, 1002).classList.contains('greenline-problem'));
    assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
    const problem = [...doc.querySelectorAll('.greenline-cell.greenline-problem')].map((c) => c.getAttribute('data-pot-id'));
    assert.deepEqual(problem, ['1002'], 'a Pest crop, an empty pot, a withered crop, and a Poor pot have no problem border');
  });

  test('jsdom: a growing crop that only stops its growth shows the pause mark in place of the countdown', async (t) => {
    const data = fixture();
    data.words = { stateStalled: 'Stagnant' };
    data.pots['1002'].state = 'growing';
    data.pots['1002'].problems = ['drought', 'lightLow'];
    data.pots['1002'].badge = 'drought';
    const { doc, core } = await setup(t, { pots: data, rows: [plant(5001, 0, 7200, 0), plant(5002, 6, 3600, 0), plant(5004, 2)] });
    await wait(core, 60);

    assert.equal(timeText(doc, 1002), null);
    assert.equal(cell(doc, 1002).querySelectorAll('.greenline-pause i').length, 2);
    assert.equal(cell(doc, 1001).querySelector(':scope > .greenline-pause'), null, 'a Pest crop has no pause mark');
  });

  const BASIC_FERT_ICON = '../../Res/Material/UI_Item_Icon_Mat_S_1500710801.png';
  const BADGE_ICONS_OF = { pest: '../../Res/PlantAnomaly/Pest.png', frost: '../../Res/PlantAnomaly/Frost.png' };

  // A growing research crop in pot 1002 with its problems, and its game row with the status.
  function researchData(problems) {
    const data = fixture();
    const pot = data.pots['1002'];
    pot.state = 'growing';
    pot.neverWithers = true;
    pot.problems = problems;
    pot.badge = problems[0] || '';
    pot.growRemainSeconds = 105960;
    pot.growTotalSeconds = 211920;
    return data;
  }

  test('jsdom: a research crop that needs fertilizer has the Basic Fertilizer badge, the pause mark, and the red border', async (t) => {
    const { doc, core } = await setup(t, { pots: researchData(['needFert']), rows: [plant(5001, 3, 7200, 0), plant(5002, 8, 3600, 0), plant(5004, 2)] });
    await wait(core, 60);

    assert.equal(iconSrc(cell(doc, 1002), '.greenline-badge'), BASIC_FERT_ICON);
    assert.equal(cell(doc, 1002).querySelectorAll(':scope > .greenline-pause i').length, 2);
    assert.equal(timeText(doc, 1002), null);
    assert.ok(cell(doc, 1002).classList.contains('greenline-problem'));
    assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
  });

  for (const [problem, status] of [['pest', 9], ['frost', 10]]) {
    test(`jsdom: a research crop with ${problem} has its badge, the pause mark, and the red border, and is not urgent`, async (t) => {
      const { doc, core } = await setup(t, { pots: researchData([problem]), rows: [plant(5001, 3, 7200, 0), plant(5002, status, 3600, 0), plant(5004, 2)] });
      await wait(core, 60);

      assert.equal(iconSrc(cell(doc, 1002), '.greenline-badge'), BADGE_ICONS_OF[problem]);
      assert.equal(cell(doc, 1002).querySelectorAll(':scope > .greenline-pause i').length, 2);
      assert.equal(timeText(doc, 1002), null);
      assert.ok(cell(doc, 1002).classList.contains('greenline-problem'));
      assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
      assert.ok(cell(doc, 1001).classList.contains('greenline-urgent'), 'a Pest crop that is not a research crop stays urgent');
    });
  }

  test('jsdom: a cell shows no crop name', async (t) => {
    const { doc } = await setup(t);

    // The hover card is hidden until the pointer is on the cell, so only the rest counts.
    for (const c of doc.querySelectorAll('.greenline-cell')) {
      const shown = [...c.childNodes].filter((n) => !(n.classList && n.classList.contains('entry-tip')))
        .map((n) => n.textContent).join('');
      assert.ok(!shown.includes('Watermelon') && !shown.includes('Planter'), shown);
    }
  });

  test('jsdom: a new state of a pot changes its cell content, not its place', async (t) => {
    const { doc, root } = await setup(t);
    const before = cellPlaces(doc);
    const data = fixture();
    data.pots['1001'].state = 'mature';
    data.pots['1001'].problems = [];
    data.pots['1001'].badge = '';

    setPots(root, data);

    assert.ok(cell(doc, 1001).classList.contains('greenline-mature'));
    assert.ok(!cell(doc, 1001).classList.contains('greenline-urgent'));
    assert.equal(cell(doc, 1001).querySelector('.greenline-badge'), null);
    assert.deepEqual(cellPlaces(doc), before);
  });

  test('shortTime: days and hours, hours and minutes, or minutes, in English and Chinese', async (t) => {
    const { root } = await setup(t);
    const short = (s, lang) => runPageJs(root, `window.__greenline.shortTime(${s}, '${lang}')`);

    assert.equal(short(86400 + 5 * 3600 + 6 * 60, 'en'), '1d5h');
    assert.equal(short(5 * 3600 + 6 * 60 + 30, 'en'), '5:06');
    assert.equal(short(3600, 'en'), '1:00');
    assert.equal(short(45 * 60 + 59, 'en'), '45m');
    assert.equal(short(0, 'en'), '0m');
    assert.equal(short(86400 + 5 * 3600 + 6 * 60, 'zh'), '1天5时');
    assert.equal(short(5 * 3600 + 6 * 60, 'zh'), '5:06');
    assert.equal(short(45 * 60, 'zh'), '45分');
  });

  test('fullTime: the days, hours, and minutes, in English and Chinese', async (t) => {
    const { root } = await setup(t);
    const full = (s, lang) => runPageJs(root, `window.__greenline.fullTime(${s}, '${lang}')`);

    assert.equal(full(86400 + 5 * 3600 + 6 * 60, 'en'), '1 d 5 h 6 min');
    assert.equal(full(5 * 3600 + 6 * 60, 'en'), '5 h 6 min');
    assert.equal(full(45 * 60, 'en'), '45 min');
    assert.equal(full(86400 + 5 * 3600 + 6 * 60, 'zh'), '1天5小时6分');
    assert.equal(full(45 * 60, 'zh'), '45分');
  });

  function timeText(doc, potId) {
    const el = cell(doc, potId).querySelector('.greenline-time');
    return el ? el.textContent : null;
  }

  // Sets the countdown of a game row, as the game's own frame loop keeps it.
  function setRemain(core, rowId, seconds) {
    core.eval(`state.maturePlants.find((p) => p.showInstanceId === ${rowId}).remainGameSeconds = ${seconds}`);
  }

  test('jsdom: a cell shows the countdown of its game row, only for a status with a countdown', async (t) => {
    const { doc, core, root } = await setup(t);

    setRemain(core, 5001, 5 * 3600 + 6 * 60 + 30);
    setRemain(core, 5002, 45 * 60 + 30);
    root.__greenline.step();

    assert.equal(timeText(doc, 1001), '5:06');
    assert.equal(timeText(doc, 1002), '45m');
    assert.equal(timeText(doc, 1003), null, 'an empty pot has no countdown');
    assert.equal(timeText(doc, 1004), null, 'a withered crop has no countdown');
    assert.equal(timeText(doc, 1005), null, 'a Poor pot has no countdown');
  });

  test('jsdom: step() writes the countdown of a cell from its game row', async (t) => {
    const { doc, core, root } = await setup(t);

    setRemain(core, 5001, 2 * 3600 + 7 * 60 + 30);
    root.__greenline.step();

    assert.equal(timeText(doc, 1001), '2:07');
  });

  test('jsdom: check() is ok on an applied frame, and not applied on a frame the game built again', async (t) => {
    const { root } = await setup(t);
    assert.equal(root.__greenline.check(), 'ok');

    const rebuilt = await loadCoreWindow(t);
    const fresh = makeRootWindow(t, rebuilt);
    runPageJs(fresh, '0');
    assert.equal(fresh.__greenline.check(), 'not applied');
  });

  test('jsdom: the mod registers no requestAnimationFrame callback', async (t) => {
    const core = await loadCoreWindow(t);
    await postPlants(core, FIXTURE_ROWS);
    await expand(core);
    const modCallbacks = [];
    const raf = core.requestAnimationFrame.bind(core);
    core.requestAnimationFrame = (cb) => {
      if (/updateCountdowns/.test(String(cb))) modCallbacks.push(cb);
      return raf(cb);
    };

    setPots(makeRootWindow(t, core), fixture());
    await wait(core, 60);

    assert.equal(modCallbacks.length, 0);
  });

  test('jsdom: a stalled crop (status 6) shows no countdown', async (t) => {
    const { doc, core } = await setup(t, { rows: [plant(5001, 6, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)] });
    await wait(core, 60);

    assert.equal(timeText(doc, 1001), null);
  });

  test('jsdom: the countdown writes its text only when the text changes', async (t) => {
    const { doc, core, root } = await setup(t);
    setRemain(core, 5001, 5 * 3600 + 6 * 60 + 50);
    root.__greenline.step();
    const time = cell(doc, 1001).querySelector('.greenline-time');

    const records = [];
    const observer = new core.MutationObserver((list) => records.push(...list));
    observer.observe(time, { childList: true, subtree: true, characterData: true });
    root.__greenline.step();
    root.__greenline.step();
    await wait(core, 0);
    assert.equal(records.length, 0, 'steps in the same minute wrote the text');

    setRemain(core, 5001, 5 * 3600 + 5 * 60 + 10);
    root.__greenline.step();
    await wait(core, 0);
    observer.disconnect();
    assert.equal(timeText(doc, 1001), '5:05');
    assert.equal(records.length, 1);
  });

  // The hover card of a cell: the header and the key rows.
  // The pointer moves over the cell (the page gets pointer moves over the HUD parts).
  function hoverCell(doc, potId, x, y) {
    const c = cell(doc, potId);
    c.dispatchEvent(new doc.defaultView.MouseEvent('mousemove', { bubbles: true, clientX: x || 50, clientY: y || 60 }));
    return c;
  }

  // The card of a cell: the one card node of the frame shows it next to the pointer.
  function card(doc, potId) {
    hoverCell(doc, potId);
    const node = hoverNode(doc);
    assert.ok(node && !node.hidden, 'the card shows');
    return readCard(node);
  }

  // The header and rows of the card in an element (a grid cell or the world hover node).
  function readCard(el) {
    const tip = el.querySelector(':scope > .entry-tip');
    assert.ok(tip, 'the element has its card');
    const chipOf = (el) => {
      const cells = el && el.querySelector('.greenline-chip .cells');
      return cells ? [[...cells.classList].find((k) => /^s\d$/.test(k)), cells.querySelectorAll('.cell').length] : null;
    };
    const head = tip.querySelector('.greenline-card-head');
    const name = head.querySelector('.greenline-card-name');
    const sub = head.querySelector('.greenline-card-sub');
    return {
      head: {
        icon: head.querySelector('img').getAttribute('src'),
        name: name.querySelector('.greenline-card-text').textContent,
        chip: chipOf(name),
        sub: sub ? sub.querySelector('.greenline-card-text').textContent : null,
        subIcon: sub && sub.querySelector('img') ? sub.querySelector('img').getAttribute('src') : null,
        subOrder: sub && sub.querySelector('.greenline-card-pill') ? [...sub.querySelector('.greenline-card-pill').children].map((e) => e.className || e.tagName) : null,
        subChip: chipOf(sub)
      },
      rows: [...tip.querySelectorAll('.greenline-card-row')].map((row) => {
        const value = row.querySelector('.greenline-card-value');
        const img = value.querySelector('img');
        const bar = value.querySelector('.greenline-bar');
        return {
          key: row.querySelector('.greenline-card-key').textContent,
          icon: img ? img.getAttribute('src') : null,
          text: value.textContent,
          classes: [...value.classList].filter((k) => k !== 'greenline-card-value'),
          bar: bar ? {
            fill: bar.querySelector('.greenline-bar-fill').style.width,
            kind: [...bar.classList].filter((k) => k !== 'greenline-bar'),
            pause: !!bar.querySelector('.greenline-pause')
          } : null
        };
      })
    };
  }

  function rowsWith(c, key) {
    return c.rows.filter((r) => r.key === key);
  }

  function rowWith(c, key) {
    const found = rowsWith(c, key);
    assert.equal(found.length, 1, `one "${key}" row in ${JSON.stringify(c.rows)}`);
    return found[0];
  }

  test('jsdom: the card of a crop has the crop and the pot (with a small pot icon) as its header, each with its size chip', async (t) => {
    const { doc } = await setup(t);
    const c = card(doc, 1001);

    assert.deepEqual(c.head, {
      icon: '../../Res/Food/UI_Item_Icon_Food_xigua.png', name: 'Watermelon', chip: ['s4', 4],
      sub: 'Large Planter', subIcon: '../../Res/Furniture/UI_Item_Icon_plantcommon_large.png',
      subOrder: ['IMG', 'greenline-card-text', 'greenline-chip'], subChip: ['s4', 4]
    });
  });

  test('jsdom: each problem is its own red State row, and a stalled growth bar is red with the pause mark', async (t) => {
    const { doc, core } = await setup(t);
    setRemain(core, 5001, 3600 + 5 * 60 + 50);
    await wait(core, 60);
    const c = card(doc, 1001);

    const states = rowsWith(c, 'State');
    assert.equal(states.length, 2);
    assert.equal(states[0].icon, '../../Res/PlantAnomaly/Pest.png');
    assert.ok(states[0].text.startsWith('Pest') && states[0].text.includes('1 h 5 min'), 'Pest has its time');
    assert.ok(states[0].classes.includes('greenline-red'));
    assert.equal(states[1].text, 'Drought');
    assert.ok(states[1].classes.includes('greenline-red'));

    const growth = rowWith(c, 'Growth');
    assert.deepEqual(growth.bar, { fill: '50%', kind: ['greenline-stalled'], pause: true });
    assert.equal(growth.text, '1 d 5 h', 'no minutes when a day or more is left');
  });

  test('jsdom: a growing crop with no problem shows Growing and a green growth bar', async (t) => {
    const data = fixture();
    data.pots['1001'].problems = [];
    data.pots['1001'].badge = '';
    const { doc, core } = await setup(t, { pots: data, rows: [plant(5001, 0, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)] });
    setRemain(core, 5001, 105960);
    const c = card(doc, 1001);

    const state = rowWith(c, 'State');
    assert.equal(state.text, 'Growing');
    assert.ok(!state.classes.includes('greenline-red') && !state.classes.includes('greenline-gold'));
    assert.deepEqual(rowWith(c, 'Growth').bar, { fill: '50%', kind: [], pause: false });
  });

  // The pot data of a growing crop with no problem, as C# sends it: no time to mature.
  function noProblemData() {
    const data = fixture();
    data.pots['1001'].problems = [];
    data.pots['1001'].badge = '';
    data.pots['1001'].growRemainSeconds = 0;
    return data;
  }

  test('jsdom: the growth bar of a crop with no problem follows the countdown of its game row, with no new pot data', async (t) => {
    const { doc, core } = await setup(t, { pots: noProblemData(), rows: [plant(5001, 0, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)] });

    setRemain(core, 5001, 105960);
    let growth = rowWith(card(doc, 1001), 'Growth');
    assert.deepEqual(growth.bar, { fill: '50%', kind: [], pause: false });
    assert.equal(growth.text, '1 d 5 h');

    setRemain(core, 5001, 14 * 3600 + 43 * 60 + 30);
    growth = rowWith(card(doc, 1001), 'Growth');
    assert.deepEqual(growth.bar, { fill: '75%', kind: [], pause: false });
    assert.equal(growth.text, '14 h 43 min');
  });

  for (const status of [0, 3, 5]) {
    test(`jsdom: the growth bar of a crop with a problem in the pot data stays still with a game row of status ${status}`, async (t) => {
      const { doc, core } = await setup(t, { rows: [plant(5001, status, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)] });

      setRemain(core, 5001, 3000);
      const before = rowWith(card(doc, 1001), 'Growth');
      assert.deepEqual(before.bar, { fill: '50%', kind: ['greenline-stalled'], pause: true });
      assert.equal(before.text, '1 d 5 h');

      setRemain(core, 5001, 1000);
      assert.deepEqual(rowWith(card(doc, 1001), 'Growth'), before);
    });
  }

  for (const [name, rows] of [
    ['a game row of status 3', [plant(5001, 3, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)]],
    ['a game row of status 5', [plant(5001, 5, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)]],
    ['no game row', [plant(5002, 1, 3600, 0), plant(5004, 2)]]
  ]) {
    test(`jsdom: a crop with no problem in the pot data but ${name} has no growth bar`, async (t) => {
      const { doc } = await setup(t, { pots: noProblemData(), rows });

      assert.equal(rowsWith(card(doc, 1001), 'Growth').length, 0);
    });
  }

  test('jsdom: the fertilizer row has the icon and name, or a grey None with no icon, then the auto value', async (t) => {
    const { doc } = await setup(t);

    const parts = () => [...hoverNode(doc).querySelectorAll('.greenline-fert-name, .greenline-fert-auto')]
      .map((e) => [e.textContent, e.classList.contains('greenline-muted')]);

    const fert = rowWith(card(doc, 1001), 'Fertilizer');
    assert.equal(fert.icon, '../../Res/Material/UI_Item_Icon_Mat_S_1500310101.png');
    assert.deepEqual(parts(1001), [['Compound Fertilizer', false], ['(auto: on)', false]], 'a set fertilizer and auto on are white');

    const none = rowWith(card(doc, 1002), 'Fertilizer');
    assert.equal(none.icon, null);
    assert.deepEqual(parts(1002), [['None', true], ['(auto: off)', true]], 'None and auto off are grey');
  });

  test('jsdom: the fertilizer row shows the dose of a part dose after the name, and no dose for a full dose', async (t) => {
    const data = fixture();
    data.pots['1001'].fertDose = '1/4';
    const { doc } = await setup(t, { pots: data });

    const parts = () => [...hoverNode(doc).querySelectorAll('.greenline-card-row .greenline-card-value > span')]
      .map((e) => [e.className, e.textContent]).filter(([cls]) => /greenline-fert/.test(cls));

    card(doc, 1001);
    assert.deepEqual(parts(), [
      ['greenline-fert-name', 'Compound Fertilizer'], ['greenline-fert-dose', '1/4'], ['greenline-fert-auto', '(auto: on)']
    ]);
    card(doc, 1002);
    assert.deepEqual(parts().map(([cls]) => cls), ['greenline-fert-name greenline-muted', 'greenline-fert-auto greenline-muted']);
  });

  test('jsdom: a long fertilizer name is cut with an ellipsis, and the dose and the auto value stay whole', async (t) => {
    const data = fixture();
    data.pots['1001'].fertDose = '1/4';
    const { doc, core } = await setup(t, { pots: data });
    card(doc, 1001);
    const style = (sel) => core.getComputedStyle(hoverNode(doc).querySelector(sel));

    assert.equal(style('.greenline-card-rows').gridTemplateColumns, 'auto minmax(0, 1fr)');
    const name = style('.greenline-fert-name');
    assert.deepEqual([name.minWidth, name.overflow, name.textOverflow], ['0px', 'hidden', 'ellipsis']);
    assert.equal(style('.greenline-fert-dose').flexShrink, '0');
    assert.equal(style('.greenline-fert-auto').flexShrink, '0');
  });

  test('jsdom: the Auto-replant row is a white On or a grey Off', async (t) => {
    const { doc } = await setup(t);

    const on = rowWith(card(doc, 1001), 'Auto-replant');
    assert.equal(on.text, 'On');
    assert.ok(!on.classes.includes('greenline-muted'));
    const off = rowWith(card(doc, 1002), 'Auto-replant');
    assert.equal(off.text, 'Off');
    assert.ok(off.classes.includes('greenline-muted'));
  });

  test('jsdom: a ripe crop shows the gold ripe icon and a gold harvest bar with the game word as its key', async (t) => {
    const { doc, core } = await setup(t);
    core.eval("state.i18n_plantHarvest = 'Harvest '");
    setRemain(core, 5002, 45 * 60 + 30);
    await wait(core, 60);
    const c = card(doc, 1002);

    const state = rowWith(c, 'State');
    assert.equal(state.icon, '../../Res/PlantAnomaly/MatureReady.png');
    assert.equal(state.text, 'Mature');
    assert.ok(state.classes.includes('greenline-gold'));
    const harvest = rowWith(c, 'Harvest');
    assert.deepEqual(harvest.bar.kind, ['greenline-ripe']);
    assert.ok(harvest.text.includes('45 min'));
    assert.equal(rowsWith(c, 'Growth').length, 0);
  });

  test('jsdom: the card of a research crop that needs fertilizer has a red State row with the Basic Fertilizer icon and no time', async (t) => {
    const data = researchData(['needFert']);
    data.words = { problemNeedFert: 'Stagnant: Needs Fertilizer' };
    const { doc, core } = await setup(t, { pots: data, rows: [plant(5001, 3, 7200, 0), plant(5002, 8, 3600, 0), plant(5004, 2)] });
    setRemain(core, 5002, 3600 + 5 * 60 + 50);
    const c = card(doc, 1002);

    const state = rowWith(c, 'State');
    assert.equal(state.icon, BASIC_FERT_ICON);
    assert.equal(state.text, 'Stagnant: Needs Fertilizer');
    assert.ok(state.classes.includes('greenline-red'));
    assert.deepEqual(rowWith(c, 'Growth').bar, { fill: '50%', kind: ['greenline-stalled'], pause: true });
  });

  test('jsdom: the card of a research crop with Pest has a red Pest State row with no time', async (t) => {
    const { doc, core } = await setup(t, { pots: researchData(['pest']), rows: [plant(5001, 3, 7200, 0), plant(5002, 9, 3600, 0), plant(5004, 2)] });
    setRemain(core, 5002, 3600 + 5 * 60 + 50);
    const c = card(doc, 1002);

    const state = rowWith(c, 'State');
    assert.equal(state.text, 'Pest');
    assert.ok(state.classes.includes('greenline-red'));
  });

  test('jsdom: a mature crop with no harvest window has the gold border and the gold State row, and no countdown or harvest bar', async (t) => {
    const data = fixture();
    data.pots['1002'].neverWithers = true;
    const { doc, core, root } = await setup(t, { pots: data, rows: [plant(5001, 3, 7200, 0), plant(5002, 1, 0, 0), plant(5004, 2)] });
    core.eval("state.i18n_plantHarvest = 'Harvest '");
    setRemain(core, 5002, 45 * 60 + 30);
    root.__greenline.step();

    assert.ok(cell(doc, 1002).classList.contains('greenline-mature'));
    assert.equal(timeText(doc, 1002), null);
    const c = card(doc, 1002);
    assert.ok(rowWith(c, 'State').classes.includes('greenline-gold'));
    assert.equal(rowsWith(c, 'Harvest').length, 0);
  });

  test('jsdom: the card of an empty pot has the pot header, no State row, and no growth bar', async (t) => {
    const { doc } = await setup(t);
    const c = card(doc, 1003);

    assert.deepEqual(c.head, { icon: '../../Res/Furniture/UI_Item_Icon_plantcommon_small.png', name: 'Small Planter', chip: ['s1', 1], sub: null, subIcon: null, subOrder: null, subChip: null });
    assert.deepEqual(c.rows.map((r) => [r.key, r.text]), [['Fertilizer', 'None(auto: on)'], ['Auto-replant', 'On']]);
  });

  test('jsdom: a Poor pot shows its state with the Till mark, and a medium pot has the s2 chip', async (t) => {
    const { doc } = await setup(t);
    const c = card(doc, 1005);

    assert.deepEqual(c.head.chip, ['s2', 2]);
    const state = rowWith(c, 'State');
    assert.equal(state.text, 'Poor');
    assert.ok(state.icon);
  });

  test('jsdom: a withered crop shows Withered and no growth bar', async (t) => {
    const { doc } = await setup(t);
    const c = card(doc, 1004);

    assert.equal(rowWith(c, 'State').text, 'Withered');
    assert.equal(c.rows.filter((r) => r.bar).length, 0);
  });

  test('jsdom: the Chinese card uses the words of the pot data', async (t) => {
    const data = fixture();
    data.lang = 'zh';
    data.words = { labelFertilizer: '肥料', valueNone: '无', autoOff: '（自动：关）', labelState: '状态', stateMature: '成熟' };
    const { doc } = await setup(t, { pots: data });

    assert.equal(rowWith(card(doc, 1002), '肥料').text, '无（自动：关）', 'the two parts sit side by side');
    assert.equal(rowWith(card(doc, 1002), '状态').text, '成熟');
  });

  async function clickCell(doc, core, root, potId) {
    const received = [];
    const listener = (e) => received.push(e.data);
    root.addEventListener('message', listener);
    cell(doc, potId).dispatchEvent(new core.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);
    root.removeEventListener('message', listener);
    return received;
  }

  test('jsdom: a click on a crop cell posts the row click of the game with the click id of the pot', async (t) => {
    const { doc, core, root } = await setup(t);

    const received = await clickCell(doc, core, root, 1001);

    assert.equal(received.length, 1);
    assert.equal(received[0].type, 'PLANT_MATURE_CLICK');
    assert.equal(received[0].sourcePageId, 'CoreUI1');
    assert.deepEqual({ ...received[0].data }, { instanceId: 5001 });
  });

  test('jsdom: a click on an empty or Poor pot cell also posts the row click, with its click id', async (t) => {
    const { doc, core, root } = await setup(t);

    const empty = await clickCell(doc, core, root, 1003);
    const poor = await clickCell(doc, core, root, 1005);

    assert.deepEqual(empty.map((m) => [m.type, m.data.instanceId]), [['PLANT_MATURE_CLICK', 5003]]);
    assert.deepEqual(poor.map((m) => [m.type, m.data.instanceId]), [['PLANT_MATURE_CLICK', 5005]]);
  });

  function assertGridGone(doc, core) {
    assert.equal(doc.querySelectorAll('.greenline-cell, .greenline-floor-label, .greenline-grid').length, 0);
    for (const row of doc.querySelectorAll('.mature-item-row')) assert.notEqual(core.getComputedStyle(row).display, 'none');
  }

  test('jsdom: with no pot data the grid is removed and the game rows show again', async (t) => {
    const { doc, core, root } = await setup(t);

    assert.equal(setPots(root, null), 'installed');
    assertGridGone(doc, core);
  });

  test('jsdom: a missing part turns the grid off and install() names it', async (t) => {
    const { doc, core, root } = await setup(t);

    // Simulates a game update that renamed the class of the game rows.
    for (const el of doc.querySelectorAll('.mature-item-row')) el.classList.remove('mature-item-row');

    const result = runPageJs(root, 'window.__greenline.install()');
    assert.match(result, /missing: grid\(\.mature-item-row\)/, `expected a missing-part report, got "${result}"`);
    assertGridGone(doc, core);
  });

  // The plant list message with the game's own Patrol values, as the header reads them.
  async function postPatrol(coreWindow, unlocked, replantOn) {
    coreWindow.postMessage({
      type: 'WebUI_CoreUI1_PlantMatureMsg',
      data: { patrolUnlocked: unlocked, replantOn: replantOn }
    }, '*');
    await wait(coreWindow, 30);
  }

  function tendButton(doc) { return doc.querySelector('.mature-toggle-row .greenline-tend-btn'); }

  test('jsdom: a Tend All button shows before the chevron while a chore waits', async (t) => {
    const { doc } = await setup(t);

    const btn = tendButton(doc);
    assert.ok(btn, 'no Tend All button');
    assert.equal(btn.textContent, 'Tend All');
    assert.ok(btn.nextElementSibling.classList.contains('mature-toggle-chevron'));
    assert.ok(btn.hasAttribute('data-interactive'));
  });

  test('jsdom: no Tend All button with no chore or with no Patrol', async (t) => {
    const { doc, root } = await setup(t);

    setPots(root, Object.assign(fixture(), { pendingChores: 0 }));
    assert.equal(tendButton(doc), null);
    setPots(root, Object.assign(fixture(), { patrol: false }));
    assert.equal(tendButton(doc), null);
  });

  test('jsdom: a Tend All click posts the game Patrol request and does not toggle the list', async (t) => {
    const { doc, core, root } = await setup(t);
    const received = [];
    root.addEventListener('message', (e) => received.push(e.data));

    tendButton(doc).dispatchEvent(new core.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);

    assert.deepEqual(received.map((m) => [m.type, m.sourcePageId, JSON.stringify({ ...m.data })]), [['PLANT_CHORE_BATCH', 'CoreUI1', '{}']]);
    assert.equal(core.eval('state.maturePlantsExpanded'), true);
  });

  test('jsdom: the game header button and checkbox are hidden while the mod shows Patrol', async (t) => {
    const { doc, core, root } = await setup(t);
    await postPatrol(core, true, false);
    setPots(root, fixture());

    for (const sel of ['.mature-patrol-btn', '.mature-replant-chk']) {
      const el = doc.querySelector(sel);
      assert.ok(el, `${sel} not rendered by the game`);
      assert.equal(core.getComputedStyle(el).display, 'none', `${sel} still shows`);
    }
  });

  function replantLine(doc) { return doc.querySelector('.mature-popover .greenline-replant'); }

  test('jsdom: the Auto-replant line is the last grid line and shows the game value', async (t) => {
    const { doc, core, root } = await setup(t);
    await postPatrol(core, true, true);
    await wait(core, 30);
    root.__greenline.step();

    const line = replantLine(doc);
    assert.ok(line, 'no Auto-replant line');
    assert.deepEqual(gridPlace(line), ['3', '1 / -1']);
    assert.ok(line.hasAttribute('data-interactive'));
    assert.ok(line.textContent.includes('Auto-replant'));
    assert.equal(line.querySelector('input[type=checkbox]').checked, true);
    assert.ok(line.classList.contains('has-entry-tip'));
    assert.ok(line.querySelector('.entry-tip').textContent.includes('plants the same crop again'));

    await postPatrol(core, true, false);
    await wait(core, 30);
    root.__greenline.step();
    assert.equal(line.querySelector('input[type=checkbox]').checked, false);
  });

  test('jsdom: an Auto-replant click posts the game Replant toggle', async (t) => {
    const { doc, core, root } = await setup(t);
    const received = [];
    root.addEventListener('message', (e) => received.push(e.data));

    replantLine(doc).querySelector('input[type=checkbox]').click();
    await wait(root, 30);

    assert.deepEqual(received.map((m) => [m.type, JSON.stringify({ ...m.data })]), [['PLANT_CHORE_REPLANT_TOGGLE', '{"on":true}']]);
  });

  test('jsdom: no Auto-replant line with no Patrol', async (t) => {
    const { doc } = await setup(t, { pots: Object.assign(fixture(), { patrol: false }) });

    assert.equal(replantLine(doc), null);
  });

  test('jsdom: a missing chevron turns off only the Tend All button', async (t) => {
    const { doc, root } = await setup(t);
    doc.querySelector('.mature-toggle-chevron').classList.remove('mature-toggle-chevron');

    const result = runPageJs(root, 'window.__greenline.install()');

    assert.match(result, /missing: tend\(\.mature-toggle-chevron\)/, result);
    assert.equal(tendButton(doc), null);
    assert.ok(doc.querySelector('.greenline-grid'), 'the grid still shows');
  });

  test('jsdom: the English defaults hold each word that the pot data does not give', async (t) => {
    const { root } = await setup(t);

    assert.equal(runPageJs(root, 'window.__greenline.word("stateGrowing")'), 'Growing');
    assert.equal(runPageJs(root, 'window.__greenline.word("problemDrought")'), 'Drought');
    assert.equal(runPageJs(root, 'window.__greenline.word("problemNeedFert")'), 'Needs Fertilizer');
    assert.equal(runPageJs(root, 'window.__greenline.word("labelFertilizer")'), 'Fertilizer');
    setPots(root, Object.assign(fixture(), { words: { problemDrought: 'Dry' } }));
    assert.equal(runPageJs(root, 'window.__greenline.word("problemDrought")'), 'Dry');
    assert.equal(runPageJs(root, 'window.__greenline.word("stateGrowing")'), 'Growing');
  });

  // The world hover card: C# sends the pot under the pointer in the game world.
  function setHover(root, potId, x, y) {
    const args = [potId].concat(x === undefined ? [] : [x, y]).map((a) => JSON.stringify(a)).join(',');
    return runPageJs(root, 'window.__greenline.setHover(' + args + ')');
  }

  function hoverNode(doc) {
    const nodes = doc.querySelectorAll('.greenline-hover');
    assert.ok(nodes.length <= 1, 'at most one hover node');
    return nodes[0] || null;
  }

  test('jsdom: setHover shows the card of the pot in one node of the frame body, the same as the grid card, and takes no pointer', async (t) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001);

    const node = hoverNode(doc);
    assert.ok(node, 'no hover node');
    assert.equal(node.parentNode, doc.body);
    assert.ok(!node.hidden);
    assert.equal(core.getComputedStyle(node).pointerEvents, 'none');
    assert.deepEqual(readCard(node), card(doc, 1001));
  });

  test('jsdom: setHover hides the card for no pot and for an unknown pot', async (t) => {
    const { doc, root } = await setup(t);

    setHover(root, 1001);
    setHover(root, 0);
    assert.ok(hoverNode(doc).hidden, 'no pot');
    setHover(root, 1001);
    assert.ok(!hoverNode(doc).hidden);
    setHover(root, 424242);
    assert.ok(hoverNode(doc).hidden, 'unknown pot');
  });

  test('jsdom: setHover of the same pot twice writes nothing to the DOM', async (t) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001);
    await wait(core, 60);

    const records = [];
    const observer = new core.MutationObserver((list) => records.push(...list));
    observer.observe(hoverNode(doc), { childList: true, subtree: true, attributes: true, characterData: true });
    setHover(root, 1001);
    await wait(core, 30);
    observer.disconnect();

    assert.equal(records.length, 0, `unexpected writes: ${records.map((r) => r.type + ' ' + r.attributeName).join(', ')}`);
  });

  function movePointer(core, x, y, target) {
    const el = target || core.document.body;
    el.dispatchEvent(new core.MouseEvent('mousemove', { bubbles: true, clientX: x, clientY: y }));
  }

  test('jsdom: the world card sits 12px right of and below the pointer, and on the other side at the window edge', async (t) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001);

    movePointer(core, 100, 200);
    const node = hoverNode(doc);
    assert.deepEqual([node.style.left, node.style.top], ['112px', '212px']);

    // jsdom has no layout, so the card has no size: the edge is the window edge itself.
    movePointer(core, core.innerWidth - 10, core.innerHeight - 10);
    assert.deepEqual([node.style.left, node.style.top], [(core.innerWidth - 22) + 'px', (core.innerHeight - 22) + 'px']);
  });

  test('jsdom: the world card hides while the pointer is on the HUD or out of the frame, and shows again over the world', async (t) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001);
    movePointer(core, 100, 200);
    const node = hoverNode(doc);

    const hud = doc.querySelector('[data-interactive]');
    assert.ok(hud, 'CoreUI1 has a HUD part with data-interactive');
    movePointer(core, 110, 210, hud);
    assert.ok(node.hidden, 'on the HUD');
    movePointer(core, 120, 220);
    assert.ok(!node.hidden, 'over the world');

    // A game window is another frame: the pointer leaves CoreUI1 with no element to go to.
    doc.body.dispatchEvent(new core.MouseEvent('mouseout', { bubbles: true, relatedTarget: null }));
    assert.ok(node.hidden, 'out of the frame');
    movePointer(core, 130, 230);
    assert.ok(!node.hidden, 'back over the world');
  });

  // The page gets no pointer moves over the game world (the web view passes them to the game), so C#
  // sends the pointer place with the pot, as fractions of the screen from the top-left corner.
  test('jsdom: the world card sits 12px right of and below the pointer place that C# sends', async (t) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001, 0.25, 0.5);

    const node = hoverNode(doc);
    assert.deepEqual([node.style.left, node.style.top], [(core.innerWidth / 4 + 12) + 'px', (core.innerHeight / 2 + 12) + 'px']);
  });

  // A game window or menu is another frame over CoreUI1: the pointer leaves CoreUI1, and the page gets no
  // move when it comes back over the world. Each place from C# checks again what is under the pointer.
  test('jsdom: after a game window closes, the next pointer place from C# shows the world card again', async (t) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001, 0.25, 0.5);
    doc.body.dispatchEvent(new core.MouseEvent('mouseout', { bubbles: true, relatedTarget: null }));
    assert.ok(hoverNode(doc).hidden, 'the window is open');

    setHover(root, 1001, 0.3, 0.5);
    assert.ok(!hoverNode(doc).hidden, 'the window closed, the pointer is over the world');
  });

  test('jsdom: the world card hides when the pointer place from C# is on another frame or on a HUD part', async (t) => {
    const { doc, core, root } = await setup(t);
    const window = { tagName: 'IFRAME', contentWindow: {} };
    root.document.elementFromPoint = () => window;
    setHover(root, 1001, 0.25, 0.5);
    assert.ok(!hoverNode(doc) || hoverNode(doc).hidden, 'a game window is at the place');

    root.document.elementFromPoint = () => ({ tagName: 'IFRAME', contentWindow: core });
    doc.elementFromPoint = () => doc.querySelector('[data-interactive]');
    setHover(root, 1001, 0.3, 0.5);
    assert.ok(!hoverNode(doc) || hoverNode(doc).hidden, 'a HUD part is at the place');

    doc.elementFromPoint = () => doc.body;
    setHover(root, 1001, 0.35, 0.5);
    assert.ok(!hoverNode(doc).hidden, 'the world is at the place');
  });

  test('jsdom: setHover returns an error as text and does not throw', async (t) => {
    const { core, root } = await setup(t);
    core.eval = () => { throw new Error('broken page'); };

    assert.match(setHover(root, 1001, 0.25, 0.5), /^error: .*broken page/);
  });

  // One card shows at a time, so the frame keeps one pot card node, and the frame step builds only it.
  test('jsdom: the frame keeps one pot card node for the grid and the world', async (t) => {
    const { doc, core, root } = await setup(t);
    await wait(core, 60);
    assert.equal(doc.querySelectorAll('.greenline-pot-card').length, 0, 'no card before a hover');

    assert.equal(card(doc, 1001).head.name, 'Watermelon');
    assert.equal(card(doc, 1002).head.name, 'Potato');
    setHover(root, 1001, 0.25, 0.5);
    await wait(core, 30);

    const cards = doc.querySelectorAll('.greenline-pot-card');
    assert.equal(cards.length, 1);
    assert.equal(cards[0].parentNode, hoverNode(doc));
    assert.equal(readCard(hoverNode(doc)).head.name, 'Watermelon');
    assert.equal(doc.querySelectorAll('.greenline-cell .entry-tip').length, 0, 'no card in a cell');
  });

  test('jsdom: the grid card follows the pointer over the cell and hides when the pointer moves to another HUD part', async (t) => {
    const { doc, core } = await setup(t);
    hoverCell(doc, 1001, 100, 200);
    const node = hoverNode(doc);
    assert.deepEqual([node.style.left, node.style.top], ['112px', '212px']);
    hoverCell(doc, 1001, 110, 205);
    assert.deepEqual([node.style.left, node.style.top], ['122px', '217px']);

    movePointer(core, 120, 210, doc.querySelector('[data-interactive]:not(.greenline-cell)'));
    assert.ok(node.hidden);
  });

  // The game passes pointer moves to the page only over the HUD parts, so a fast move from a cell to
  // the world gives the page no move; the next pointer place from C# hides the card of the cell.
  test('jsdom: a pointer place from C# over the world hides the card of the cell, or shows the hovered world pot', async (t) => {
    const { doc, root } = await setup(t);
    hoverCell(doc, 1001);
    doc.elementFromPoint = () => doc.body;

    setHover(root, 0, 0.5, 0.5);
    assert.ok(hoverNode(doc).hidden, 'no pot under the pointer');
    hoverCell(doc, 1001);
    setHover(root, 1002, 0.5, 0.5);
    assert.ok(!hoverNode(doc).hidden);
    assert.equal(readCard(hoverNode(doc)).head.name, 'Potato');

    doc.elementFromPoint = () => cell(doc, 1001);
    setHover(root, 0, 0.1, 0.1);
    assert.equal(readCard(hoverNode(doc)).head.name, 'Watermelon', 'the place is on a cell');
  });

  test('jsdom: the card stays inside the window at each edge', async (t) => {
    const { doc, core } = await setup(t);
    hoverCell(doc, 1001, 100, 100);
    const node = hoverNode(doc);
    Object.defineProperty(node, 'offsetWidth', { value: core.innerWidth - 50 });
    Object.defineProperty(node, 'offsetHeight', { value: core.innerHeight - 50 });

    hoverCell(doc, 1001, 100, 100);
    assert.deepEqual([node.style.left, node.style.top], ['0px', '0px']);
  });

  // CoreUI1 sends the places of its HUD parts to the game after each change of style or class; the card
  // moves with the pointer, so it opts out with the game's own marker.
  test('jsdom: the card node opts out of the game sync of the HUD places', async (t) => {
    const { doc } = await setup(t);
    hoverCell(doc, 1001);
    assert.ok(hoverNode(doc).hasAttribute('data-no-sync'));
  });
}
