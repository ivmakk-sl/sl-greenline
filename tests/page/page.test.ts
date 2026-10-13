// Runs page.js against the game's own CoreUI1.html, so a game update that renames or removes a page
// part page.js depends on shows up here instead of only in the game. The pot data is the fixture that
// the xUnit test of GridLogic.ToJson compares with, so both sides of the JSON contract use one file.
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
const CORE_UI1_HTML = path.join(GAME_DIR, 'SurvivalLog_Data', 'StreamingAssets', 'WebUI', 'UI', 'CoreUI1', 'CoreUI1.html');
const SRC_DIR = path.join(HERE, '..', '..', 'src', 'Web');
// The behavior tests run the Vite bundle (npm test builds it first); the static test reads the FEATURES
// table from the source, because Vite writes each string in double quotes.
const PAGE_SOURCE_PATH = path.join(SRC_DIR, 'page', 'core.ts');
const PAGE_BUNDLE_PATH = path.join(HERE, '..', '..', 'obj', 'page', 'page.js');
const FIXTURE_PATH = path.join(HERE, '..', 'fixtures', 'pots.json');
const EN_WORDS_PATH = path.join(SRC_DIR, '..', 'i18n', 'en.json');

const gameFileExists = fs.existsSync(CORE_UI1_HTML);

