  // The grid of pot cells: the layout, the cell content, the countdowns, and the cell click.

  var CELLS_PER_LINE = 6;

  function place(el, row, column) {
    setStyle(el, 'grid-row', row);
    setStyle(el, 'grid-column', column);
  }

  // Removes everything the grid adds, so the list shows as the game's plain list.
  function clearGrid(doc) {
    var grids = doc.querySelectorAll('.greenline-grid');
    for (var g = 0; g < grids.length; g++) {
      grids[g].classList.remove('greenline-grid');
      if (grids[g].style.getPropertyValue('grid-template-columns')) grids[g].style.removeProperty('grid-template-columns');
    }
    var ours = doc.querySelectorAll('.greenline-floor-label, .greenline-cell, .greenline-replant');
    for (var n = 0; n < ours.length; n++) ours[n].remove();
  }

  var BADGE_ICONS = {
    pest: '../../Res/PlantAnomaly/Pest.png',
    frost: '../../Res/PlantAnomaly/Frost.png',
    drought: '../../Res/PlantAnomaly/Drought.png',
    weed: '../../Res/PlantAnomaly/Weed.png',
    lightLow: '../../Res/PlantAnomaly/LightLow.png',
    needFert: FERT_ICON,
    withered: '../../Res/PlantAnomaly/Withered.png'
  };

  // The action icon of Till (action 1618).
  var TILL_ICON = '../../Res/icon/icon_line_tool_18.png';

  // The countdown text of a pot, from the game row that the game's own frame loop counts down, or ''.
  function countdownText(pot, rows) {
    if (isStalled(pot)) return '';
    if (!pot || !pot.rowId || pot.state === 'withered') return '';
    var row = rows[String(pot.rowId)];
    if (!row || !hasCountdown(row.status) || noHarvestWindow(row)) return '';
    return shortTime(row.remainGameSeconds, potsData.lang);
  }

  // Keeps the countdown line of a cell: written only when the text changes, removed when empty.
  function setTime(doc, el, text) {
    var time = el.querySelector('.greenline-time');
    if (!text) {
      if (time) time.remove();
      return;
    }
    if (!time) {
      time = doc.createElement('div');
      time.className = 'greenline-time';
      el.appendChild(time);
    }
    if (time.textContent !== text) time.textContent = text;
  }

  // The pause mark of a stalled crop: two bars, as the game's pause icon of the speed control.
  function setPause(doc, el, on) {
    var mark = el.querySelector(':scope > .greenline-pause');
    if (!on) {
      if (mark) mark.remove();
      return;
    }
    if (mark) return;
    mark = doc.createElement('span');
    mark.className = 'greenline-pause';
    mark.appendChild(doc.createElement('i'));
    mark.appendChild(doc.createElement('i'));
    el.appendChild(mark);
  }

  // The frame step: the countdown under each cell, from the live game rows.
  function updateCountdowns(doc, state) {
    if (!potsData) return;
    var cells = doc.querySelectorAll('.greenline-cell');
    if (!cells.length) return;
    var rows = rowsById(state);
    var pots = potsData.pots || {};
    for (var i = 0; i < cells.length; i++) {
      var pot = pots[cells[i].getAttribute('data-pot-id')] || {};
      setTime(doc, cells[i], countdownText(pot, rows));
      setPause(doc, cells[i], isStalled(pot));
    }
  }

  // The content of a cell: the crop icon, or the pot icon for an empty or Poor pot, the badge, the
  // Till mark of a Poor pot, the gold border of a mature crop, the red pulse of Pest and Frost (the
  // crop dies), and the red border of a crop that stops growing.
  function fillCell(doc, el, pot) {
    setImg(doc, el, 'greenline-icon', hasCrop(pot) ? pot.cropIcon : pot.potIcon);
    setImg(doc, el, 'greenline-badge', BADGE_ICONS[pot.badge] || '');
    setImg(doc, el, 'greenline-till', pot.state === 'poor' ? TILL_ICON : '');
    toggleClass(el, 'greenline-mature', pot.state === 'mature');
    toggleClass(el, 'greenline-urgent', isUrgent(pot));
    toggleClass(el, 'greenline-problem', isStalled(pot));
  }

  // Posts what a game row posts on a click (UnitySendEvent('PLANT_MATURE_CLICK') from the CoreUI1
  // iframe to its parent, the root page, where this script runs). The game finds the pot by the id, not
  // in its plant list, so the click id of a pot with no game row (an empty or Poor pot) also works.
  function onCellClick(e) {
    e.stopPropagation();
    var pot = potsData && potsData.pots && potsData.pots[e.currentTarget.getAttribute('data-pot-id')];
    if (!pot || !pot.clickId) return;
    window.postMessage({ type: 'PLANT_MATURE_CLICK', data: { instanceId: pot.clickId }, sourcePageId: 'CoreUI1' }, '*');
  }

  function applyGrid(doc, state) {
    if (!potsData) {
      clearGrid(doc);
      return '';
    }
    // A collapse keeps the popover in the DOM for its leave transition, so the grid stays as it is.
    if (!popoverShouldRender(state)) return '';
    var missing = partsMissing(doc, 'grid');
    if (missing.length) {
      clearGrid(doc);
      return 'grid(' + missing.join(',') + ')';
    }

    var popover = doc.querySelector(FEATURES.grid[0]);
    var floors = potsData.floors || [];
    var keep = {};
    var line = 1, widest = 1;
    for (var f = 0; f < floors.length; f++) {
      var floor = floors[f];
      var cells = floor.cells || [];
      var lines = Math.max(1, Math.ceil(cells.length / CELLS_PER_LINE));
      widest = Math.max(widest, Math.min(cells.length, CELLS_PER_LINE));

      var label = popover.querySelector('.greenline-floor-label[data-floor-id="' + floor.id + '"]');
      if (!label) {
        label = doc.createElement('div');
        label.className = 'greenline-floor-label';
        label.setAttribute('data-floor-id', String(floor.id));
        popover.appendChild(label);
      }
      place(label, line + ' / span ' + lines, '1');
      if (label.textContent !== floor.label) label.textContent = floor.label;
      keep['label:' + floor.id] = true;

      for (var c = 0; c < cells.length; c++) {
        var potId = String(cells[c]);
        var el = popover.querySelector('.greenline-cell[data-pot-id="' + potId + '"]');
        if (!el) {
          el = doc.createElement('div');
          el.className = 'greenline-cell';
          el.setAttribute('data-pot-id', potId);
          // CoreUI1 sends a click on an element without [data-interactive] to the game world.
          el.setAttribute('data-interactive', '');
          el.addEventListener('click', onCellClick);
          popover.appendChild(el);
        }
        place(el, String(line + Math.floor(c / CELLS_PER_LINE)), String(2 + c % CELLS_PER_LINE));
        fillCell(doc, el, (potsData.pots || {})[potId] || {});
        keep['cell:' + potId] = true;
      }
      line += lines;
    }

    var ours = popover.querySelectorAll('.greenline-floor-label, .greenline-cell');
    for (var o = 0; o < ours.length; o++) {
      var key = ours[o].classList.contains('greenline-floor-label')
        ? 'label:' + ours[o].getAttribute('data-floor-id')
        : 'cell:' + ours[o].getAttribute('data-pot-id');
      if (!keep[key]) ours[o].remove();
    }

    applyReplant(doc, popover, line);
    updateCountdowns(doc, state);
    setStyle(popover, 'grid-template-columns', 'max-content repeat(' + widest + ', 26px)');
    if (!popover.classList.contains('greenline-grid')) popover.classList.add('greenline-grid');
    return '';
  }
