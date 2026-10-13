// The root page of the dev harness: sets the CSS as C# does, loads both page scripts, then gives CoreUI1
// a fake plant list through the game's own plant message and the pot data through setPots, and gives
// the planting window a fake pot through the game's own window messages and the pot values through
// setState, as the plugin does.
import tokensCss from '../../src/Web/tokens.css?inline';
import pageCss from '../../src/Web/page.css?inline';
import plantCss from '../../src/Web/plantpanel.css?inline';
import type { PotsData } from '../../src/Web/page/types';
import pots from './pots.json';

// PageScript.WithCss and PageStyle.OpacityRule with the config defaults (0.9 and 0.96).
window.__greenlineCss = tokensCss + '\n' + pageCss + '\n' + ':root{--gl-panel-opacity:90%;--gl-card-opacity:96%}';
window.__greenlinePlantCss = tokensCss + '\n' + plantCss;
// A page can be ready before the scripts load, so its start waits for them.
const scripts = Promise.all([import('../../src/Web/page/main'), import('../../src/Web/plantpanel/main')]);

type Frame = Window & typeof globalThis;

// The game rows of the pots with a crop (tests/dev/pots.json), as WebUI_CoreUI1_PlantMatureMsg has them:
// status 0 growing, 1 mature, 2 withered, 3 Pest; the times are game seconds.
const plants = [
  { showInstanceId: 6001, plantName: 'Watermelon', status: 0, harvestTotalSeconds: 211920, harvestElapsedSeconds: 50000, dispatchUtcMs: 0 },
  { showInstanceId: 6002, plantName: 'Watermelon', status: 3, harvestTotalSeconds: 7200, harvestElapsedSeconds: 1200, dispatchUtcMs: 0 },
  { showInstanceId: 6003, plantName: 'Overwinter No.1', status: 5, harvestTotalSeconds: 0, harvestElapsedSeconds: 0, dispatchUtcMs: 0 },
  { showInstanceId: 6004, plantName: 'Potato', status: 1, harvestTotalSeconds: 86400, harvestElapsedSeconds: 20000, dispatchUtcMs: 0 },
  { showInstanceId: 6005, plantName: 'Overwinter No.1', status: 1, harvestTotalSeconds: 0, harvestElapsedSeconds: 0, dispatchUtcMs: 0 },
  { showInstanceId: 6006, plantName: 'Spinach', status: 2, harvestTotalSeconds: 0, harvestElapsedSeconds: 0, dispatchUtcMs: 0 },
];

function startCore(core: Frame): void {
  core.postMessage({ type: 'WebUI_CoreUI1_PlantMatureMsg', data: { containerActive: true, replantOn: true, plants } }, '*');
  // Open the plant list, so the grid shows, after Vue has drawn the plant panel.
  setTimeout(() => {
    core.eval('state.maturePlantsExpanded = true');
    setTimeout(() => {
      const result = window.__greenline!.setPots(pots as PotsData);
      console.log('Greenline setPots:', result, window.__greenline!.check());
    }, 100);
  }, 100);
}

// The planting window of the Poor pot 2007 after a stopped replant.
function startPlant(plant: Frame): void {
  plant.postMessage({ type: 'WebUI_PlantPanel_LocalizationMsg', data: {
    titleText: 'PLANTING', envLabel: 'Planting', selectSeedLabel: 'Select a seed', startBtnText: 'PLANT',
    seedInfoLabel: 'Seed info', facilityLabel: 'Facility', capacityLabel: 'Capacity', lightStatusLabel: 'Light',
    tempStatusLabel: 'Temperature', needFillLabel: 'Need: ', emptySeedText: 'No seeds', emptyFertText: 'No fertilizer',
  } }, '*');
  plant.postMessage({ type: 'WebUI_PlantPanel_FacilityMsg', data: {
    facilityName: 'Plant Grow Box (Medium)', facilityIcon: '../../Res/Furniture/UI_Item_Icon_plantcommon_middle.png',
    facilityDesc: 'A medium grow box.', capacity: 2, actualLight: 3, coldStress: 0,
  } }, '*');
  plant.postMessage({ type: 'WebUI_PlantPanel_SeedMsg', data: { seeds: [
    { name: 'Watermelon Seeds', icon: '../../Res/Food/UI_Item_Icon_Food_xigua.png', ownedCount: 0, plantSize: 2, lightNeed: 2, coldResistance: 1 },
    { name: 'Potato Seeds', icon: '../../Res/Food/UI_Item_Icon_Food_S_1300900301.png', ownedCount: 5, plantSize: 1, lightNeed: 1, coldResistance: 2 },
  ] } }, '*');
  plant.postMessage({ type: 'WebUI_PlantPanel_SelectionMsg', data: {
    selectedSeedIndex: 1, selectedFertIndex: -1, requiredSeedCount: 2, canStartPlant: true, isLightOk: true, isTempOk: true,
  } }, '*');
  setTimeout(() => {
    window.__greenlinePlant!.setState({
      pot: '2007', replant: true, fert: false, words: {},
      lastCrop: { label: 'Last crop:', name: 'Watermelon', icon: '../../Res/Food/UI_Item_Icon_Food_xigua.png', red: 'Replant stopped: seeds 0/1' },
    });
    console.log('Greenline plant panel:', window.__greenlinePlant!.install());
  }, 100);
}

// Each page takes messages only after its own start, and then calls this function of the root page, as
// Root.html has it in the game.
(window as Window & { notifyPageReady?: (pageId: string) => void }).notifyPageReady = (pageId) => {
  void scripts.then(() => {
    if (pageId === 'CoreUI1') startCore((document.getElementById('core') as HTMLIFrameElement).contentWindow as Frame);
    if (pageId === 'PlantPanel') startPlant((document.getElementById('plant') as HTMLIFrameElement).contentWindow as Frame);
  });
};

const plantFrame = document.getElementById('plant') as HTMLIFrameElement;
document.getElementById('toggle')!.addEventListener('click', () => { plantFrame.hidden = !plantFrame.hidden; });
