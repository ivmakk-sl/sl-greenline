// Runs in the root page, reaching the CoreUI1 iframe through iframe.contentWindow, as in Trapline.
// Defines window.__greenline once; C# then calls window.__greenline.install() and
// window.__greenline.setPots(data) with the pot data (the JSON contract of GridLogic.ToJson). The
// script turns the HUD plant list into a grid of pots: one or more lines for each floor, the floor name
// in column 1, and one mod cell for each pot. The game rows stay in the DOM (Vue owns them) but are
// hidden while the grid is on. The CoreUI1 iframe is looked up fresh on every call.
//
// This file is the wrapper: each "// @include <file>" line below stands for that file of page/, in
// this order. C# (PageIncludes) and the page tests join them the same way. The files share one
// function scope, so a var whose value reads a var of another file must come in a later file.
window.__greenline = window.__greenline || (function () {
  // @include core.js
  // @include tend.js
  // @include grid.js
  // @include card.js
  // @include pointer.js
  // @include install.js

  return { install: install, setPots: setPots, setHover: setHover, check: check, step: step, word: word, shortTime: shortTime, fullTime: fullTime };
})();
