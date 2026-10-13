// The data of the plant panel script: the state that C# sends with setState (PageJson.PlantPanelJson).

// The last-crop line of a planting window: the grey label, the crop with its icon, and the red line of a
// stopped replant ('' for none).
export interface LastCrop {
  label: string;
  name: string;
  icon: string;
  red: string;
}

// The values of one pot for its windows. lastCrop is null when the mod knows no last crop of the pot.
export interface PlantState {
  pot: string;
  replant: boolean;
  fert: boolean;
  lastCrop: LastCrop | null;
  words: Record<string, string>;
}

// The two windows of a pot that the script adds its parts to.
export type PageName = 'PlantPanel' | 'PlantingDetails';

export interface GreenlinePlantApi {
  install(): string;
  setState(state: Partial<PlantState> | null): void;
}

declare global {
  interface Window {
    // The interface of the plant panel script in the root page.
    __greenlinePlant?: GreenlinePlantApi;
    // tokens.css and plantpanel.css, which C# sets in the root page before the script.
    __greenlinePlantCss?: string;
  }
  interface Document {
    // The observer of the plant panel script in a window frame.
    __greenlineObserver?: MutationObserver;
  }
}
