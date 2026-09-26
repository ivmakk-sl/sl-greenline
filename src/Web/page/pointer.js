  // The card next to the pointer, for a grid cell or a pot in the game world.

  // The pot under the pointer in the game world, as the key of potsData.pots, or ''.
  var hoverPot = '';

  // The last pointer place in the frame, in window pixels (clientX and clientY); the pot of the grid
  // cell under it, or ''; and whether the world is under it: not a HUD part (CoreUI1 marks each with
  // [data-interactive]) and not out of the frame (a game window is another frame).
  var pointer = { x: 0, y: 0, cell: '', world: true };

  var POINTER_GAP = 12;

  // Next to the pointer, as the game's own tips: right of and below it, or on the other side when the
  // card would pass the window edge. The body of CoreUI1 has a CSS zoom (and HUD Scale changes it),
  // which scales the node's left and top, so the pointer place is divided by it.
  function placeHover(w, node) {
    var zoom = node.currentCSSZoom || 1;
    var x = pointer.x / zoom, y = pointer.y / zoom;
    var left = x + POINTER_GAP, top = y + POINTER_GAP;
    if (left + node.offsetWidth > w.innerWidth / zoom) left = x - POINTER_GAP - node.offsetWidth;
    if (top + node.offsetHeight > w.innerHeight / zoom) top = y - POINTER_GAP - node.offsetHeight;
    left = Math.max(0, left);
    top = Math.max(0, top);
    setStyle(node, 'left', Math.round(left) + 'px');
    setStyle(node, 'top', Math.round(top) + 'px');
  }

  function readTarget(el) {
    var cellEl = el && el.closest ? el.closest('.greenline-cell') : null;
    pointer.cell = cellEl ? cellEl.getAttribute('data-pot-id') : '';
    pointer.world = !(el && el.closest && el.closest('[data-interactive]'));
  }

  function watchPointer(w, state) {
    if (w.__greenlinePointer) return;
    w.__greenlinePointer = true;
    var doc = w.document;
    doc.addEventListener('mousemove', function (e) {
      pointer.x = e.clientX;
      pointer.y = e.clientY;
      readTarget(e.target);
      try { updateHover(doc, state); } catch (err) { /* reported by the next setHover() */ }
    }, true);
    doc.addEventListener('mouseout', function (e) {
      if (e.relatedTarget) return;
      pointer.world = false;
      pointer.cell = '';
      try { updateHover(doc, state); } catch (err) { /* reported by the next setHover() */ }
    }, true);
  }

  // The card next to the pointer: one mod node at the end of the frame body, which takes no pointer, so
  // each click still reaches the world. It shows the card of the grid cell under the pointer, or of the
  // world pot under the pointer. It moves with the pointer, so it has the game's data-no-sync marker:
  // CoreUI1 sends the places of its HUD parts to the game after each style change outside such a node.
  function updateHover(doc, state) {
    var node = doc.querySelector('.greenline-hover');
    var pots = potsData && potsData.pots;
    var pot = !pots ? null : pointer.cell ? pots[pointer.cell] : pointer.world && hoverPot ? pots[hoverPot] : null;
    if (!pot) {
      if (node && !node.hidden) node.hidden = true;
      return;
    }
    if (!node) {
      node = doc.createElement('div');
      node.className = 'greenline-hover';
      node.setAttribute('data-no-sync', '');
      doc.body.appendChild(node);
    }
    if (node.hidden) node.hidden = false;
    var rows = rowsById(state);
    setCard(doc, node, cardModel(pot, pot.rowId ? rows[String(pot.rowId)] : null, state));
    placeHover(doc.defaultView, node);
  }

  // What is under a pointer place from C# (fractions of the screen): another frame (a game window or
  // menu) over CoreUI1 in the root page, or the part of CoreUI1 at the place (a cell, a HUD part, or
  // the world).
  function readPlace(w, x, y) {
    if (typeof document.elementFromPoint === 'function') {
      var top = document.elementFromPoint(x * window.innerWidth, y * window.innerHeight);
      if (top && top.tagName === 'IFRAME' && top.contentWindow !== w) {
        pointer.cell = '';
        pointer.world = false;
        return;
      }
    }
    var doc = w.document;
    readTarget(typeof doc.elementFromPoint === 'function' ? doc.elementFromPoint(pointer.x, pointer.y) : null);
  }

  // C# calls this when the pot under the pointer in the game world changes (its PotId, or 0 for none),
  // and when the pointer moves. The game passes pointer moves to the page only over the HUD parts, so
  // C# sends the pointer place, as fractions of the screen from the top-left.
  // Returns 'ok', 'no CoreUI1 frame', or 'error: <text>'; C# turns the world card off on an error.
  function setHover(potId, x, y) {
    try {
      hoverPot = potId ? String(potId) : '';
      var w = findFrame();
      if (!w) return 'no CoreUI1 frame';
      if (typeof x === 'number' && typeof y === 'number') {
        pointer.x = x * w.innerWidth;
        pointer.y = y * w.innerHeight;
        readPlace(w, x, y);
      }
      updateHover(w.document, w.eval('state'));
      return 'ok';
    } catch (e) {
      return 'error: ' + (e && e.message ? e.message : String(e));
    }
  }
