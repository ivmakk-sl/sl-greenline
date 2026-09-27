  // The style node, the apply pass on the CoreUI1 frame, and install() and setPots() for C#.

  // The styles are tokens.css and page.css, which C# sets as window.__greenlineCss before this script.
  function ensureStyle(doc) {
    if (doc.getElementById('greenline-style')) return;
    var style = doc.createElement('style');
    style.id = 'greenline-style';
    style.textContent = window.__greenlineCss || '';
    doc.head.appendChild(style);
  }

  // A change inside a game row (the countdown text) or inside a mod cell (the mod's own countdown and
  // content) needs no new pass. A popover built again (an expand) or a row added or removed does.
  function needsApply(records) {
    for (var i = 0; i < records.length; i++) {
      var el = records[i].target;
      if (el.nodeType !== 1) el = el.parentElement;
      if (el && el.closest && el.closest('.mature-item-row, .greenline-cell, .greenline-replant, .greenline-hover')) continue;
      return true;
    }
    return false;
  }

  // Applies the grid to one CoreUI1 window. Returns the "installed"/"missing:"/"error:" text that
  // install() and setPots() hand back to C#.
  function apply(w) {
    var doc = w.document, state;
    try { state = w.eval('state'); } catch (e) { return 'error: state not reachable (' + e + ')'; }

    ensureStyle(doc);
    watchPointer(w, state);
    var missing = [];
    try {
      var m1 = applyGrid(doc, state); if (m1) missing.push(m1);
      var m2 = applyTend(doc, state); if (m2) missing.push(m2);
    } catch (e) { return 'error: ' + e; }

    if (!w.__greenlineObserver) {
      var observer = new w.MutationObserver(function (records) {
        if (!needsApply(records)) return;
        try {
          ensureStyle(doc);
          var s = w.eval('state');
          applyGrid(doc, s);
          applyTend(doc, s);
        } catch (e) { /* a page update broke a part; the next install() or setPots() call reports it */ }
      });
      // childList only: the game's countdown writes text on each frame, and the mod's own writes of
      // styles and attributes must not start a new pass.
      observer.observe(doc.body, { childList: true, subtree: true });
      w.__greenlineObserver = observer;
    }

    // The page's state is one const object for the life of the window, so the step keeps it. The shown
    // times change at most once each game minute, so 4 steps each second are enough; each part returns
    // at once while nothing of the mod shows.
    if (!w.__greenlineTick) {
      w.__greenlineTick = true;
      w.setInterval(function () {
        try { stepFrame(doc, state); } catch (e) { /* reported by the next install() or setPots() */ }
      }, 250);
    }

    return missing.length ? ('installed; missing: ' + missing.join(', ')) : 'installed';
  }

  // The countdowns of the cells, the Auto-replant value, and the hover card, from the game state.
  function stepFrame(doc, state) {
    updateCountdowns(doc, state);
    updateReplant(doc, state);
    updateHover(doc, state);
  }

  // One step on the CoreUI1 frame, for the page tests.
  function step() {
    var w = findFrame();
    if (w) stepFrame(w.document, w.eval('state'));
  }

  function findFrame() {
    var frames = document.querySelectorAll('iframe');
    for (var i = 0; i < frames.length; i++) {
      var w = frames[i].contentWindow;
      if (!w) continue;
      try { if (!/CoreUI1\.html/i.test(String(w.location))) continue; } catch (e) { continue; }
      if (w.document.readyState !== 'complete') continue;
      return w;
    }
    return null;
  }

  function attempt(retries) {
    var w = findFrame();
    if (!w) {
      if (retries > 0) setTimeout(function () { attempt(retries - 1); }, 300);
      return 'no CoreUI1 frame';
    }
    return apply(w);
  }

  function install() { return attempt(10); }

  function setPots(data) {
    potsData = data || null;
    var w = findFrame();
    if (!w) return 'no CoreUI1 frame';
    return apply(w);
  }
