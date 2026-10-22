// The sun and the thermometer of the light and temperature line of the game HUD (BT_ICO.sun and BT_ICO.thermo of
// CoreUI1.html), drawn as the HUD draws them: a 24 by 24 stroke icon.
const SUN = '<circle cx="12" cy="12" r="4.2"/><path d="M12 2.6v2.2"/><path d="M12 19.2v2.2"/><path d="m5.2 5.2 1.6 1.6"/>'
  + '<path d="m17.2 17.2 1.6 1.6"/><path d="M2.6 12h2.2"/><path d="M19.2 12h2.2"/><path d="m6.8 17.2-1.6 1.6"/>'
  + '<path d="m18.8 5.2-1.6 1.6"/>';
const THERMO = '<path d="M14 14.76V5a2 2 0 0 0-4 0v9.76a4 4 0 1 0 4 0z"/>';

function icon(doc: Document, cls: string, paths: string): SVGSVGElement {
  const svg = doc.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('class', cls);
  svg.innerHTML = paths;
  return svg;
}

export function sunIcon(doc: Document): SVGSVGElement { return icon(doc, 'greenline-sun', SUN); }

export function thermoIcon(doc: Document): SVGSVGElement { return icon(doc, 'greenline-thermo', THERMO); }
