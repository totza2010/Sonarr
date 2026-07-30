import React, { ReactElement } from 'react';

// Flags, keyed by ISO 3166 country code - languageCountries.ts is what says which language wears
// which. Drawn here rather than pulled from a flag package because a few hundred lines of shapes are
// cheaper than a dependency this fork would have to carry through every upstream merge, and because
// emoji flags are not an option: Chrome on Windows has no glyphs for them and falls back to the
// letters, which are already on the chip.
//
// Every country any language in the map points at is here, so no language shows as a bare code.
//
// They are deliberately simplified. Nothing here is drawn wider than 21 pixels, where a coat of arms
// is a smudge and the points of a star cannot be counted, so emblems are reduced to the shape and
// colour that survive at that size and inscriptions are left off entirely. These are made to be
// recognised at a glance beside their own two-letter code, not to be accurate heraldry.

const STAR =
  '0,-1 0.224,-0.309 0.951,-0.309 0.363,0.118 0.588,0.809 0,0.382 -0.588,0.809 -0.363,0.118 -0.951,-0.309 -0.224,-0.309';

// Equal horizontal bands, top to bottom - the shape most flags in the world are.
function bandsH(...colors: string[]) {
  const height = 4 / colors.length;

  return (
    <>
      {colors.map((color, index) => (
        <rect
          key={index}
          y={height * index}
          width="6"
          height={height}
          fill={color}
        />
      ))}
    </>
  );
}

// Equal vertical bands, left to right.
function bandsV(...colors: string[]) {
  const width = 6 / colors.length;

  return (
    <>
      {colors.map((color, index) => (
        <rect
          key={index}
          x={width * index}
          width={width}
          height="4"
          fill={color}
        />
      ))}
    </>
  );
}

// The Nordic cross, offset towards the hoist, optionally with a second colour inside it.
function nordic(background: string, cross: string, inner?: string) {
  return (
    <>
      <rect width="6" height="4" fill={background} />
      <rect x="1.55" width="0.9" height="4" fill={cross} />
      <rect y="1.55" width="6" height="0.9" fill={cross} />
      {inner ? (
        <>
          <rect x="1.8" width="0.4" height="4" fill={inner} />
          <rect y="1.8" width="6" height="0.4" fill={inner} />
        </>
      ) : null}
    </>
  );
}

// A crescent, cut out of a disc by a second disc in the field's own colour.
function crescent(
  field: string,
  mark: string,
  cx: number,
  cy: number,
  r: number
) {
  return (
    <>
      <circle cx={cx} cy={cy} r={r} fill={mark} />
      <circle cx={cx + r * 0.32} cy={cy} r={r * 0.8} fill={field} />
    </>
  );
}

