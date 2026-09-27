  // The pot card: its model from the pot data and the game row, and its one node.

  var MATURE_ICON = '../../Res/PlantAnomaly/MatureReady.png';

  // The action icon of Plant (action 1610), for a growing crop.
  var GROWING_ICON = '../../Res/icon/icon_line_nature_18.png';

  // The item icon of Basic Fertilizer, for the fertilizer lines with no fertilizer of their own.
  var FERT_ICON = '../../Res/Material/UI_Item_Icon_Mat_S_1500710801.png';

  var PROBLEM_LINES = {
    pest: ['problemPest', '../../Res/PlantAnomaly/Pest.png', 3],
    frost: ['problemFrost', '../../Res/PlantAnomaly/Frost.png', 4],
    drought: ['problemDrought', '../../Res/PlantAnomaly/Drought.png', 0],
    weed: ['problemWeed', '../../Res/PlantAnomaly/Weed.png', 0],
    lightLow: ['problemLightLow', '../../Res/PlantAnomaly/LightLow.png', 0],
    // No game icon exists for too strong light; the light icon stands for both light problems.
    lightHigh: ['problemLightHigh', '../../Res/PlantAnomaly/LightLow.png', 0]
  };

  var STATE_LINES = {
    growing: ['stateGrowing', GROWING_ICON],
    mature: ['stateMature', MATURE_ICON],
    withered: ['stateWithered', '../../Res/PlantAnomaly/Withered.png'],
    poor: ['statePoor', TILL_ICON],
    empty: ['stateEmpty', '']
  };

  // A bar fill from 0 to 1, in whole percent, so the card changes only when the bar visibly moves.
  function fillOf(x) {
    return Math.round(Math.max(0, Math.min(1, x)) * 100) / 100;
  }

  // A game time word ('Harvest', '收获 ') trimmed, or the fallback.
  function gameWord(text, fallback) {
    var w = (text || '').trim();
    return w || fallback;
  }

  // The hover card: a header with the crop (or the pot) and the pot under it, then key rows.
  // Each row is { key, icon, text, cls, bar }; a bar is { fill (0..1), kind, text, pause }. The game
  // row gives the live times (the decay of Pest and Frost, the harvest window, and the growth time of a
  // crop with no problem); the pot data gives the still growth time of a crop with a problem.
  function cardModel(pot, row, state) {
    var lang = potsData.lang;
    var status = row ? row.status : -1;
    var remain = row ? row.remainGameSeconds : 0;
    var crop = hasCrop(pot);
    var model = {
      head: crop
        ? { icon: pot.cropIcon, name: pot.cropName, size: pot.cropSize, sub: pot.potName, subIcon: pot.potIcon, subSize: pot.potSize }
        : { icon: pot.potIcon, name: pot.potName, size: pot.potSize, sub: '', subIcon: '', subSize: 0 },
      rows: []
    };
    var rows = model.rows, keyState = word('labelState');

    var problems = (pot.state === 'growing' || pot.state === 'mature') ? (pot.problems || []) : [];
    var shown = 0;
    for (var i = 0; i < problems.length; i++) {
      var p = PROBLEM_LINES[problems[i]];
      if (!p) continue;
      var text = word(p[0]);
      if (p[2] && p[2] === status) text += '  ' + fullTime(remain, lang);
      rows.push({ key: keyState, icon: p[1], text: text, cls: 'greenline-red' });
      shown++;
    }
    // An empty pot needs no State row: its header already shows the pot, and nothing waits on it.
    if (!shown && pot.state !== 'empty') {
      var st = STATE_LINES[pot.state] || STATE_LINES.empty;
      rows.push({
        key: keyState, icon: st[1] || '', text: word(st[0]),
        cls: pot.state === 'mature' ? 'greenline-gold' : ''
      });
    }

    // The growth time runs from the game row only while the pot data and the row both say the crop has
    // no problem. With a problem in the pot data, the still time of the pot data shows; with no problem
    // in the pot data but a problem in the row (the next push brings it), no bar shows.
    var left = -1;
    if (problems.length) left = pot.growRemainSeconds || 0;
    else if (status === 0) left = remain;
    if (pot.state === 'growing' && pot.growTotalSeconds > 0 && left >= 0) {
      rows.push({
        key: word('labelGrowth'),
        bar: {
          fill: fillOf(1 - left / pot.growTotalSeconds),
          kind: problems.length ? 'greenline-stalled' : '', text: barTime(left, lang), pause: problems.length > 0
        }
      });
    }
    if (pot.state === 'mature' && status === 1) {
      var total = row.harvestTotalSeconds || 0;
      rows.push({
        key: gameWord(state && state.i18n_plantHarvest, word('labelHarvest')),
        bar: { fill: total > 0 ? fillOf(remain / total) : 1, kind: 'greenline-ripe', text: barTime(remain, lang), pause: false }
      });
    }

    // Fertilizer: the name (a grey None with no icon), then the auto value, white when on, grey when off.
    rows.push({
      key: word('labelFertilizer'), icon: pot.fertName ? (pot.fertIcon || '') : '', cls: '',
      parts: [
        { text: pot.fertName || word('valueNone'), cls: 'greenline-fert-name' + (pot.fertName ? '' : ' greenline-muted') },
        { text: word(pot.autoFert ? 'autoOn' : 'autoOff'), cls: 'greenline-fert-auto' + (pot.autoFert ? '' : ' greenline-muted') }
      ]
    });
    rows.push({
      key: word('autoReplant'), icon: '', text: word(pot.autoReplant === false ? 'valueOff' : 'valueOn'),
      cls: pot.autoReplant === false ? 'greenline-muted' : ''
    });
    return model;
  }

  function sizeChip(doc, size) {
    var chip = doc.createElement('span');
    chip.className = 'greenline-chip';
    var cells = doc.createElement('span');
    cells.className = 'cells s' + size;
    for (var n = 0; n < size; n++) {
      var sq = doc.createElement('span');
      sq.className = 'cell';
      cells.appendChild(sq);
    }
    chip.appendChild(cells);
    return chip;
  }

  // A header line: the name and its size chip. The pot line under a crop (with an icon) is one pill of
  // the pot icon, the name, and the size chip, so the pot reads as one tag.
  function namedLine(doc, cls, text, size, icon) {
    var line = doc.createElement('span');
    line.className = cls;
    var box = line;
    if (icon) {
      box = doc.createElement('span');
      box.className = 'greenline-card-pill';
      line.appendChild(box);
      var img = doc.createElement('img');
      img.setAttribute('src', icon);
      box.appendChild(img);
    }
    var t = doc.createElement('span');
    t.className = 'greenline-card-text';
    t.textContent = text || '';
    box.appendChild(t);
    if (size > 0) box.appendChild(sizeChip(doc, size));
    return line;
  }

  function barEl(doc, bar) {
    var el = doc.createElement('span');
    el.className = 'greenline-bar' + (bar.kind ? ' ' + bar.kind : '');
    var fill = doc.createElement('span');
    fill.className = 'greenline-bar-fill';
    fill.style.width = Math.round(bar.fill * 100) + '%';
    el.appendChild(fill);
    var label = doc.createElement('span');
    label.className = 'greenline-bar-text';
    if (bar.pause) {
      var mark = doc.createElement('span');
      mark.className = 'greenline-pause';
      mark.appendChild(doc.createElement('i'));
      mark.appendChild(doc.createElement('i'));
      label.appendChild(mark);
    }
    label.appendChild(doc.createTextNode(bar.text));
    el.appendChild(label);
    return el;
  }

  // One card shows at a time, so the frame keeps one pot card node, in the card node next to the
  // pointer, and only it is built. It is built again only when
  // its model changes (at most once each game minute), so the frame step does not write the DOM on each
  // frame.
  function setCard(doc, el, model) {
    var tip = doc.querySelector('.greenline-pot-card');
    if (!tip) {
      tip = doc.createElement('div');
      tip.className = 'entry-tip greenline-tip greenline-pot-card';
    }
    if (!el.classList.contains('has-entry-tip')) el.classList.add('has-entry-tip');
    if (tip.parentNode !== el) el.appendChild(tip);
    var key = JSON.stringify(model);
    if (tip.__greenlineCard === key) return;
    tip.__greenlineCard = key;
    toggleClass(tip, 'greenline-zh', potsData.lang === 'zh');
    while (tip.firstChild) tip.removeChild(tip.firstChild);

    var head = doc.createElement('div');
    head.className = 'greenline-card-head';
    // The large icon sits in the game's icon slot (the icon-wrapper of the planting window).
    var slot = doc.createElement('span');
    slot.className = 'greenline-card-slot';
    var img = doc.createElement('img');
    if (model.head.icon) img.setAttribute('src', model.head.icon);
    slot.appendChild(img);
    head.appendChild(slot);
    var names = doc.createElement('span');
    names.className = 'greenline-card-names' + (model.head.sub ? '' : ' greenline-card-one');
    names.appendChild(namedLine(doc, 'greenline-card-name', model.head.name, model.head.size));
    if (model.head.sub) names.appendChild(namedLine(doc, 'greenline-card-sub', model.head.sub, model.head.subSize, model.head.subIcon));
    head.appendChild(names);
    tip.appendChild(head);

    var grid = doc.createElement('div');
    grid.className = 'greenline-card-rows';
    for (var i = 0; i < model.rows.length; i++) {
      var r = model.rows[i];
      var rowEl = doc.createElement('div');
      rowEl.className = 'greenline-card-row';
      var k = doc.createElement('span');
      k.className = 'greenline-card-key';
      k.textContent = r.key;
      rowEl.appendChild(k);
      var v = doc.createElement('span');
      v.className = 'greenline-card-value' + (r.cls ? ' ' + r.cls : '');
      if (r.bar) {
        v.appendChild(barEl(doc, r.bar));
      } else {
        if (r.icon) {
          var icon = doc.createElement('img');
          icon.setAttribute('src', r.icon);
          v.appendChild(icon);
        }
        if (r.parts) {
          for (var n = 0; n < r.parts.length; n++) {
            var part = doc.createElement('span');
            part.className = r.parts[n].cls;
            part.textContent = r.parts[n].text;
            v.appendChild(part);
          }
        } else {
          v.appendChild(doc.createTextNode(r.text));
        }
      }
      rowEl.appendChild(v);
      grid.appendChild(rowEl);
    }
    tip.appendChild(grid);
  }
