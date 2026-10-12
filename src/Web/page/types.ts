// The data that the page script reads: the fields of the CoreUI1 page state, and the pot data of
// setPots (the JSON contract of GridLogic.ToJson in C#).

// One row of the plant list of the CoreUI1 state (state.maturePlants, filled from
// WebUI_CoreUI1_PlantMatureMsg). The game counts remainGameSeconds down in its own frame loop.
export interface PlantRow {
  showInstanceId: number;
  // 0 growing, 1 mature, 2 withered, 3 Pest, 4 Frost; the others stop the growth.
  status: number;
  remainGameSeconds: number;
  harvestTotalSeconds: number;
}

// The fields of `const state = reactive({...})` in CoreUI1.html that the script reads.
export interface CoreState {
  maturePlantsActive: boolean;
  maturePlants: PlantRow[];
  maturePlantsExpanded: boolean;
  // The game's time word of the harvest window ('Harvest', '收获 ').
  i18n_plantHarvest?: string;
}

// The CoreUI1 frame: a window whose top-level `state` the script reads with eval.
export type CoreWindow = Window & typeof globalThis;

export type PotState = 'growing' | 'mature' | 'withered' | 'poor' | 'empty';

// One pot of the pot data, by its PotId.
export interface Pot {
  rowId: number;
  clickId: number;
  potName: string;
  potIcon: string;
  potSize: number;
  state: PotState;
  cropName: string;
  cropIcon: string;
  cropSize: number;
  neverWithers: boolean;
  problems: string[];
  badge: string;
  growRemainSeconds: number;
  growTotalSeconds: number;
  fertName: string;
  fertIcon: string;
  fertDose: string;
  autoFert: boolean;
  autoReplant: boolean;
}

// One floor of the grid: its lines hold the cells (PotIds) in this order.
export interface Floor {
  id: number;
  label: string;
  cells: number[];
}

// The pot data of setPots.
export interface PotsData {
  lang: string;
  patrol: boolean;
  pendingChores: number;
  // Greenline's own Auto-replant option.
  replantOn: boolean;
  words: Record<string, string>;
  floors: Floor[];
  pots: Record<string, Pot>;
}

export interface GreenlineApi {
  install(): string;
  setPots(data: PotsData | null): string;
  setHover(potId: number, x?: number, y?: number, keepCell?: boolean): string;
  check(): string;
  step(): void;
  word(key: string): string;
  shortTime(seconds: number, lang: string): string;
  fullTime(seconds: number, lang: string): string;
}

declare global {
  interface Window {
    // The interface of the page script in the root page.
    __greenline?: GreenlineApi;
    // tokens.css and page.css, which C# sets in the root page before the script.
    __greenlineCss?: string;
    // The body observer of the page script in the CoreUI1 frame.
    __greenlineObserver?: MutationObserver;
    // Set in the CoreUI1 frame once the step runs and once the pointer is watched.
    __greenlineTick?: boolean;
    __greenlinePointer?: boolean;
  }
}