const flags: Record<string, ReactElement> = {
  AD: bandsV('#10069f', '#fedf00', '#d0103a'),
  AL: (
    <>
      <rect width="6" height="4" fill="#e41e20" />
      <path d="M2 1.4 3 1.9l1-.5-.55 1.2H2.55z" fill="#000" />
    </>
  ),
  BA: (
    <>
      <rect width="6" height="4" fill="#002395" />
      <path d="M1.9 0h2.6L1.9 4z" fill="#fecb00" />
      <circle cx="2.1" cy="0.7" r="0.16" fill="#fff" />
      <circle cx="1.6" cy="1.7" r="0.16" fill="#fff" />
      <circle cx="1.1" cy="2.7" r="0.16" fill="#fff" />
    </>
  ),
  BD: (
    <>
      <rect width="6" height="4" fill="#006a4e" />
      <circle cx="2.7" cy="2" r="1.1" fill="#f42a41" />
    </>
  ),
  BE: bandsV('#000', '#fae042', '#ed2939'),
  BG: bandsH('#fff', '#00966e', '#d62612'),
  BO: bandsH('#d52b1e', '#f9e300', '#007934'),
  CA: (
    <>
      <rect width="6" height="4" fill="#fff" />
      <rect width="1.5" height="4" fill="#d80621" />
      <rect x="4.5" width="1.5" height="4" fill="#d80621" />
      <path
        d="M3 0.7 3.25 1.5 3.9 1.2 3.6 2 4.2 2.1 3.55 2.6 3.7 3 3.05 2.85 3 3.5 2.95 2.85 2.3 3 2.45 2.6 1.8 2.1 2.4 2 2.1 1.2 2.75 1.5z"
        fill="#d80621"
      />
    </>
  ),
  CD: (
    <>
      <rect width="6" height="4" fill="#007fff" />
      <path d="M0 3.4 5 0h1v.6L1 4H0z" fill="#f7d618" />
      <path d="M0 3.6 5.2 0h.4L.4 4H0z" fill="#ce1021" />
      <polygon
        points={STAR}
        fill="#f7d618"
        transform="translate(0.9 0.9) scale(0.6)"
      />
    </>
  ),
  CN: (
    <>
      <rect width="6" height="4" fill="#de2910" />
      <polygon
        points={STAR}
        fill="#ffde00"
        transform="translate(1.4 1.2) scale(0.8)"
      />
    </>
  ),
  CZ: (
    <>
      <rect width="6" height="2" fill="#fff" />
      <rect y="2" width="6" height="2" fill="#d7141a" />
      <path d="M0 0 3 2 0 4z" fill="#11457e" />
    </>
  ),
  DE: bandsH('#000', '#dd0000', '#ffce00'),
  DK: nordic('#c8102e', '#fff'),
  EE: bandsH('#0072ce', '#000', '#fff'),
  EG: bandsH('#ce1126', '#fff', '#000'),
  ES: (
    <>
      <rect width="6" height="4" fill="#aa151b" />
      <rect y="1" width="6" height="2" fill="#f1bf00" />
    </>
  ),
  ET: (
    <>
      {bandsH('#078930', '#fcdd09', '#da121a')}
      <circle cx="3" cy="2" r="1" fill="#0f47af" />
      <polygon
        points={STAR}
        fill="#fcdd09"
        transform="translate(3 2) scale(0.7)"
      />
    </>
  ),
  FI: nordic('#fff', '#003580'),
  FR: bandsV('#002395', '#fff', '#ed2939'),
  GB: (
    <>
      <rect width="6" height="4" fill="#012169" />
      <path d="M0 0 6 4M6 0 0 4" stroke="#fff" strokeWidth="0.8" />
      <path d="M0 0 6 4M6 0 0 4" stroke="#c8102e" strokeWidth="0.45" />
      <path d="M3 0v4M0 2h6" stroke="#fff" strokeWidth="1.3" />
      <path d="M3 0v4M0 2h6" stroke="#c8102e" strokeWidth="0.8" />
    </>
  ),
  GE: (
    <>
      <rect width="6" height="4" fill="#fff" />
      <path d="M2.6 0h0.8v4h-0.8z" fill="#f00" />
      <path d="M0 1.6h6v0.8H0z" fill="#f00" />
      <path d="M1 0.5h0.5v0.9H1z M0.75 0.75h1v0.4h-1z" fill="#f00" />
      <path d="M4.5 0.5h0.5v0.9h-0.5z M4.25 0.75h1v0.4h-1z" fill="#f00" />
      <path d="M1 2.6h0.5v0.9H1z M0.75 2.85h1v0.4h-1z" fill="#f00" />
      <path d="M4.5 2.6h0.5v0.9h-0.5z M4.25 2.85h1v0.4h-1z" fill="#f00" />
    </>
  ),
  GR: (
    <>
      {bandsH(
        '#0d5eaf',
        '#fff',
        '#0d5eaf',
        '#fff',
        '#0d5eaf',
        '#fff',
        '#0d5eaf',
        '#fff',
        '#0d5eaf'
      )}
      <rect width="2.22" height="2.22" fill="#0d5eaf" />
      <path d="M0.89 0h0.44v2.22H0.89z M0 0.89h2.22v0.44H0z" fill="#fff" />
    </>
  ),
  HR: bandsH('#ff0000', '#fff', '#171796'),
  HU: bandsH('#ce2939', '#fff', '#477050'),
  ID: bandsH('#ce1126', '#fff'),
  IE: bandsV('#169b62', '#fff', '#ff883e'),
  IL: (
    <>
      <rect width="6" height="4" fill="#fff" />
      <rect y="0.5" width="6" height="0.45" fill="#0038b8" />
      <rect y="3.05" width="6" height="0.45" fill="#0038b8" />
      <path
        d="M3 1.15 3.8 2.55H2.2z M3 2.85 2.2 1.45h1.6z"
        fill="none"
        stroke="#0038b8"
        strokeWidth="0.16"
      />
    </>
  ),
  IN: (
    <>
      {bandsH('#f93', '#fff', '#128807')}
      <circle
        cx="3"
        cy="2"
        r="0.5"
        fill="none"
        stroke="#008"
        strokeWidth="0.16"
      />
    </>
  ),
  IR: bandsH('#239f40', '#fff', '#da0000'),
  IS: nordic('#02529c', '#fff', '#dc1e35'),
  IT: bandsV('#009246', '#fff', '#ce2b37'),
  JP: (
    <>
      <rect width="6" height="4" fill="#fff" />
      <circle cx="3" cy="2" r="1.2" fill="#bc002d" />
    </>
  ),
  KH: (
    <>
      <rect width="6" height="4" fill="#032ea1" />
      <rect y="1" width="6" height="2" fill="#e00025" />
      <path
        d="M2.4 2.4h1.2v0.3H2.4z M2.6 1.5h0.8v0.9h-0.8z M3 1.2l0.5 0.3h-1z"
        fill="#fff"
      />
    </>
  ),
  KR: (
    <>
      <rect width="6" height="4" fill="#fff" />
      <path d="M2 2a1 1 0 0 1 2 0z" fill="#cd2e3a" />
      <path d="M2 2a1 1 0 0 0 2 0z" fill="#0047a0" />
    </>
  ),
  KZ: (
    <>
      <rect width="6" height="4" fill="#00afca" />
      <circle cx="3" cy="1.9" r="0.7" fill="#fec50c" />
    </>
  ),
  LA: (
    <>
      <rect width="6" height="4" fill="#ce1126" />
      <rect y="1" width="6" height="2" fill="#002868" />
      <circle cx="3" cy="2" r="0.7" fill="#fff" />
    </>
  ),
  LT: bandsH('#fdb913', '#006a44', '#c1272d'),
  LU: bandsH('#ed2939', '#fff', '#00a1de'),
  LV: (
    <>
      <rect width="6" height="4" fill="#9e3039" />
      <rect y="1.6" width="6" height="0.8" fill="#fff" />
    </>
  ),
  MK: (
    <>
      <rect width="6" height="4" fill="#d20000" />
      <path
        d="M3 2 0 0h1.4z M3 2 6 0H4.6z M3 2 0 4h1.4z M3 2 6 4H4.6z M3 2 0 1.7v0.6z M3 2 6 1.7v0.6z"
        fill="#ffe600"
      />
      <circle cx="3" cy="2" r="0.75" fill="#ffe600" />
      <circle cx="3" cy="2" r="0.5" fill="#d20000" />
    </>
  ),
  ML: bandsV('#14b53a', '#fcd116', '#ce1126'),
  MN: (
    <>
      {bandsV('#c4272f', '#015197', '#c4272f')}
      <path d="M0.7 1.2h0.6v1.6h-0.6z M0.55 1h0.9v0.2h-0.9z" fill="#f9cf02" />
    </>
  ),
  MY: (
    <>
      {bandsH(
        '#cc0001',
        '#fff',
        '#cc0001',
        '#fff',
        '#cc0001',
        '#fff',
        '#cc0001'
      )}
      <rect width="3.4" height="2.28" fill="#010066" />
      {crescent('#010066', '#ffcc00', 1.4, 1.15, 0.55)}
      <polygon
        points={STAR}
        fill="#ffcc00"
        transform="translate(2.4 1.15) scale(0.45)"
      />
    </>
  ),
  NL: bandsH('#ae1c28', '#fff', '#21468b'),
  NO: nordic('#ba0c2f', '#fff', '#00205b'),
  PH: (
    <>
      <rect width="6" height="2" fill="#0038a8" />
      <rect y="2" width="6" height="2" fill="#ce1126" />
      <path d="M0 0 2.6 2 0 4z" fill="#fff" />
      <circle cx="0.8" cy="2" r="0.35" fill="#fcd116" />
    </>
  ),
  PK: (
    <>
      <rect width="6" height="4" fill="#01411c" />
      <rect width="1.5" height="4" fill="#fff" />
      {crescent('#01411c', '#fff', 3.6, 2, 0.85)}
      <polygon
        points={STAR}
        fill="#fff"
        transform="translate(4.4 1.3) scale(0.4)"
      />
    </>
  ),
  PL: bandsH('#fff', '#dc143c'),
  PT: (
    <>
      <rect width="6" height="4" fill="#f00" />
      <rect width="2.4" height="4" fill="#060" />
      <circle cx="2.4" cy="2" r="0.8" fill="#ff0" />
    </>
  ),
  RO: bandsV('#002b7f', '#fcd116', '#ce1126'),
  RS: bandsH('#c6363c', '#0c4076', '#fff'),
  RU: bandsH('#fff', '#0039a6', '#d52b1e'),
  SE: nordic('#006aa7', '#fecc00'),
  SI: bandsH('#fff', '#0000c6', '#d50000'),
  SK: bandsH('#fff', '#0b4ea2', '#ee1c25'),
  SN: (
    <>
      {bandsV('#00853f', '#fdef42', '#e31b23')}
      <polygon
        points={STAR}
        fill="#00853f"
        transform="translate(3 2) scale(0.8)"
      />
    </>
  ),
  SO: (
    <>
      <rect width="6" height="4" fill="#4189dd" />
      <polygon
        points={STAR}
        fill="#fff"
        transform="translate(3 2) scale(1.1)"
      />
    </>
  ),
  TH: (
    <>
      <rect width="6" height="4" fill="#a51931" />
      <rect y="0.67" width="6" height="2.66" fill="#f4f5f8" />
      <rect y="1.33" width="6" height="1.34" fill="#2d2a4a" />
    </>
  ),
  TR: (
    <>
      <rect width="6" height="4" fill="#e30a17" />
      {crescent('#e30a17', '#fff', 2.4, 2, 0.85)}
      <polygon
        points={STAR}
        fill="#fff"
        transform="translate(3.6 2) scale(0.45)"
      />
    </>
  ),
  TZ: (
    <>
      <rect width="6" height="4" fill="#1eb53a" />
      <path d="M6 0v4H0z" fill="#00a3dd" />
      <path d="M0 4 6 0v0.55L0.8 4z" fill="#fcd116" />
      <path d="M0 3.45 5.2 0h0.8L0 4z" fill="#000" />
    </>
  ),
  UA: bandsH('#0057b7', '#ffd700'),
  US: (
    <>
      <rect width="6" height="4" fill="#fff" />
      <rect y="0" width="6" height="0.57" fill="#b22234" />
      <rect y="1.14" width="6" height="0.57" fill="#b22234" />
      <rect y="2.29" width="6" height="0.57" fill="#b22234" />
      <rect y="3.43" width="6" height="0.57" fill="#b22234" />
      <rect width="2.4" height="1.71" fill="#3c3b6e" />
    </>
  ),
  VN: (
    <>
      <rect width="6" height="4" fill="#da251d" />
      <polygon
        points={STAR}
        fill="#ff0"
        transform="translate(3 2) scale(1.1)"
      />
    </>
  ),
  ZA: (
    <>
      <rect width="6" height="2" fill="#e03c31" />
      <rect y="2" width="6" height="2" fill="#001489" />
      <path d="M0 0 3 2 0 4z" fill="#fff" />
      <path d="M0 0.55 2.2 2 0 3.45z" fill="#007749" />
      <path d="M0 0.9 1.7 2 0 3.1z" fill="#000" />
      <path d="M2.05 1.5h3.95v1H2.05z" fill="#007749" />
    </>
  ),
};

export default flags;
