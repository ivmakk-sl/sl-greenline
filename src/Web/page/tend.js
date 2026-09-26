  // The Tend All button and the Auto-replant line of the list header.

  // The Tend All button of the header, in the style of Trapline's Take All: shown only while Patrol is
  // on and a chore waits. While Patrol is on, the game's own header button and Replant checkbox are
  // hidden (the greenline-patrol class on the panel), because the mod shows its own.
  function applyTend(doc, state) {
    var panel = doc.querySelector(FEATURES.tend[0]);
    if (!panelShouldRender(state) || !panel) return '';
    var missing = partsMissing(doc, 'tend');
    var btn = doc.querySelector('.greenline-tend-btn');
    if (missing.length) {
      if (btn) btn.remove();
      return 'tend(' + missing.join(',') + ')';
    }
    toggleClass(panel, 'greenline-patrol', hasPatrol());
    if (!hasPatrol() || !(potsData.pendingChores > 0)) {
      if (btn) btn.remove();
      return '';
    }
    var row = doc.querySelector(FEATURES.tend[1]);
    var chevron = doc.querySelector(FEATURES.tend[2]);
    if (!btn) {
      btn = doc.createElement('button');
      btn.type = 'button';
      btn.className = 'greenline-tend-btn';
      btn.setAttribute('data-interactive', '');
      btn.addEventListener('click', function (e) {
        e.stopPropagation();
        window.postMessage({ type: 'PLANT_CHORE_BATCH', data: {}, sourcePageId: 'CoreUI1' }, '*');
      });
    }
    if (btn.parentNode !== row || btn.nextSibling !== chevron) row.insertBefore(btn, chevron);
    var text = word('tendAll');
    if (btn.textContent !== text) btn.textContent = text;
    return '';
  }

  // The Auto-replant line: the last grid line of the expanded list, with a checkbox for the game's
  // own Replant value (state.matureReplantOn, saved by the game). A click posts the message of the
  // game's own header checkbox.
  function applyReplant(doc, popover, line) {
    var el = popover.querySelector('.greenline-replant');
    if (!hasPatrol()) {
      if (el) el.remove();
      return;
    }
    if (!el) {
      el = doc.createElement('label');
      el.className = 'greenline-replant has-entry-tip';
      el.setAttribute('data-interactive', '');
      var box = doc.createElement('input');
      box.type = 'checkbox';
      box.addEventListener('click', function (e) { e.stopPropagation(); });
      box.addEventListener('change', function (e) {
        window.postMessage({ type: 'PLANT_CHORE_REPLANT_TOGGLE', data: { on: e.target.checked }, sourcePageId: 'CoreUI1' }, '*');
      });
      el.appendChild(box);
      var text = doc.createElement('span');
      text.className = 'greenline-replant-text';
      el.appendChild(text);
      var tip = doc.createElement('div');
      tip.className = 'entry-tip greenline-tip';
      el.appendChild(tip);
      popover.appendChild(el);
    }
    place(el, String(line), '1 / -1');
    var label = el.querySelector('.greenline-replant-text');
    if (label.textContent !== word('autoReplant')) label.textContent = word('autoReplant');
    var tipEl = el.querySelector('.entry-tip');
    if (tipEl.textContent !== word('autoReplantTip')) tipEl.textContent = word('autoReplantTip');
  }

  // The checkbox follows the game value. `checked` is a property, so this writes no DOM attribute.
  function updateReplant(doc, state) {
    var box = doc.querySelector('.greenline-replant input');
    var on = !!(state && state.matureReplantOn);
    if (box && box.checked !== on) box.checked = on;
  }
