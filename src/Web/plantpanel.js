// Runs in the root page. Adds the pot's two options, Auto-replant and Auto-fertilize, as checkboxes to the
// two windows of a pot: the planting window (PlantPanel), in its header before the close button, and the
// window of a growing crop (PlantingDetails), under the timer. The planting window also gets the line with
// the reason why the replant stopped, above its Start button. The C# side sends the values with setState()
// and then calls install(). A reopened window is a new iframe, so install() finds the frame again each
// time, and it retries every 300 ms for 3 seconds when the frame does not exist yet. A frame observer puts
// the parts back when Vue renders them away.
// install() returns "installed", "no frame", "missing: <page> <parts>", or "error: <text>".
(function () {
  if (window.__greenlinePlant) return;

  // For each window: the page parts that the script needs (a game update that renames one turns the
  // script off for that window), where the checkboxes go, and whether it has the reason line.
  var PAGES = {
    PlantPanel: {
      parts: ['.plant-system h1', '.plant-btn'],
      reason: true,
      // The header row holds the title block and the close button; the checkboxes go between them.
      place: function (doc, group) {
        var header = doc.querySelector('.plant-system h1').parentNode.parentNode;
        var close = header.lastElementChild === group ? group.previousElementSibling : header.lastElementChild;
        if (group.parentNode !== header || group.nextElementSibling !== close) header.insertBefore(group, close);
      }
    },
    PlantingDetails: {
      parts: ['.plant-system .panel'],
      reason: false,
      place: function (doc, group) {
        var panel = doc.querySelector('.plant-system .panel');
        if (panel.lastElementChild !== group) panel.appendChild(group);
      }
    }
  };
  var OPTIONS = [['replant', 'autoReplant', 'Auto-replant'], ['fert', 'autoFert', 'Auto-fertilize']];

  var current = { pot: '', replant: true, fert: false, reason: '', words: {} };

  function word(key, fallback) {
    var w = current.words && current.words[key];
    return typeof w === 'string' && w ? w : fallback;
  }

  // The loaded frames of the two windows, as [page name, document].
  function windowDocs() {
    var frames = document.querySelectorAll('iframe'), found = [];
    for (var i = 0; i < frames.length; i++) {
      var w;
      try { w = frames[i].contentWindow; } catch (e) { continue; }
      var m = w && String(w.location).match(/(PlantPanel|PlantingDetails)\.html/i);
      if (!m) continue;
      var doc = w.document;
      if (!doc || !doc.body || doc.readyState === 'loading') continue;
      found.push([m[1], doc]);
    }
    return found;
  }

  function missingParts(name, doc) {
    var parts = PAGES[name].parts, missing = [];
    for (var p = 0; p < parts.length; p++) if (!doc.querySelector(parts[p])) missing.push(parts[p]);
    return missing;
  }

  function onToggle(e) {
    var key = e.target.parentNode.getAttribute('data-key');
    // A later apply() (the observer rebuilding the group) must show the new value.
    current[key] = !!e.target.checked;
    window.postMessage({ type: 'GREENLINE_POT_OPTION', data: { pot: current.pot, key: key, on: !!e.target.checked }, sourcePageId: 'PlantPanel' }, '*');
  }

  function optionGroup(doc) {
    var group = doc.querySelector('.greenline-options');
    if (group) return group;
    group = doc.createElement('div');
    group.className = 'greenline-options';
    for (var i = 0; i < OPTIONS.length; i++) {
      var row = doc.createElement('label');
      row.className = 'greenline-option';
      row.setAttribute('data-key', OPTIONS[i][0]);
      row.setAttribute('data-interactive', '');
      var box = doc.createElement('input');
      box.type = 'checkbox';
      box.addEventListener('change', onToggle);
      row.appendChild(box);
      row.appendChild(doc.createElement('span'));
      group.appendChild(row);
    }
    return group;
  }

  // Writes the parts of one window, each only when it differs, so a same setState writes nothing.
  function apply(name, doc) {
    if (!doc.getElementById('greenline-plant-style')) {
      var style = doc.createElement('style');
      style.id = 'greenline-plant-style';
      // tokens.css and plantpanel.css, which C# sets as window.__greenlinePlantCss before this script.
      style.textContent = window.__greenlinePlantCss || '';
      doc.head.appendChild(style);
    }

    var group = optionGroup(doc);
    PAGES[name].place(doc, group);
    for (var i = 0; i < OPTIONS.length; i++) {
      var row = group.querySelector('[data-key="' + OPTIONS[i][0] + '"]');
      var text = word(OPTIONS[i][1], OPTIONS[i][2]);
      var label = row.querySelector('span');
      if (label.textContent !== text) label.textContent = text;
      var on = !!current[OPTIONS[i][0]];
      var input = row.querySelector('input');
      if (input.checked !== on) input.checked = on;
    }

    if (!PAGES[name].reason) return;
    var btn = doc.querySelector('.plant-btn');
    var line = doc.querySelector('.greenline-reason');
    if (!current.reason) {
      if (line) line.remove();
      return;
    }
    if (!line) {
      line = doc.createElement('div');
      line.className = 'greenline-reason';
    }
    if (line.nextElementSibling !== btn) btn.parentNode.insertBefore(line, btn);
    if (line.textContent !== current.reason) line.textContent = current.reason;
  }

  function watch(name, doc) {
    if (doc.__greenlineObserver) return;
    var Observer = doc.defaultView && doc.defaultView.MutationObserver;
    if (!Observer) return;
    doc.__greenlineObserver = new Observer(function () {
      if (missingParts(name, doc).length) return;
      var needsReason = PAGES[name].reason && !!current.reason && !doc.querySelector('.greenline-reason');
      if (!doc.querySelector('.greenline-options') || needsReason) apply(name, doc);
    });
    doc.__greenlineObserver.observe(doc.body, { childList: true, subtree: true });
  }

  function attempt(retries) {
    try {
      var docs = windowDocs();
      if (!docs.length) {
        if (retries > 0) setTimeout(function () { attempt(retries - 1); }, 300);
        return 'no frame';
      }
      var missing = [];
      for (var i = 0; i < docs.length; i++) {
        var m = missingParts(docs[i][0], docs[i][1]);
        if (m.length) { missing.push(docs[i][0] + ' ' + m.join(', ')); continue; }
        apply(docs[i][0], docs[i][1]);
        watch(docs[i][0], docs[i][1]);
      }
      return missing.length ? 'missing: ' + missing.join('; ') : 'installed';
    } catch (e) {
      return 'error: ' + (e && e.message ? e.message : String(e));
    }
  }

  window.__greenlinePlant = {
    install: function () { return attempt(10); },
    setState: function (state) {
      state = state || {};
      current = {
        pot: String(state.pot || ''), replant: state.replant !== false, fert: !!state.fert,
        reason: state.reason || '', words: state.words || {}
      };
      var docs = windowDocs();
      for (var i = 0; i < docs.length; i++) if (!missingParts(docs[i][0], docs[i][1]).length) apply(docs[i][0], docs[i][1]);
    }
  };
})();