if (!gameFileExists) {
  test.skip('page.js against the game page: ' + `game files not found under SL_GAME_DIR (${GAME_DIR}); set SL_GAME_DIR to the game folder`, () => {});
} else {
  const pageSource = fs.readFileSync(PAGE_SOURCE_PATH, 'utf8');
  const pageJs = fs.readFileSync(PAGE_BUNDLE_PATH, 'utf8');
  // The CSS that C# hands to page.js (PageScript.Css): the tokens, then the page rules.
  const pageCss = ['tokens.css', 'page.css'].map((f: Any) => fs.readFileSync(path.join(SRC_DIR, f), 'utf8')).join('\n');
  const coreUi1Html = fs.readFileSync(CORE_UI1_HTML, 'utf8');

  // The pot data with each English mod text, as C# sends it: the page has no word of its own. The fixture file is
  // the JSON contract of GridLogic.ToJson, which holds only two words.
  function fixture() {
    const data = JSON.parse(fs.readFileSync(FIXTURE_PATH, 'utf8'));
    data.words = JSON.parse(fs.readFileSync(EN_WORDS_PATH, 'utf8'));
    return data;
  }

  // Pulls the page part names out of the FEATURES table of page.js, so this test never copies the names.
  function featureNames() {
    const table = pageSource.match(/export const FEATURES = \{([\s\S]*?)\};/);
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
      const present = name.replace(/^[#.]/, '').split('.').every((piece: Any) => coreUi1Html.includes(piece));
      assert.ok(present, `page part "${name}" not found in CoreUI1.html`);
    }
  });

  // CoreUI1.html starts its own requestAnimationFrame loops, so each window is closed after its test.
  async function loadCoreWindow(t?: Any) {
    const dom = new JSDOM(coreUi1Html, {
      url: url.pathToFileURL(CORE_UI1_HTML).href,
      runScripts: 'dangerously',
      resources: 'usable',
      pretendToBeVisual: true
    });
    t.onTestFinished(() => dom.window.close());
    await new Promise((resolve: Any, reject: Any) => {
      dom.window.addEventListener('load', resolve);
      setTimeout(() => reject(new Error('CoreUI1.html did not fire load within 5s')), 5000);
    });
    return dom.window as Any;
  }

  // A blank window that stands in for the root page (Root.html), which holds the CoreUI1 iframe.
  function makeRootWindow(t?: Any, coreWindow?: Any) {
    const root = new JSDOM('<!doctype html><html><body></body></html>', {
      url: 'file:///Root.html',
      pretendToBeVisual: true
    }).window as Any;
    t.onTestFinished(() => root.close());
    const frameBox = { current: coreWindow || null };
    const originalQSA = root.document.querySelectorAll.bind(root.document);
    root.document.querySelectorAll = (selector: Any) =>
      selector === 'iframe' ? (frameBox.current ? [{ contentWindow: frameBox.current }] : []) : originalQSA(selector);
    vm.createContext(root);
    return root;
  }

  function runPageJs(root?: Any, callExpr?: Any) {
    const css = 'window.__greenlineCss=' + JSON.stringify(pageCss) + ';';
    return vm.runInContext(css + pageJs + ';' + callExpr, root, { filename: 'page.js' });
  }

  function setPots(root?: Any, data?: Any) {
    return runPageJs(root, 'window.__greenline.setPots(' + JSON.stringify(data) + ')');
  }

  function wait(win?: Any, ms?: Any) {
    return new Promise((resolve: Any) => win.setTimeout(resolve, ms));
  }

  // The plant list message that the game sends (WebUI_CoreUI1_PlantMatureMsg).
  async function postPlants(coreWindow?: Any, plants?: Any) {
    coreWindow.postMessage({
      type: 'WebUI_CoreUI1_PlantMatureMsg',
      data: { containerActive: true, plants }
    }, '*');
    await wait(coreWindow, 30);
  }

  function plant(rowId?: Any, status?: Any, totalSeconds?: Any, elapsedSeconds?: Any) {
    return {
      showInstanceId: rowId, plantName: 'Crop ' + rowId, status,
      harvestTotalSeconds: totalSeconds || 0, harvestElapsedSeconds: elapsedSeconds || 0, dispatchUtcMs: 0
    };
  }

  // The game rows of the fixture pots: 5001 growing with Pest (status 3), 5002 mature, 5004 withered.
  const FIXTURE_ROWS = [plant(5001, 3, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)];

  // Waits until Vue's enter transition of the popover ends, so its class writes are not counted as
  // writes of the mod.
  async function expand(coreWindow?: Any) {
    coreWindow.eval('state.maturePlantsExpanded = true');
    for (let i = 0; i < 50; i++) {
      await wait(coreWindow, 20);
      const popover = coreWindow.document.querySelector('.mature-popover');
      if (popover && !/mature-popover-enter/.test(popover.className)) return;
    }
    throw new Error('the popover enter transition did not end within 1s');
  }

  async function setup(t?: Any, opts?: Any) {
    opts = opts || {};
    const core = await loadCoreWindow(t);
    await postPlants(core, opts.rows || FIXTURE_ROWS);
    if (opts.expanded !== false) await expand(core);
    const root = makeRootWindow(t, core);
    const result = setPots(root, opts.pots || fixture());
    return { core, root, result, doc: core.document };
  }

  function cell(doc?: Any, potId?: Any) {
    return doc.querySelector('.greenline-cell[data-pot-id="' + potId + '"]');
  }

  function gridPlace(el?: Any) {
    return [el.style.getPropertyValue('grid-row'), el.style.getPropertyValue('grid-column')];
  }

  test('jsdom: pot data turns the popover into a grid and hides the game rows', async (t: Any) => {
    const { doc, core, result } = await setup(t);
    assert.equal(result, 'installed');

    const popover = doc.querySelector('.mature-popover');
    assert.ok(popover.classList.contains('greenline-grid'));
    const rows = doc.querySelectorAll('.mature-item-row');
    assert.equal(rows.length, 3);
    for (const row of rows) assert.equal(core.getComputedStyle(row).display, 'none');
  });

  test('jsdom: the style node of the frame holds the CSS that C# hands to the script', async (t: Any) => {
    const { doc } = await setup(t);

    const styles = doc.querySelectorAll('#greenline-style');
    assert.equal(styles.length, 1);
    assert.equal(styles[0].textContent, pageCss);
  });

  // The selectors of each page.css rule whose value uses the token.
  function selectorsUsing(token?: Any) {
    const css = fs.readFileSync(path.join(SRC_DIR, 'page.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
    const out = [];
    for (const m of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
      if (m[2].includes('var(' + token + ')')) out.push(...m[1].split(',').map((s: Any) => s.trim()));
    }
    return out;
  }

  test('jsdom: the background opacity rules reach the header, the grid, and the pot card of the game page', async (t: Any) => {
    const { doc } = await setup(t);
    hoverCell(doc, 1001);

    const panel = selectorsUsing('--gl-panel-opacity');
    const cardRule = selectorsUsing('--gl-card-opacity');
    assert.deepEqual([panel.length, cardRule.length], [2, 2]);
    assert.ok(doc.querySelector(panel[0]).classList.contains('mature-toggle-row'), panel[0]);
    assert.ok(doc.querySelector(panel[1]).classList.contains('mature-popover'), panel[1]);
    const potCard = doc.querySelector('.greenline-pot-card');
    assert.ok(potCard && potCard.matches(cardRule[0]), cardRule[0]);
    // The arrow of the card has the same background.
    assert.equal(cardRule[1], '.greenline-hover::before');
    assert.ok(doc.querySelector('.greenline-hover').matches(cardRule[1].replace('::before', '')));
  });

  test('jsdom: each floor has a label in column 1 that spans its lines', async (t: Any) => {
    const { doc } = await setup(t);

    const upper = doc.querySelector('.greenline-floor-label[data-floor-id="102"]');
    const home = doc.querySelector('.greenline-floor-label[data-floor-id="1"]');
    assert.equal(upper.textContent, '2F');
    assert.equal(home.textContent, 'Home');
    assert.deepEqual(gridPlace(upper), ['1 / span 1', '1']);
    assert.deepEqual(gridPlace(home), ['2 / span 1', '1']);
  });

  test('jsdom: each pot has one cell, placed by its floor and cell index', async (t: Any) => {
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

  test('jsdom: a floor with 8 pots has 6 cells on its first line and 2 on the next', async (t: Any) => {
    const { doc } = await setup(t, { pots: eightPotFloor() });

    assert.deepEqual(gridPlace(doc.querySelector('.greenline-floor-label[data-floor-id="1"]')), ['2 / span 2', '1']);
    assert.deepEqual(gridPlace(cell(doc, 2005)), ['2', '7']);
    assert.deepEqual(gridPlace(cell(doc, 2006)), ['3', '2']);
    assert.deepEqual(gridPlace(cell(doc, 2007)), ['3', '3']);
  });

  test('jsdom: the popover has cell columns for the longest line only', async (t: Any) => {
    const small = await setup(t);
    assert.match(small.doc.querySelector('.mature-popover').style.getPropertyValue('grid-template-columns'), /repeat\(3,/);

    const big = await setup(t, { pots: eightPotFloor() });
    assert.match(big.doc.querySelector('.mature-popover').style.getPropertyValue('grid-template-columns'), /repeat\(6,/);
  });

  test('jsdom: a second setPots with the same data writes nothing to the DOM', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    // The first frames write the first countdown texts.
    await wait(core, 60);

    const records: Any[] = [];
    const observer = new core.MutationObserver((list: Any) => records.push(...list));
    observer.observe(doc.documentElement, { childList: true, subtree: true, attributes: true, attributeOldValue: true, characterData: true });
    setPots(root, fixture());
    await wait(core, 30);
    observer.disconnect();

    // The game's own countdown writes the text of its rows on each frame; only the mod's writes count.
    const ours = records.filter((r: Any) => {
      const el = r.target.nodeType === 1 ? r.target : r.target.parentElement;
      return !(el && el.closest('.mature-item-row'));
    });
    assert.equal(ours.length, 0, `unexpected writes: ${ours.map((r: Any) => r.type + ' ' + r.attributeName + ' old=' + r.oldValue + ' ' + (r.target.className || r.target.nodeName)).join(', ')}`);
  });

  async function collapse(coreWindow?: Any) {
    coreWindow.eval('state.maturePlantsExpanded = false');
    for (let i = 0; i < 50; i++) {
      await wait(coreWindow, 20);
      if (!coreWindow.document.querySelector('.mature-popover')) return;
    }
    throw new Error('the popover leave transition did not end within 1s');
  }

  function cellPlaces(doc?: Any) {
    const places: Record<string, Any> = {};
    for (const c of doc.querySelectorAll('.greenline-cell')) {
      const id = c.getAttribute('data-pot-id');
      assert.equal(places[id], undefined, `pot ${id} has more than one cell`);
      places[id] = gridPlace(c);
    }
    return places;
  }

  test('jsdom: a collapse and an expand bring the grid back', async (t: Any) => {
    const { doc, core } = await setup(t);
    const before = cellPlaces(doc);

    await collapse(core);
    await expand(core);

    assert.ok(doc.querySelector('.mature-popover').classList.contains('greenline-grid'));
    assert.deepEqual(cellPlaces(doc), before);
    assert.equal(doc.querySelectorAll('.greenline-floor-label').length, 2);
  });

  test('jsdom: a row added or removed by the game keeps each cell in its place', async (t: Any) => {
    const { doc, core } = await setup(t);
    const before = cellPlaces(doc);

    await postPlants(core, [plant(5001, 3, 7200, 0), plant(5004, 2)]);
    assert.deepEqual(cellPlaces(doc), before);
    await postPlants(core, [plant(5001, 3, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2), plant(5009, 0, 9000, 0)]);
    assert.deepEqual(cellPlaces(doc), before);
  });

  test('jsdom: when the game list goes away, no mod node stays', async (t: Any) => {
    const { doc, core } = await setup(t);

    await postPlants(core, []);

    assert.equal(doc.querySelector('.mature-panel'), null);
    assert.equal(doc.querySelectorAll('.greenline-cell, .greenline-floor-label').length, 0);
  });

  test('jsdom: a countdown text write of the game triggers no work of the observer', async (t: Any) => {
    const { core } = await setup(t);
    const originalEval = core.eval.bind(core);
    let stateReads = 0;
    core.eval = (code: Any) => { if (code === 'state') stateReads++; return originalEval(code); };

    // The withered row: the game's own countdown loop does not rewrite its text.
    originalEval('state.maturePlants.find((p) => p.status === 2).remainText = "9:99"');
    await wait(core, 50);

    const texts = [...core.document.querySelectorAll('.mature-item-row .mature-item-remain')].map((e: Any) => e.textContent);
    assert.ok(texts.includes('9:99'), `the game did not write the text: ${texts}`);
    assert.equal(stateReads, 0);
  });

  function iconSrc(el?: Any, selector?: Any) {
    const img = el.querySelector(selector);
    return img ? img.getAttribute('src') : null;
  }

  test('jsdom: a crop cell has the crop icon, and an empty or Poor pot cell has the pot icon', async (t: Any) => {
    const { doc } = await setup(t);
    const pots = fixture().pots;

    assert.equal(iconSrc(cell(doc, 1001), '.greenline-icon'), pots['1001'].cropIcon);
    assert.equal(iconSrc(cell(doc, 1003), '.greenline-icon'), pots['1003'].potIcon);
    assert.equal(iconSrc(cell(doc, 1005), '.greenline-icon'), pots['1005'].potIcon);
    assert.equal(cell(doc, 1003).querySelector('.greenline-till'), null);
    assert.ok(iconSrc(cell(doc, 1005), '.greenline-till'), 'a Poor pot cell has the Till mark');
    assert.equal(cell(doc, 1001).querySelector('.greenline-till'), null);
  });

  test('jsdom: a cell with a badge has the game icon of that problem', async (t: Any) => {
    const { doc } = await setup(t);

    assert.equal(iconSrc(cell(doc, 1001), '.greenline-badge'), '../../Res/PlantAnomaly/Pest.png');
    assert.equal(iconSrc(cell(doc, 1004), '.greenline-badge'), '../../Res/PlantAnomaly/Withered.png');
    assert.equal(cell(doc, 1002).querySelector('.greenline-badge'), null);
    assert.equal(cell(doc, 1003).querySelector('.greenline-badge'), null);
  });

  test('jsdom: only a mature cell has the gold border, and only a Pest or Frost cell is urgent', async (t: Any) => {
    const { doc } = await setup(t);

    const mature = [...doc.querySelectorAll('.greenline-cell.greenline-mature')].map((c: Any) => c.getAttribute('data-pot-id'));
    const urgent = [...doc.querySelectorAll('.greenline-cell.greenline-urgent')].map((c: Any) => c.getAttribute('data-pot-id'));
    assert.deepEqual(mature, ['1002']);
    assert.deepEqual(urgent, ['1001']);
  });

  test('jsdom: a Frost crop is urgent, and a crop with Drought only is not', async (t: Any) => {
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

  test('jsdom: a growing crop with a problem that only stops its growth has the red border, not the pulse', async (t: Any) => {
    const data = fixture();
    data.pots['1002'].state = 'growing';
    data.pots['1002'].problems = ['weed'];
    data.pots['1002'].badge = 'weed';
    const { doc } = await setup(t, { pots: data });

    assert.ok(cell(doc, 1002).classList.contains('greenline-problem'));
    assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
    const problem = [...doc.querySelectorAll('.greenline-cell.greenline-problem')].map((c: Any) => c.getAttribute('data-pot-id'));
    assert.deepEqual(problem, ['1002'], 'a Pest crop, an empty pot, a withered crop, and a Poor pot have no problem border');
  });

  test('jsdom: a growing crop that only stops its growth shows the pause mark in place of the countdown', async (t: Any) => {
    const data = fixture();
    data.words = { ...data.words, stateStalled: 'Stagnant' };
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
  const BADGE_ICONS_OF: Record<string, string> = { pest: '../../Res/PlantAnomaly/Pest.png', frost: '../../Res/PlantAnomaly/Frost.png' };

  // A growing research crop in pot 1002 with its problems, and its game row with the status.
  function researchData(problems?: Any) {
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

  test('jsdom: a research crop that needs fertilizer has the Basic Fertilizer badge, the pause mark, and the red border', async (t: Any) => {
    const { doc, core } = await setup(t, { pots: researchData(['needFert']), rows: [plant(5001, 3, 7200, 0), plant(5002, 8, 3600, 0), plant(5004, 2)] });
    await wait(core, 60);

    assert.equal(iconSrc(cell(doc, 1002), '.greenline-badge'), BASIC_FERT_ICON);
    assert.equal(cell(doc, 1002).querySelectorAll(':scope > .greenline-pause i').length, 2);
    assert.equal(timeText(doc, 1002), null);
    assert.ok(cell(doc, 1002).classList.contains('greenline-problem'));
    assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
  });

  for (const [problem, status] of [['pest', 9], ['frost', 10]]) {
    test(`jsdom: a research crop with ${problem} has its badge, the pause mark, and the red border, and is not urgent`, async (t: Any) => {
      const { doc, core } = await setup(t, { pots: researchData([problem]), rows: [plant(5001, 3, 7200, 0), plant(5002, status, 3600, 0), plant(5004, 2)] });
      await wait(core, 60);

      assert.equal(iconSrc(cell(doc, 1002), '.greenline-badge'), BADGE_ICONS_OF[problem as string]);
      assert.equal(cell(doc, 1002).querySelectorAll(':scope > .greenline-pause i').length, 2);
      assert.equal(timeText(doc, 1002), null);
      assert.ok(cell(doc, 1002).classList.contains('greenline-problem'));
      assert.ok(!cell(doc, 1002).classList.contains('greenline-urgent'));
      assert.ok(cell(doc, 1001).classList.contains('greenline-urgent'), 'a Pest crop that is not a research crop stays urgent');
    });
  }

  test('jsdom: a cell shows no crop name', async (t: Any) => {
    const { doc } = await setup(t);

    // The hover card is hidden until the pointer is on the cell, so only the rest counts.
    for (const c of doc.querySelectorAll('.greenline-cell')) {
      const shown = [...c.childNodes].filter((n: Any) => !(n.classList && n.classList.contains('entry-tip')))
        .map((n: Any) => n.textContent).join('');
      assert.ok(!shown.includes('Watermelon') && !shown.includes('Planter'), shown);
    }
  });

  test('jsdom: a new state of a pot changes its cell content, not its place', async (t: Any) => {
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

  // Days with one decimal, rounded down, so a cell never shows a crop as nearer than it is; whole days from 10 days.
  test('shortTime: days with one decimal, hours and minutes, or minutes, in English and Chinese', async (t: Any) => {
    const { root } = await setup(t);
    const short = (s: Any, lang: Any) => runPageJs(root, `window.__greenline.shortTime(${s}, '${lang}')`);

    assert.equal(short(86400 + 5 * 3600 + 6 * 60, 'en'), '1.2d');
    assert.equal(short(86400 + 9 * 3600 + 35 * 60, 'en'), '1.3d', '1.399 days rounds down');
    assert.equal(short(86400, 'en'), '1.0d');
    assert.equal(short(9 * 86400 + 23 * 3600, 'en'), '9.9d');
    assert.equal(short(10 * 86400, 'en'), '10d');
    assert.equal(short(12 * 86400 + 23 * 3600, 'en'), '12d');
    assert.equal(short(5 * 3600 + 6 * 60 + 30, 'en'), '5:06');
    assert.equal(short(3600, 'en'), '1:00');
    assert.equal(short(45 * 60 + 59, 'en'), '45m');
    assert.equal(short(0, 'en'), '0m');
    assert.equal(short(86400 + 17 * 3600, 'zh'), '1.7天');
    assert.equal(short(12 * 86400, 'zh'), '12天');
    assert.equal(short(5 * 3600 + 6 * 60, 'zh'), '5:06');
    assert.equal(short(45 * 60, 'zh'), '45分');
  });

  test('fullTime: the days, hours, and minutes, in English and Chinese', async (t: Any) => {
    const { root } = await setup(t);
    const full = (s: Any, lang: Any) => runPageJs(root, `window.__greenline.fullTime(${s}, '${lang}')`);

    assert.equal(full(86400 + 5 * 3600 + 6 * 60, 'en'), '1 d 5 h 6 min');
    assert.equal(full(5 * 3600 + 6 * 60, 'en'), '5 h 6 min');
    assert.equal(full(45 * 60, 'en'), '45 min');
    assert.equal(full(86400 + 5 * 3600 + 6 * 60, 'zh'), '1天5小时6分');
    assert.equal(full(45 * 60, 'zh'), '45分');
  });

  function timeText(doc?: Any, potId?: Any) {
    const el = cell(doc, potId).querySelector('.greenline-time');
    return el ? el.textContent : null;
  }

  // Sets the countdown of a game row, as the game's own frame loop keeps it.
  function setRemain(core?: Any, rowId?: Any, seconds?: Any) {
    core.eval(`state.maturePlants.find((p) => p.showInstanceId === ${rowId}).remainGameSeconds = ${seconds}`);
  }

  test('jsdom: a cell shows the countdown of its game row, only for a status with a countdown', async (t: Any) => {
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

  test('jsdom: step() writes the countdown of a cell from its game row', async (t: Any) => {
    const { doc, core, root } = await setup(t);

    setRemain(core, 5001, 2 * 3600 + 7 * 60 + 30);
    root.__greenline.step();

    assert.equal(timeText(doc, 1001), '2:07');
  });

  test('jsdom: check() is ok on an applied frame, and not applied on a frame the game built again', async (t: Any) => {
    const { root } = await setup(t);
    assert.equal(root.__greenline.check(), 'ok');

    const rebuilt = await loadCoreWindow(t);
    const fresh = makeRootWindow(t, rebuilt);
    runPageJs(fresh, '0');
    assert.equal(fresh.__greenline.check(), 'not applied');
  });

  test('jsdom: the mod registers no requestAnimationFrame callback', async (t: Any) => {
    const core = await loadCoreWindow(t);
    await postPlants(core, FIXTURE_ROWS);
    await expand(core);
    const modCallbacks = [];
    const raf = core.requestAnimationFrame.bind(core);
    core.requestAnimationFrame = (cb: Any) => {
      if (/updateCountdowns/.test(String(cb))) modCallbacks.push(cb);
      return raf(cb);
    };

    setPots(makeRootWindow(t, core), fixture());
    await wait(core, 60);

    assert.equal(modCallbacks.length, 0);
  });

  test('jsdom: a stalled crop (status 6) shows no countdown', async (t: Any) => {
    const { doc, core } = await setup(t, { rows: [plant(5001, 6, 7200, 0), plant(5002, 1, 3600, 0), plant(5004, 2)] });
    await wait(core, 60);

    assert.equal(timeText(doc, 1001), null);
  });

  test('jsdom: the countdown writes its text only when the text changes', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    setRemain(core, 5001, 5 * 3600 + 6 * 60 + 50);
    root.__greenline.step();
    const time = cell(doc, 1001).querySelector('.greenline-time');

    const records: Any[] = [];
    const observer = new core.MutationObserver((list: Any) => records.push(...list));
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

  // The pot card of a cell: the pointer comes onto the cell. jsdom has no layout, so a test that reads
  // the place gives the cell its rect.
  function hoverCell(doc?: Any, potId?: Any, rect?: Any) {
    const c = cell(doc, potId);
    if (rect) c.getBoundingClientRect = () => ({ ...rect, width: rect.right - rect.left, height: rect.bottom - rect.top });
    c.dispatchEvent(new doc.defaultView.MouseEvent('mouseenter', { clientX: 50, clientY: 60 }));
    return c;
  }

  // The card of a cell: the one card node of the frame shows it next to the cell.
  function card(doc?: Any, potId?: Any) {
    hoverCell(doc, potId);
    const node = hoverNode(doc);
    assert.ok(node && !node.hidden, 'the card shows');
    return readCard(node);
  }

  // The header and rows of the card in an element (a grid cell or the world hover node).
  function readCard(el?: Any) {
    const tip = el.querySelector(':scope > .entry-tip');
    assert.ok(tip, 'the element has its card');
    const chipOf = (el: Any) => {
      const cells = el && el.querySelector('.greenline-chip .cells');
      return cells ? [[...cells.classList].find((k: Any) => /^s\d$/.test(k)), cells.querySelectorAll('.cell').length] : null;
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
        subOrder: sub && sub.querySelector('.greenline-card-pill') ? [...sub.querySelector('.greenline-card-pill').children].map((e: Any) => e.className || e.tagName) : null,
        subChip: chipOf(sub)
      },
      rows: [...tip.querySelectorAll('.greenline-card-row')].map((row: Any) => {
        const value = row.querySelector('.greenline-card-value');
        const img = value.querySelector('img');
        const bar = value.querySelector('.greenline-bar');
        return {
          key: row.querySelector('.greenline-card-key').textContent,
          icon: img ? img.getAttribute('src') : null,
          text: value.textContent,
          classes: [...value.classList].filter((k: Any) => k !== 'greenline-card-value'),
          bar: bar ? {
            fill: bar.querySelector('.greenline-bar-fill').style.width,
            kind: [...bar.classList].filter((k: Any) => k !== 'greenline-bar'),
            pause: !!bar.querySelector('.greenline-pause')
          } : null
        };
      })
    };
  }

  function rowsWith(c?: Any, key?: Any) {
    return c.rows.filter((r: Any) => r.key === key);
  }

  function rowWith(c?: Any, key?: Any) {
    const found = rowsWith(c, key);
    assert.equal(found.length, 1, `one "${key}" row in ${JSON.stringify(c.rows)}`);
    return found[0];
  }

  test('jsdom: the card of a crop has the crop and the pot (with a small pot icon) as its header, each with its size chip', async (t: Any) => {
    const { doc } = await setup(t);
    const c = card(doc, 1001);

    assert.deepEqual(c.head, {
      icon: '../../Res/Food/UI_Item_Icon_Food_xigua.png', name: 'Watermelon', chip: ['s4', 4],
      sub: 'Large Planter', subIcon: '../../Res/Furniture/UI_Item_Icon_plantcommon_large.png',
      subOrder: ['IMG', 'greenline-card-text', 'greenline-chip'], subChip: ['s4', 4]
    });
  });

  test('jsdom: each problem is its own red State row, and a stalled growth bar is red with the pause mark', async (t: Any) => {
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

  test('jsdom: a growing crop with no problem shows Growing and a green growth bar', async (t: Any) => {
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

  test('jsdom: the growth bar of a crop with no problem follows the countdown of its game row, with no new pot data', async (t: Any) => {
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
    test(`jsdom: the growth bar of a crop with a problem in the pot data stays still with a game row of status ${status}`, async (t: Any) => {
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
    test(`jsdom: a crop with no problem in the pot data but ${name} has no growth bar`, async (t: Any) => {
      const { doc } = await setup(t, { pots: noProblemData(), rows });

      assert.equal(rowsWith(card(doc, 1001), 'Growth').length, 0);
    });
  }

  test('jsdom: the fertilizer row has the icon and name, or a grey None with no icon, then the auto value', async (t: Any) => {
    const { doc } = await setup(t);

    const parts = (_pot?: Any) => [...hoverNode(doc).querySelectorAll('.greenline-fert-name, .greenline-fert-auto')]
      .map((e: Any) => [e.textContent, e.classList.contains('greenline-muted')]);

    const fert = rowWith(card(doc, 1001), 'Fertilizer');
    assert.equal(fert.icon, '../../Res/Material/UI_Item_Icon_Mat_S_1500310101.png');
    assert.deepEqual(parts(1001), [['Compound Fertilizer', false], ['(auto: on)', false]], 'a set fertilizer and auto on are white');

    const none = rowWith(card(doc, 1002), 'Fertilizer');
    assert.equal(none.icon, null);
    assert.deepEqual(parts(1002), [['None', true], ['(auto: off)', true]], 'None and auto off are grey');
  });

  test('jsdom: the fertilizer row shows the dose of a part dose after the name, and no dose for a full dose', async (t: Any) => {
    const data = fixture();
    data.pots['1001'].fertDose = '1/4';
    const { doc } = await setup(t, { pots: data });

    const parts = (_pot?: Any) => [...hoverNode(doc).querySelectorAll('.greenline-card-row .greenline-card-value > span')]
      .map((e: Any) => [e.className, e.textContent]).filter(([cls]) => /greenline-fert/.test(cls));

    card(doc, 1001);
    assert.deepEqual(parts(), [
      ['greenline-fert-name', 'Compound Fertilizer'], ['greenline-fert-dose', '1/4'], ['greenline-fert-auto', '(auto: on)']
    ]);
    card(doc, 1002);
    assert.deepEqual(parts().map(([cls]) => cls), ['greenline-fert-name greenline-muted', 'greenline-fert-auto greenline-muted']);
  });

  test('jsdom: a long fertilizer name is cut with an ellipsis, and the dose and the auto value stay whole', async (t: Any) => {
    const data = fixture();
    data.pots['1001'].fertDose = '1/4';
    const { doc, core } = await setup(t, { pots: data });
    card(doc, 1001);
    const style = (sel: Any) => core.getComputedStyle(hoverNode(doc).querySelector(sel));

    assert.equal(style('.greenline-card-rows').gridTemplateColumns, 'auto minmax(0, 1fr)');
    const name = style('.greenline-fert-name');
    assert.deepEqual([name.minWidth, name.overflow, name.textOverflow], ['0px', 'hidden', 'ellipsis']);
    assert.equal(style('.greenline-fert-dose').flexShrink, '0');
    assert.equal(style('.greenline-fert-auto').flexShrink, '0');
  });

  test('jsdom: the Auto-replant row is a white On or a grey Off', async (t: Any) => {
    const { doc } = await setup(t);

    const on = rowWith(card(doc, 1001), 'Auto-replant');
    assert.equal(on.text, 'On');
    assert.ok(!on.classes.includes('greenline-muted'));
    const off = rowWith(card(doc, 1002), 'Auto-replant');
    assert.equal(off.text, 'Off');
    assert.ok(off.classes.includes('greenline-muted'));
  });

  test('jsdom: a ripe crop shows the gold ripe icon and a gold harvest bar with the game word as its key', async (t: Any) => {
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

  test('jsdom: the card of a research crop that needs fertilizer has a red State row with the Basic Fertilizer icon and no time', async (t: Any) => {
    const data = researchData(['needFert']);
    data.words = { ...data.words, problemNeedFert: 'Stagnant: Needs Fertilizer' };
    const { doc, core } = await setup(t, { pots: data, rows: [plant(5001, 3, 7200, 0), plant(5002, 8, 3600, 0), plant(5004, 2)] });
    setRemain(core, 5002, 3600 + 5 * 60 + 50);
    const c = card(doc, 1002);

    const state = rowWith(c, 'State');
    assert.equal(state.icon, BASIC_FERT_ICON);
    assert.equal(state.text, 'Stagnant: Needs Fertilizer');
    assert.ok(state.classes.includes('greenline-red'));
    assert.deepEqual(rowWith(c, 'Growth').bar, { fill: '50%', kind: ['greenline-stalled'], pause: true });
  });

  test('jsdom: the card of a research crop with Pest has a red Pest State row with no time', async (t: Any) => {
    const { doc, core } = await setup(t, { pots: researchData(['pest']), rows: [plant(5001, 3, 7200, 0), plant(5002, 9, 3600, 0), plant(5004, 2)] });
    setRemain(core, 5002, 3600 + 5 * 60 + 50);
    const c = card(doc, 1002);

    const state = rowWith(c, 'State');
    assert.equal(state.text, 'Pest');
    assert.ok(state.classes.includes('greenline-red'));
  });

  test('jsdom: a mature crop with no harvest window has the gold border and the gold State row, and no countdown or harvest bar', async (t: Any) => {
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

  test('jsdom: the card of an empty pot has the pot header, no State row, and no growth bar', async (t: Any) => {
    const { doc } = await setup(t);
    const c = card(doc, 1003);

    assert.deepEqual(c.head, { icon: '../../Res/Furniture/UI_Item_Icon_plantcommon_small.png', name: 'Small Planter', chip: ['s1', 1], sub: null, subIcon: null, subOrder: null, subChip: null });
    assert.deepEqual(c.rows.map((r: Any) => [r.key, r.text]), [['Fertilizer', 'None(auto: on)'], ['Auto-replant', 'On']]);
  });

  test('jsdom: a Poor pot shows its state with the Till mark, and a medium pot has the s2 chip', async (t: Any) => {
    const { doc } = await setup(t);
    const c = card(doc, 1005);

    assert.deepEqual(c.head.chip, ['s2', 2]);
    const state = rowWith(c, 'State');
    assert.equal(state.text, 'Poor');
    assert.ok(state.icon);
  });

  test('jsdom: a withered crop shows Withered and no growth bar', async (t: Any) => {
    const { doc } = await setup(t);
    const c = card(doc, 1004);

    assert.equal(rowWith(c, 'State').text, 'Withered');
    assert.equal(c.rows.filter((r: Any) => r.bar).length, 0);
  });

  test('jsdom: the Chinese card uses the words of the pot data', async (t: Any) => {
    const data = fixture();
    data.lang = 'zh';
    data.words = { ...data.words, labelFertilizer: '肥料', valueNone: '无', autoOff: '（自动：关）', labelState: '状态', stateMature: '成熟' };
    const { doc } = await setup(t, { pots: data });

    assert.equal(rowWith(card(doc, 1002), '肥料').text, '无（自动：关）', 'the two parts sit side by side');
    assert.equal(rowWith(card(doc, 1002), '状态').text, '成熟');
  });

  async function clickCell(doc?: Any, core?: Any, root?: Any, potId?: Any) {
    const received: Any[] = [];
    const listener = (e: Any) => received.push(e.data);
    root.addEventListener('message', listener);
    cell(doc, potId).dispatchEvent(new core.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);
    root.removeEventListener('message', listener);
    return received;
  }

  test('jsdom: a click on a crop cell posts the row click of the game with the click id of the pot', async (t: Any) => {
    const { doc, core, root } = await setup(t);

    const received = await clickCell(doc, core, root, 1001);

    assert.equal(received.length, 1);
    assert.equal(received[0].type, 'PLANT_MATURE_CLICK');
    assert.equal(received[0].sourcePageId, 'CoreUI1');
    assert.deepEqual({ ...received[0].data }, { instanceId: 5001 });
  });

  test('jsdom: a click on an empty or Poor pot cell also posts the row click, with its click id', async (t: Any) => {
    const { doc, core, root } = await setup(t);

    const empty = await clickCell(doc, core, root, 1003);
    const poor = await clickCell(doc, core, root, 1005);

    assert.deepEqual(empty.map((m: Any) => [m.type, m.data.instanceId]), [['PLANT_MATURE_CLICK', 5003]]);
    assert.deepEqual(poor.map((m: Any) => [m.type, m.data.instanceId]), [['PLANT_MATURE_CLICK', 5005]]);
  });

  function assertGridGone(doc?: Any, core?: Any) {
    assert.equal(doc.querySelectorAll('.greenline-cell, .greenline-floor-label, .greenline-grid').length, 0);
    for (const row of doc.querySelectorAll('.mature-item-row')) assert.notEqual(core.getComputedStyle(row).display, 'none');
  }

  test('jsdom: with no pot data the grid is removed and the game rows show again', async (t: Any) => {
    const { doc, core, root } = await setup(t);

    assert.equal(setPots(root, null), 'installed');
    assertGridGone(doc, core);
  });

  test('jsdom: a missing part turns the grid off and install() names it', async (t: Any) => {
    const { doc, core, root } = await setup(t);

    // Simulates a game update that renamed the class of the game rows.
    for (const el of doc.querySelectorAll('.mature-item-row')) el.classList.remove('mature-item-row');

    const result = runPageJs(root, 'window.__greenline.install()');
    assert.match(result, /missing: grid\(\.mature-item-row\)/, `expected a missing-part report, got "${result}"`);
    assertGridGone(doc, core);
  });

  // The plant list message with the game's own Patrol values, as the header reads them.
  async function postPatrol(coreWindow?: Any, unlocked?: Any, replantOn?: Any) {
    coreWindow.postMessage({
      type: 'WebUI_CoreUI1_PlantMatureMsg',
      data: { patrolUnlocked: unlocked, replantOn: replantOn }
    }, '*');
    await wait(coreWindow, 30);
  }

  function tendButton(doc?: Any) { return doc.querySelector('.mature-toggle-row .greenline-tend-btn'); }

  test('jsdom: a Tend All button shows before the chevron while a chore waits', async (t: Any) => {
    const { doc } = await setup(t);

    const btn = tendButton(doc);
    assert.ok(btn, 'no Tend All button');
    assert.equal(btn.textContent, 'Tend All');
    assert.ok(btn.nextElementSibling.classList.contains('mature-toggle-chevron'));
    assert.ok(btn.hasAttribute('data-interactive'));
  });

  test('jsdom: no Tend All button with no chore or with no Patrol', async (t: Any) => {
    const { doc, root } = await setup(t);

    setPots(root, Object.assign(fixture(), { pendingChores: 0 }));
    assert.equal(tendButton(doc), null);
    setPots(root, Object.assign(fixture(), { patrol: false }));
    assert.equal(tendButton(doc), null);
  });

  test('jsdom: a Tend All click posts GREENLINE_TEND and does not toggle the list', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    const received: Any[] = [];
    root.addEventListener('message', (e: Any) => received.push(e.data));

    tendButton(doc).dispatchEvent(new core.MouseEvent('click', { bubbles: true }));
    await wait(root, 30);

    assert.deepEqual(received.map((m: Any) => [m.type, m.sourcePageId, JSON.stringify({ ...m.data })]), [['GREENLINE_TEND', 'CoreUI1', '{}']]);
    assert.equal(core.eval('state.maturePlantsExpanded'), true);
  });

  test('jsdom: the game header button and checkbox are hidden while the mod shows Patrol', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    await postPatrol(core, true, false);
    setPots(root, fixture());

    for (const sel of ['.mature-patrol-btn', '.mature-replant-chk']) {
      const el = doc.querySelector(sel);
      assert.ok(el, `${sel} not rendered by the game`);
      assert.equal(core.getComputedStyle(el).display, 'none', `${sel} still shows`);
    }
  });

  function replantLine(doc?: Any) { return doc.querySelector('.mature-popover .greenline-replant'); }

  test('jsdom: the Auto-replant line is the last grid line and shows the option of the pot data', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    // The game's own Replant value does not count.
    await postPatrol(core, true, false);
    root.__greenline.step();

    const line = replantLine(doc);
    assert.ok(line, 'no Auto-replant line');
    assert.deepEqual(gridPlace(line), ['3', '1 / -1']);
    assert.ok(line.hasAttribute('data-interactive'));
    assert.ok(line.textContent.includes('Auto-replant'));
    assert.equal(line.querySelector('input[type=checkbox]').checked, true);
    assert.ok(line.classList.contains('has-entry-tip'));
    assert.ok(line.querySelector('.entry-tip').textContent.includes('plants the same crop again'));

    setPots(root, Object.assign(fixture(), { replantOn: false }));
    assert.equal(line.querySelector('input[type=checkbox]').checked, false);
    root.__greenline.step();
    assert.equal(line.querySelector('input[type=checkbox]').checked, false);
  });

  test('jsdom: an Auto-replant click posts GREENLINE_REPLANT with the new value', async (t: Any) => {
    const { doc, root } = await setup(t, { pots: Object.assign(fixture(), { replantOn: false }) });
    const received: Any[] = [];
    root.addEventListener('message', (e: Any) => received.push(e.data));

    replantLine(doc).querySelector('input[type=checkbox]').click();
    await wait(root, 30);

    assert.deepEqual(received.map((m: Any) => [m.type, m.sourcePageId, JSON.stringify({ ...m.data })]), [['GREENLINE_REPLANT', 'CoreUI1', '{"on":true}']]);
  });

  test('jsdom: no Auto-replant line with no Patrol', async (t: Any) => {
    const { doc } = await setup(t, { pots: Object.assign(fixture(), { patrol: false }) });

    assert.equal(replantLine(doc), null);
  });

  test('jsdom: a missing chevron turns off only the Tend All button', async (t: Any) => {
    const { doc, root } = await setup(t);
    doc.querySelector('.mature-toggle-chevron').classList.remove('mature-toggle-chevron');

    const result = runPageJs(root, 'window.__greenline.install()');

    assert.match(result, /missing: tend\(\.mature-toggle-chevron\)/, result);
    assert.equal(tendButton(doc), null);
    assert.ok(doc.querySelector('.greenline-grid'), 'the grid still shows');
  });

  test('jsdom: a word is the text of the pot data, and its name before the first pot data', async (t: Any) => {
    const { root } = await setup(t);
    runPageJs(root, 'window.__greenline.setPots(null)');

    assert.equal(runPageJs(root, 'window.__greenline.word("stateGrowing")'), 'stateGrowing');
    setPots(root, Object.assign(fixture(), { words: { problemDrought: 'Dry' } }));
    assert.equal(runPageJs(root, 'window.__greenline.word("problemDrought")'), 'Dry');
    assert.equal(runPageJs(root, 'window.__greenline.word("stateGrowing")'), 'stateGrowing');
  });


  // The world hover card: C# sends the pot under the pointer in the game world.
  function setHover(root?: Any, potId?: Any, x?: Any, y?: Any, keepCell?: Any) {
    const args = [potId].concat(x === undefined ? [] : [x, y]).concat(keepCell === undefined ? [] : [keepCell])
      .map((a: Any) => JSON.stringify(a)).join(',');
    return runPageJs(root, 'window.__greenline.setHover(' + args + ')');
  }

  function hoverNode(doc?: Any) {
    const nodes = doc.querySelectorAll('.greenline-hover');
    assert.ok(nodes.length <= 1, 'at most one hover node');
    return nodes[0] || null;
  }

  test('jsdom: setHover shows the card of the pot in one node of the frame body, the same as the grid card, and takes no pointer', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001);

    const node = hoverNode(doc);
    assert.ok(node, 'no hover node');
    assert.equal(node.parentNode, doc.body);
    assert.ok(!node.hidden);
    assert.equal(core.getComputedStyle(node).pointerEvents, 'none');
    assert.deepEqual(readCard(node), card(doc, 1001));
  });

  test('jsdom: setHover hides the card for no pot and for an unknown pot', async (t: Any) => {
    const { doc, root } = await setup(t);

    setHover(root, 1001);
    setHover(root, 0);
    assert.ok(hoverNode(doc).hidden, 'no pot');
    setHover(root, 1001);
    assert.ok(!hoverNode(doc).hidden);
    setHover(root, 424242);
    assert.ok(hoverNode(doc).hidden, 'unknown pot');
  });

  test('jsdom: setHover of the same pot twice writes nothing to the DOM', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001);
    await wait(core, 60);

    const records: Any[] = [];
    const observer = new core.MutationObserver((list: Any) => records.push(...list));
    observer.observe(hoverNode(doc), { childList: true, subtree: true, attributes: true, characterData: true });
    setHover(root, 1001);
    await wait(core, 30);
    observer.disconnect();

    assert.equal(records.length, 0, `unexpected writes: ${records.map((r: Any) => r.type + ' ' + r.attributeName).join(', ')}`);
  });

  // The card is pinned next to its anchor (a cell, or the place of a world pot): right of it with the
  // arrow at the middle of the anchor, on the other side when the card would pass the right edge, and
  // inside the window at the top and the bottom. jsdom has no layout, so a test gives the card a size.
  function sized(node?: Any, w?: Any, h?: Any) {
    Object.defineProperty(node, 'offsetWidth', { value: w, configurable: true });
    Object.defineProperty(node, 'offsetHeight', { value: h, configurable: true });
  }

  function placeOf(node?: Any) {
    return [node.style.left, node.style.top, node.style.getPropertyValue('--gl-arrow-y'), node.classList.contains('greenline-hover-left')];
  }

  const CELL = { left: 100, top: 200, right: 126, bottom: 226 };

  test('jsdom: the grid card sits right of its cell with the arrow at the middle of the cell', async (t: Any) => {
    const { doc } = await setup(t);
    hoverCell(doc, 1001, CELL);
    const node = hoverNode(doc);
    sized(node, 200, 100);
    hoverCell(doc, 1001, CELL);

    assert.deepEqual(placeOf(node), ['136px', '163px', '50px', false]);
  });

  test('jsdom: the grid card sits left of its cell at the right edge of the window', async (t: Any) => {
    const { doc, core } = await setup(t);
    const rect = { left: core.innerWidth - 60, top: 200, right: core.innerWidth - 34, bottom: 226 };
    hoverCell(doc, 1001, rect);
    const node = hoverNode(doc);
    sized(node, 200, 100);
    hoverCell(doc, 1001, rect);

    assert.deepEqual(placeOf(node), [(core.innerWidth - 60 - 10 - 200) + 'px', '163px', '50px', true]);
  });

  test('jsdom: the card stays inside the window at the top and the bottom, and the arrow keeps pointing at the cell', async (t: Any) => {
    const { doc, core } = await setup(t);
    hoverCell(doc, 1001, CELL);
    const node = hoverNode(doc);
    sized(node, 200, 100);

    hoverCell(doc, 1001, { left: 100, top: 0, right: 126, bottom: 26 });
    assert.deepEqual(placeOf(node), ['136px', '0px', '13px', false]);

    const h = core.innerHeight;
    hoverCell(doc, 1001, { left: 100, top: h - 26, right: 126, bottom: h });
    assert.deepEqual(placeOf(node), ['136px', (h - 100) + 'px', '87px', false]);
  });

  test('jsdom: the place takes the CSS zoom of the frame body', async (t: Any) => {
    const { doc } = await setup(t);
    hoverCell(doc, 1001, CELL);
    const node = hoverNode(doc);
    sized(node, 200, 100);
    Object.defineProperty(node, 'currentCSSZoom', { value: 2, configurable: true });
    hoverCell(doc, 1001, CELL);

    // The cell rect is in window pixels; the node's left and top are in zoomed pixels.
    assert.deepEqual(placeOf(node), ['73px', '57px', '50px', false]);
  });

  test('jsdom: the grid card shows when the pointer comes onto a cell, stays still while it moves there, and closes when it leaves', async (t: Any) => {
    const { doc, core } = await setup(t);
    const c = hoverCell(doc, 1001, CELL);
    const node = hoverNode(doc);
    assert.ok(!node.hidden);
    const before = placeOf(node);

    const records: Any[] = [];
    const observer = new core.MutationObserver((list: Any) => records.push(...list));
    observer.observe(node, { childList: true, subtree: true, attributes: true, characterData: true });
    c.dispatchEvent(new core.MouseEvent('mousemove', { bubbles: true, clientX: 110, clientY: 210 }));
    c.dispatchEvent(new core.MouseEvent('mousemove', { bubbles: true, clientX: 120, clientY: 220 }));
    await wait(core, 30);
    observer.disconnect();
    assert.equal(records.length, 0, 'a move on the cell writes nothing');
    assert.deepEqual(placeOf(node), before);

    c.dispatchEvent(new core.MouseEvent('mouseleave', {}));
    assert.ok(node.hidden);
  });

  test('jsdom: the grid card closes when the pointer leaves the frame', async (t: Any) => {
    const { doc, core } = await setup(t);
    hoverCell(doc, 1001, CELL);
    doc.body.dispatchEvent(new core.MouseEvent('mouseout', { bubbles: true, relatedTarget: null }));

    assert.ok(hoverNode(doc).hidden);
  });

  function worldPlaceOf(node?: Any) {
    return [node.style.left, node.style.top, node.style.getPropertyValue('--gl-arrow-x'), node.classList.contains('greenline-hover-below')];
  }

  // C# sends the place of the top of the pot on the screen, as fractions of the screen from the top-left corner.
  // The world card sits above it, as the game's card of a trap, with the arrow down at the pot.
  test('jsdom: setHover places the world card above the pot, moves it with a new place, and hides it with 0', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001, 0.25, 0.5);
    const node = hoverNode(doc);
    sized(node, 200, 100);
    setHover(root, 1001, 0.25, 0.5);
    const x = core.innerWidth / 4, y = core.innerHeight / 2;
    assert.deepEqual(worldPlaceOf(node), [Math.round(x - 100) + 'px', Math.round(y - 10 - 100) + 'px', '100px', false]);
    assert.ok(!node.classList.contains('greenline-hover-left'));

    setHover(root, 1001, 0.5, 0.5);
    assert.equal(node.style.left, Math.round(core.innerWidth / 2 - 100) + 'px', 'a camera move moves the card with the pot');

    setHover(root, 0);
    assert.ok(node.hidden);
  });

  test('jsdom: the world card goes under the pot when there is no room above, and stays inside the window at the sides', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    setHover(root, 1001, 0.5, 0.05);
    const node = hoverNode(doc);
    sized(node, 200, 100);

    const y = core.innerHeight * 0.05;
    setHover(root, 1001, 0.5, 0.05);
    assert.deepEqual(worldPlaceOf(node), [Math.round(core.innerWidth / 2 - 100) + 'px', Math.round(y + 10) + 'px', '100px', true]);

    // The arrow keeps pointing at the pot when the card stops at the edge.
    setHover(root, 1001, 0, 0.5);
    assert.deepEqual(worldPlaceOf(node), ['0px', Math.round(core.innerHeight / 2 - 110) + 'px', '0px', false]);
    setHover(root, 1001, 1, 0.5);
    assert.deepEqual(worldPlaceOf(node), [(core.innerWidth - 200) + 'px', Math.round(core.innerHeight / 2 - 110) + 'px', '200px', false]);
  });

  test('jsdom: the grid card after a world card is beside its cell again', async (t: Any) => {
    const { doc, root } = await setup(t);
    setHover(root, 1001, 0.5, 0.05);
    const node = hoverNode(doc);
    sized(node, 200, 100);
    setHover(root, 1001, 0.5, 0.05);
    assert.ok(node.classList.contains('greenline-hover-below'));

    hoverCell(doc, 1001, CELL);
    assert.deepEqual(placeOf(node), ['136px', '163px', '50px', false]);
    assert.ok(!node.classList.contains('greenline-hover-below') && !node.classList.contains('greenline-hover-above'));
  });

  // The pointer leaves the web UI for the game world: C# sends 0, which also closes the card of a cell
  // that got no mouseleave (the game gives the page no pointer move over the world).
  test('jsdom: setHover 0 closes the grid card too', async (t: Any) => {
    const { doc, root } = await setup(t);
    hoverCell(doc, 1001, CELL);

    setHover(root, 0);

    assert.ok(hoverNode(doc).hidden);
  });

  // C# sends 0 also when the pointer moves from a pot in the world onto the web UI: with keepCell the card of the
  // cell that the pointer just entered stays.
  test('jsdom: setHover 0 with keepCell leaves the grid card open', async (t: Any) => {
    const { doc, root } = await setup(t);
    hoverCell(doc, 1001, CELL);

    setHover(root, 0, 0, 0, true);

    assert.ok(!hoverNode(doc).hidden);
    assert.deepEqual(readCard(hoverNode(doc)), card(doc, 1001));
  });

  test('jsdom: an error of setHover goes to C# as a message, and setHover does not throw', async (t: Any) => {
    const { core, root } = await setup(t);
    const sent: Any[] = [];
    root.vuplex = { postMessage: (m: Any) => sent.push(m) };
    core.eval = () => { throw new Error('broken page'); };

    assert.match(setHover(root, 1001, 0.25, 0.5), /^error: .*broken page/);
    assert.equal(sent.length, 1);
    assert.match(sent[0], /^slmod\|greenline\|hover error: .*broken page/);
  });

  // One card shows at a time, so the frame keeps one pot card node, and the frame step builds only it.
  test('jsdom: the frame keeps one pot card node for the grid and the world', async (t: Any) => {
    const { doc, core, root } = await setup(t);
    await wait(core, 60);
    assert.equal(doc.querySelectorAll('.greenline-pot-card').length, 0, 'no card before a hover');

    assert.equal(card(doc, 1001).head.name, 'Watermelon');
    assert.equal(card(doc, 1002).head.name, 'Potato');
    cell(doc, 1002).dispatchEvent(new core.MouseEvent('mouseleave', {}));
    setHover(root, 1001, 0.25, 0.5);
    await wait(core, 30);

    const cards = doc.querySelectorAll('.greenline-pot-card');
    assert.equal(cards.length, 1);
    assert.equal(cards[0].parentNode, hoverNode(doc));
    assert.equal(readCard(hoverNode(doc)).head.name, 'Watermelon');
    assert.equal(doc.querySelectorAll('.greenline-cell .entry-tip').length, 0, 'no card in a cell');
  });

  // CoreUI1 sends the places of its HUD parts to the game after each change of style or class; the card
  // moves with its cell and its pot, so it opts out with the game's own marker.
  test('jsdom: the card node opts out of the game sync of the HUD places', async (t: Any) => {
    const { doc } = await setup(t);
    hoverCell(doc, 1001);
    assert.ok(hoverNode(doc).hasAttribute('data-no-sync'));
  });
}
