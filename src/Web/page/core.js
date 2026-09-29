  // The pot data, the words, and the helpers that the other files share.

  var FEATURES = {
    grid: ['.mature-popover', '.mature-item-row'],
    tend: ['.mature-panel', '.mature-toggle-row', '.mature-toggle-chevron']
  };

  // The English words, used when the pot data gives no word for a key.
  var DEFAULT_WORDS = {
    stateGrowing: 'Growing',
    stateMature: 'Mature',
    stateWithered: 'Withered',
    statePoor: 'Poor',
    stateStalled: 'Stalled',
    stateEmpty: 'Empty',
    problemPest: 'Pest',
    problemFrost: 'Frost',
    problemLightLow: 'Low Light',
    problemLightHigh: 'Light Too Strong',
    problemWeed: 'Weed',
    problemDrought: 'Drought',
    problemNeedFert: 'Needs Fertilizer',
    labelState: 'State',
    labelGrowth: 'Growth',
    labelHarvest: 'Harvest',
    labelFertilizer: 'Fertilizer',
    valueNone: 'None',
    autoOn: '(auto: on)',
    autoOff: '(auto: off)',
    valueOn: 'On',
    valueOff: 'Off',
    tendAll: 'Tend All',
    autoReplant: 'Auto-replant',
    autoReplantTip: 'After a harvest, Tend All plants the same crop again, with no planting window.'
  };

  // The item icon of Basic Fertilizer: the game has no icon for Needs Fertilizer, so the badge and the
  // card line of that problem use it.
  var FERT_ICON = '../../Res/Material/UI_Item_Icon_Mat_S_1500710801.png';

  // Set by setPots(); null until C# pushes pot data, and then the grid stays off.
  var potsData = null;

  function word(key) {
    var words = potsData && potsData.words;
    if (words && typeof words[key] === 'string' && words[key]) return words[key];
    return DEFAULT_WORDS[key] || '';
  }

  function partsMissing(doc, feature) {
    var parts = FEATURES[feature], missing = [];
    for (var p = 0; p < parts.length; p++) if (!doc.querySelector(parts[p])) missing.push(parts[p]);
    return missing;
  }

  // The popover (and its rows) renders only while the game shows the plant list and it is expanded.
  function popoverShouldRender(state) {
    return !!(state && state.maturePlantsActive && state.maturePlants && state.maturePlants.length > 0
      && state.maturePlantsExpanded);
  }

  // Writes one inline style property only when its value differs: the body observer and the
  // panel's own observers see every write, even of an identical value.
  function setStyle(el, prop, value) {
    if (el.style.getPropertyValue(prop) !== value) el.style.setProperty(prop, value);
  }

  function panelShouldRender(state) {
    return !!(state && state.maturePlantsActive && state.maturePlants && state.maturePlants.length > 0);
  }

  function hasPatrol() { return !!(potsData && potsData.patrol); }

  function pad2(n) { return n < 10 ? '0' + n : String(n); }

  // The countdown under a cell: days and hours from 1 day, hours and minutes under 1 day, and minutes
  // under 1 hour.
  function shortTime(seconds, lang) {
    var total = Math.max(0, Math.floor(seconds));
    var days = Math.floor(total / 86400), hours = Math.floor(total % 86400 / 3600), mins = Math.floor(total % 3600 / 60);
    var zh = lang === 'zh';
    if (days > 0) return zh ? days + '天' + hours + '时' : days + 'd' + hours + 'h';
    if (hours > 0) return hours + ':' + pad2(mins);
    return zh ? mins + '分' : mins + 'm';
  }

  // The countdown of the hover card, with no part before the first one that is not zero.
  function fullTime(seconds, lang) {
    var total = Math.max(0, Math.floor(seconds));
    var days = Math.floor(total / 86400), hours = Math.floor(total % 86400 / 3600), mins = Math.floor(total % 3600 / 60);
    var zh = lang === 'zh';
    var parts = [];
    if (days > 0) parts.push(zh ? days + '天' : days + ' d');
    if (days > 0 || hours > 0) parts.push(zh ? hours + '小时' : hours + ' h');
    parts.push(zh ? mins + '分' : mins + ' min');
    return parts.join(zh ? '' : ' ');
  }

  // The text of a card bar: at most two groups, so no minutes when a day or more is left.
  function barTime(seconds, lang) {
    var total = Math.max(0, Math.floor(seconds));
    if (total < 86400) return fullTime(total, lang);
    var days = Math.floor(total / 86400), hours = Math.floor(total % 86400 / 3600);
    return lang === 'zh' ? days + '天' + hours + '小时' : days + ' d ' + hours + ' h';
  }

  // The game statuses of a row that the game counts down (plantHasCountdown of CoreUI1): growing,
  // mature, Pest, and Frost. Weed, Drought, and Low Light stop the growth.
  function hasCountdown(status) {
    return status === 0 || status === 1 || status === 3 || status === 4;
  }

  // A mature crop with no harvest window (a research crop, harvestTotalSeconds 0): the game shows only
  // the Harvest word, with no time.
  function noHarvestWindow(row) {
    return row.status === 1 && !(row.harvestTotalSeconds > 0);
  }

  function rowsById(state) {
    var rows = {};
    var list = (state && state.maturePlants) || [];
    for (var i = 0; i < list.length; i++) rows[String(list[i].showInstanceId)] = list[i];
    return rows;
  }

  function toggleClass(el, cls, on) {
    if (el.classList.contains(cls) !== on) el.classList.toggle(cls, on);
  }

  // Keeps one <img> of the class in the cell with the source, or none when the source is empty.
  function setImg(doc, el, cls, src) {
    var img = el.querySelector('img.' + cls);
    if (!src) {
      if (img) img.remove();
      return;
    }
    if (!img) {
      img = doc.createElement('img');
      img.className = cls;
      el.appendChild(img);
    }
    if (img.getAttribute('src') !== src) img.setAttribute('src', src);
  }

  function hasCrop(pot) {
    return pot.state === 'growing' || pot.state === 'mature' || pot.state === 'withered';
  }

  // Pest or Frost kills the crop, except a research crop, which never withers.
  function isUrgent(pot) {
    var problems = pot.problems || [];
    return hasCrop(pot) && pot.state !== 'withered' && !pot.neverWithers
      && (problems.indexOf('pest') >= 0 || problems.indexOf('frost') >= 0);
  }

  // A growing crop with a problem that only stops its growth (Weed, Drought, Low Light, Needs
  // Fertilizer, and Pest or Frost of a research crop): the game keeps no timer for these, so the cell
  // shows the pause mark in place of the countdown.
  function isStalled(pot) {
    return !!pot && pot.state === 'growing' && !!pot.badge && !isUrgent(pot);
  }
