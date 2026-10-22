// Runs in the root page, reaching the CoreUI1 iframe through iframe.contentWindow, as in Trapline.
// Defines window.__greenline once; C# then calls window.__greenline.setPots(data) with the pot data (the
// JSON contract of GridLogic.ToJson) and asks for no result: the script answers by page messages
// (postMod). The script turns the HUD plant list into a grid of pots: a floor line for each floor, a
// zone line for each other climate area of a floor, the area light and the area temperature at the
// right end of each line, one mod cell for each pot, and the last line with the pin. The game rows stay
// in the DOM (Vue owns them) but are hidden while the grid is on. The CoreUI1 iframe is looked up fresh
// on every call.
//
// Vite builds this entry into one IIFE file (obj/page/page.js), which C# embeds and sends. A second
// full send of the script keeps the first interface and its state.
import { fullTime, shortTime, word } from './core';
import { check, install, setPots, step } from './install';
import { setHover } from './pointer';

window.__greenline = window.__greenline || { install, setPots, setHover, check, step, word, shortTime, fullTime };
