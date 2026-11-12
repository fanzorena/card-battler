// Generates the robot part SVGs (Kenney-like flat style: thick dark outline, flat fill, top highlight, bottom shade, neon accents).
// Usage: node Tools/robot-art/gen.js Assets/Resources/RobotParts   (keeps existing .meta GUIDs). Preview: node Tools/robot-art/sheet.js Assets/Resources/RobotParts sheet.html
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const out = process.argv[2];
const OUT = '#0B0D14', SW = 4;
const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16));
const toHex = c => '#' + c.map(v => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('').toUpperCase();
const mix = (a, b, t) => toHex(hex(a).map((v, i) => v + (hex(b)[i] - v) * t));
const MAKER = { Ferrovelho: '#FF9A3D', Bastion: '#C9D35A', Kagemori: '#3DE0FF', Nyx: '#E05CFF' };
const ELEM = { 'Cinético': '#D9DEE8', 'Térmico': '#FF6A3D', 'Elétrico': '#FFE14D', 'Químico': '#7CFF6B' };
const pal = (maker, elem) => {
  const a = MAKER[maker], b = mix('#3A4258', a, 0.35);
  return { a, b, k: mix('#161A26', a, 0.15), m: mix(b, a, 0.4), e: elem ? ELEM[elem] : a };
};
const rot = (r, cx, cy) => r ? ` transform="rotate(${r} ${cx} ${cy})"` : '';

// Panel: base, highlight band, shade band, outline on top.
function panel(cx, cy, w, h, fill, r = 8, rt = 0) {
  const x = cx - w / 2, y = cy - h / 2, hh = Math.min(10, h * 0.22);
  return `<g${rot(rt, cx, cy)}><rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}"/>` +
    (h > 14 ? `<rect x="${x + 5}" y="${y + 4}" width="${w - 10}" height="${hh}" rx="${Math.min(r, hh / 2)}" fill="${mix(fill, '#FFFFFF', 0.2)}"/>` +
      `<rect x="${x + 4}" y="${y + h - 9}" width="${w - 8}" height="5" rx="2.5" fill="${mix(fill, '#000000', 0.3)}"/>` : '') +
    `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="none" stroke="${OUT}" stroke-width="${SW}"/></g>`;
}
const circ = (cx, cy, r, fill, hl = true) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${fill}" stroke="${OUT}" stroke-width="${SW}"/>` +
  (hl && r > 8 ? `<circle cx="${cx - r * 0.35}" cy="${cy - r * 0.35}" r="${r * 0.28}" fill="${mix(fill, '#FFFFFF', 0.3)}"/>` : '');
const glow = (cx, cy, r, c) => `<circle cx="${cx}" cy="${cy}" r="${r * 1.7}" fill="${c}" opacity="0.22"/><circle cx="${cx}" cy="${cy}" r="${r}" fill="${c}" stroke="${OUT}" stroke-width="3"/><circle cx="${cx - r * 0.25}" cy="${cy - r * 0.25}" r="${r * 0.35}" fill="#FFFFFF" opacity="0.85"/>`;
const bar = (cx, cy, w, h, c, r = 3) => `<rect x="${cx - w / 2}" y="${cy - h / 2}" width="${w}" height="${h}" rx="${r}" fill="${c}"/>`;
const neon = (cx, cy, w, h, c) => bar(cx, cy, w + 10, h + 10, c, (h + 10) / 2).replace('/>', ' opacity="0.22"/>') + bar(cx, cy, w, h, c, h / 2) + bar(cx, cy - h * 0.15, w - 6, Math.max(2, h * 0.3), '#FFFFFF', 1).replace('/>', ' opacity="0.7"/>');
const bolt = (cx, cy) => `<circle cx="${cx}" cy="${cy}" r="3.2" fill="${OUT}"/>`;
const poly = (pts, fill) => `<polygon points="${pts.map(p => p.join(',')).join(' ')}" fill="${fill}" stroke="${OUT}" stroke-width="${SW}" stroke-linejoin="round"/>`;
const line = (x1, y1, x2, y2, c, w = 3) => `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${c}" stroke-width="${w}" stroke-linecap="round"/>`;
// Socket where RobotView draws the core (torso-local 90,85).
const socket = p => `<circle cx="90" cy="85" r="50" fill="${p.k}" stroke="${OUT}" stroke-width="${SW}"/><circle cx="90" cy="85" r="42" fill="none" stroke="${mix(p.k, '#FFFFFF', 0.12)}" stroke-width="3"/>`;
const shoulder = p => circ(45, 26, 23, p.b) + bolt(45, 26) + panel(45, 82, 30, 80, p.b, 6) + circ(45, 120, 11, p.k, false);

const parts = {
  // ---- Heads 120x100 ----
  otica_sucata: [120, 100, 'Ferrovelho', null, p =>
    line(26, 40, 26, 14, OUT, 7) + line(26, 40, 26, 14, p.k, 3) + glow(26, 10, 6, p.a) +
    panel(60, 62, 96, 66, p.b, 10) + panel(30, 80, 24, 14, p.k, 3, -8) + bolt(22, 36) + bolt(98, 36) +
    circ(72, 60, 25, p.k, false) + `<circle cx="72" cy="60" r="16" fill="${p.a}" opacity="0.9"/>` + circ(72, 60, 8, OUT, false) + `<circle cx="66" cy="54" r="4" fill="#FFFFFF"/>`],
  sensor_k7: [120, 100, 'Kagemori', 'Elétrico', p =>
    panel(14, 50, 12, 40, p.k, 4, 12) + panel(106, 50, 12, 40, p.k, 4, -12) +
    poly([[48, 32], [60, 14], [72, 32]], p.a) +
    panel(60, 60, 104, 56, p.b, 18) + bar(60, 56, 88, 18, OUT, 9) + neon(60, 56, 78, 8, p.a) + glow(96, 76, 4, p.e)],
  elmo_bastion: [120, 100, 'Bastion', 'Cinético', p =>
    panel(60, 16, 38, 20, p.a, 4) + panel(60, 58, 114, 80, p.b, 16) + bolt(14, 30) + bolt(106, 30) + bolt(14, 86) + bolt(106, 86) +
    bar(60, 50, 82, 14, OUT, 4) + neon(60, 50, 70, 5, p.a) +
    [0, 1, 2, 3].map(i => bar(42 + i * 12, 82, 6, 10, p.k, 2)).join('')],
  coroa_nyx: [120, 100, 'Nyx', 'Químico', p =>
    poly([[24, 40], [30, 6], [44, 32]], p.a) + poly([[50, 28], [60, 2], [70, 28]], p.a) + poly([[76, 32], [90, 6], [96, 40]], p.a) +
    circ(60, 62, 38, p.b) + `<path d="M30 66 Q60 84 90 66" fill="none" stroke="${OUT}" stroke-width="8" stroke-linecap="round"/>` +
    `<path d="M32 66 Q60 82 88 66" fill="none" stroke="${p.a}" stroke-width="3" stroke-linecap="round"/>` + glow(60, 50, 9, p.e)],

  // ---- Torsos 180x165 (core socket at 90,85) ----
  carcaca_remendada: [180, 165, 'Ferrovelho', null, p =>
    panel(90, 84, 164, 146, p.b, 16) + panel(40, 34, 44, 30, p.k, 4, -6) + panel(140, 134, 40, 28, p.k, 4, 8) + panel(150, 36, 26, 22, p.m, 3, 10) +
    [[24, 116, 40, 116], [26, 124, 42, 128], [30, 132, 46, 136]].map(l => line(...l, OUT, 3)).join('') +
    [[18, 18], [162, 18], [18, 150], [162, 150]].map(([x, y]) => circ(x, y, 6, p.a, false)).join('') + socket(p)],
  placa_m2: [180, 165, 'Bastion', 'Cinético', p =>
    panel(90, 84, 176, 158, p.b, 12) + panel(18, 84, 14, 124, p.k, 4) + panel(162, 84, 14, 124, p.k, 4) +
    bar(90, 22, 120, 10, p.a, 3) + bar(90, 148, 120, 10, p.a, 3) +
    [[34, 22], [146, 22], [34, 148], [146, 148]].map(([x, y]) => bolt(x, y)).join('') + socket(p)],
  chassi_aero: [180, 165, 'Kagemori', 'Elétrico', p =>
    poly([[4, 20], [176, 20], [158, 104], [22, 104]], p.b) + poly([[40, 100], [140, 100], [122, 160], [58, 160]], p.k) +
    bar(90, 26, 150, 6, mix(p.b, '#FFFFFF', 0.2), 3) + neon(90, 147, 40, 6, p.a) + neon(20, 40, 18, 5, p.a) + neon(160, 40, 18, 5, p.a) + socket(p)],
  torax_refrigerado: [180, 165, 'Nyx', 'Térmico', p =>
    panel(90, 84, 160, 146, p.b, 18) + neon(18, 84, 8, 112, p.a) + neon(162, 84, 8, 112, p.a) +
    [0, 1, 2].map(i => bar(90, 20 + i * 9, 100, 5, OUT, 2)).join('') +
    socket(p) + glow(150, 145, 5, p.e)],

  // ---- Arms 90x210 (shoulder at top, tool at bottom; the right arm is mirrored in code) ----
  punho_hidraulico: [90, 210, 'Ferrovelho', 'Cinético', p => shoulder(p) +
    panel(45, 130, 42, 12, p.a, 3) + panel(45, 168, 66, 58, p.m, 12) + [0, 1, 2, 3].map(i => bar(24 + i * 14, 192, 10, 10, p.k, 3)).join('') + bolt(26, 152) + bolt(64, 152)],
  serra_circular: [90, 210, 'Ferrovelho', 'Cinético', p => {
    const cx = 45, cy = 164, pts = [];
    for (let i = 0; i < 32; i++) { const r = i % 2 ? 36 : 43, t = i * Math.PI / 16; pts.push([+(cx + r * Math.cos(t)).toFixed(1), +(cy + r * Math.sin(t)).toFixed(1)]); }
    return shoulder(p) + poly(pts, '#C5CCD8') + `<circle cx="${cx}" cy="${cy}" r="26" fill="none" stroke="${p.a}" stroke-width="4"/>` + circ(cx, cy, 14, p.k, false) + bolt(cx, cy);
  }],
  canhao_30mm: [90, 210, 'Bastion', 'Cinético', p => shoulder(p) +
    panel(45, 148, 46, 40, p.b, 8) + panel(45, 182, 24, 46, p.k, 3) + panel(45, 202, 34, 12, p.a, 3) + bar(45, 148, 30, 6, p.a, 2)],
  escudo_braco: [90, 210, 'Bastion', null, p => shoulder(p) +
    poly([[6, 96], [84, 96], [84, 180], [45, 206], [6, 180]], p.m) + poly([[16, 106], [74, 106], [74, 174], [45, 194], [16, 174]], mix(p.m, '#FFFFFF', 0.12)) +
    bar(45, 150, 8, 80, p.a, 3) + bolt(16, 106) + bolt(74, 106)],
  lamina_plasma: [90, 210, 'Kagemori', 'Térmico', p => shoulder(p) +
    panel(45, 134, 56, 12, p.k, 4) + neon(45, 176, 14, 64, p.e) + `<polygon points="38,208 52,208 45,196" fill="${p.e}"/>` + bar(45, 134, 20, 4, p.a, 2)],
  pulso_tesla: [90, 210, 'Kagemori', 'Elétrico', p => shoulder(p) +
    panel(45, 140, 26, 60, p.k, 4) + [0, 1, 2].map(i => `<circle cx="45" cy="${140 + i * 20}" r="21" fill="none" stroke="${OUT}" stroke-width="8"/><circle cx="45" cy="${140 + i * 20}" r="21" fill="none" stroke="${p.e}" stroke-width="4"/>`).join('') +
    glow(45, 194, 8, p.e)],
  injetor: [90, 210, 'Nyx', 'Químico', p => shoulder(p) +
    panel(45, 132, 40, 10, p.a, 3) + panel(45, 162, 30, 52, p.m, 6) + bar(45, 165, 16, 38, p.e, 4) + bar(41, 158, 4, 22, '#FFFFFF', 2).replace('/>', ' opacity="0.6"/>') +
    line(45, 190, 45, 207, OUT, 6) + line(45, 190, 45, 205, '#C5CCD8', 2)],
  lanca_chamas: [90, 210, 'Nyx', 'Térmico', p => shoulder(p) +
    panel(18, 124, 22, 40, p.a, 8) + line(26, 134, 36, 150, OUT, 4) + panel(45, 152, 30, 52, p.k, 4) + bar(45, 176, 40, 8, p.a, 3) +
    `<circle cx="45" cy="196" r="18" fill="${p.e}" opacity="0.3"/><circle cx="45" cy="194" r="10" fill="${p.e}"/><circle cx="45" cy="192" r="5" fill="#FFE14D"/>`],

  // ---- Legs 200x140 ----
  rodas_entrega: [200, 140, 'Ferrovelho', null, p => {
    const wheel = x => circ(x, 100, 30, '#202432', false) + `<circle cx="${x}" cy="100" r="18" fill="${p.k}"/>` +
      [0, 60, 120].map(t => line(x - 16 * Math.cos(t * Math.PI / 180), 100 - 16 * Math.sin(t * Math.PI / 180), x + 16 * Math.cos(t * Math.PI / 180), 100 + 16 * Math.sin(t * Math.PI / 180), p.b, 4)).join('') + circ(x, 100, 8, p.a, false);
    return panel(100, 36, 164, 50, p.b, 10) + bolt(30, 28) + bolt(170, 28) + bar(100, 30, 60, 6, p.a, 3) + panel(52, 70, 14, 30, p.k, 3) + panel(148, 70, 14, 30, p.k, 3) + wheel(52) + wheel(148);
  }],
  esteiras: [200, 140, 'Bastion', 'Cinético', p =>
    panel(100, 28, 140, 40, p.b, 8) + `<rect x="4" y="60" width="192" height="72" rx="36" fill="#202432" stroke="${OUT}" stroke-width="${SW}"/>` +
    Array.from({ length: 11 }, (_, i) => bar(30 + i * 14, 64, 6, 6, '#3A4258', 1) + bar(30 + i * 14, 128, 6, 6, '#3A4258', 1)).join('') +
    [40, 80, 120, 160].map(x => circ(x, 96, 17, p.b, false) + bolt(x, 96)).join('') + neon(100, 54, 120, 5, p.a)],
  pernas_digitigradas: [200, 140, 'Kagemori', null, p =>
    panel(66, 54, 26, 64, p.b, 8, 22) + panel(134, 54, 26, 64, p.b, 8, -22) +
    panel(58, 104, 20, 60, p.k, 6, -20) + panel(142, 104, 20, 60, p.k, 6, 20) +
    circ(74, 80, 10, p.a, false) + circ(126, 80, 10, p.a, false) +
    panel(50, 132, 46, 12, p.a, 4) + panel(150, 132, 46, 12, p.a, 4) + panel(100, 18, 124, 30, p.b, 10)],
  hover: [200, 140, 'Nyx', null, p =>
    `<ellipse cx="100" cy="120" rx="96" ry="20" fill="${p.a}" opacity="0.18"/><ellipse cx="100" cy="116" rx="64" ry="12" fill="${p.a}" opacity="0.35"/><ellipse cx="100" cy="112" rx="34" ry="6" fill="#FFFFFF" opacity="0.6"/>` +
    panel(100, 40, 150, 46, p.b, 14) + panel(100, 82, 176, 26, p.k, 13) + [40, 80, 120, 160].map(x => neon(x, 84, 18, 6, p.a)).join('')],
};

fs.mkdirSync(out, { recursive: true });
for (const [id, [w, h, maker, elem, draw]] of Object.entries(parts)) {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">\n${draw(pal(maker, elem))}\n</svg>\n`;
  fs.writeFileSync(path.join(out, id + '.svg'), svg);
  const meta = path.join(out, id + '.svg.meta');
  const guid = fs.existsSync(meta) ? /guid: (\w+)/.exec(fs.readFileSync(meta, 'utf8'))[1] : crypto.randomBytes(16).toString('hex');
  fs.writeFileSync(meta, fs.readFileSync(path.join(__dirname, 'svg.meta.tpl'), 'utf8').replace('GUID', guid));
}
console.log(Object.keys(parts).length, 'parts');
