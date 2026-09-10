/* Preview-only helper: renders the shared product card so the three grid screens
   (Home, Shop, Merchant storefront) stay in sync without a build step. The card
   markup here is the single source of truth for §5.4 (stamp) + §5.6 (grey photo
   card) + the price treatment. Not part of the app. */

const FAED_GRADES = {
  A: { on: 4, name: "Sealed — new, unopened box" },
  B: { on: 3, name: "Box opened or damaged — item is new and unused" },
  C: { on: 2, name: "Customer return — opened and checked, never used" },
  D: { on: 1, name: "Ex-display — light scratches or marks from showroom use" },
};

const FAED_S = '<svg viewBox="0 0 64 64" fill="none" stroke="#16181A" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">';
const FAED_GLYPH = {
  kettle: FAED_S + '<path d="M14 30h30l-3 20H17z"/><path d="M44 34c6 0 8 4 8 8"/><path d="M22 30l4-8h12l4 8"/><path d="M26 40h10"/></svg>',
  drill: FAED_S + '<path d="M16 22h22v14H16z"/><path d="M38 26h12v6H38z"/><path d="M22 36l-4 12h8l2-12"/><path d="M18 22v-4h10v4"/></svg>',
  vacuum: FAED_S + '<circle cx="24" cy="40" r="12"/><path d="M34 34l14-16"/><path d="M44 14h8v8"/><circle cx="24" cy="40" r="4"/></svg>',
  blender: FAED_S + '<path d="M22 12h20l-3 26H25z"/><path d="M22 42h20v8H22z"/><path d="M28 12l2 20M36 12l-2 20"/></svg>',
  iron: FAED_S + '<path d="M12 40c0-10 12-16 26-16h14l-6 16z"/><path d="M20 46h28"/><path d="M40 24l2-6h8"/></svg>',
  saw: FAED_S + '<circle cx="30" cy="34" r="14"/><path d="M30 34l10-8"/><path d="M14 44h34l6-10"/><path d="M40 20h10v8"/></svg>',
};

function faedStamp(grade, big) {
  const g = FAED_GRADES[grade];
  let bars = "";
  for (let i = 0; i < 4; i++) {
    bars += `<span class="pv-stamp__bar${i < g.on ? " is-on" : ""}"></span>`;
  }
  return `<span class="pv-stamp${big ? " pv-stamp--lg" : ""}">
      <span class="pv-stamp__meter">${bars}</span>
      <span class="pv-stamp__grade">GRADE ${grade}</span>
    </span>`;
}

function faedCard(p, opts = {}) {
  const g = FAED_GRADES[p.grade];
  const was = p.was
    ? `<span class="pv-price__was">${p.was.toFixed(3)} JD</span>
       <span class="pv-price__off">${Math.round((1 - p.now / p.was) * 100)}% off</span>`
    : "";
  return `<article class="pv-card${opts.sm ? " pv-card--sm" : ""}">
    <div class="pv-photo">
      <div class="pv-photo__cutout">${FAED_GLYPH[p.glyph]}</div>
      ${faedStamp(p.grade)}
    </div>
    <div class="pv-card__body">
      <span class="pv-condition-note">${g.name}</span>
      <h3 class="pv-card__title"><a href="listing-detail.html">${p.title}</a></h3>
      <a class="pv-card__merchant" href="storefront.html">${p.shop}</a>
      <div class="pv-price">
        <span class="pv-price__now">${p.now.toFixed(3)} JD</span>
        ${was}
      </div>
      <span class="pv-card__stock">${p.stock}</span>
    </div>
  </article>`;
}

const FAED_PRODUCTS = [
  { title: "Rapid-Boil Electric Kettle", shop: "Amman Kitchen Co.", glyph: "kettle", grade: "A", now: 22.0, stock: "1 available" },
  { title: "Bagless Upright Vacuum", shop: "Amman Kitchen Co.", glyph: "vacuum", grade: "D", now: 74.0, was: 119.0, stock: "1 available" },
  { title: "Countertop Blender", shop: "Amman Kitchen Co.", glyph: "blender", grade: "B", now: 39.0, was: 55.0, stock: "1 available" },
  { title: "Cordless Drill Driver", shop: "Petra Power Tools", glyph: "drill", grade: "B", now: 48.0, was: 69.0, stock: "2 available" },
  { title: "Steam Iron", shop: "Amman Kitchen Co.", glyph: "iron", grade: "C", now: 31.5, stock: "1 available" },
  { title: "Circular Saw", shop: "Petra Power Tools", glyph: "saw", grade: "A", now: 95.0, stock: "1 available" },
  { title: "5-Speed Hand Mixer", shop: "Amman Kitchen Co.", glyph: "blender", grade: "A", now: 27.0, stock: "1 available" },
  { title: "Angle Grinder", shop: "Petra Power Tools", glyph: "saw", grade: "D", now: 41.0, was: 63.0, stock: "1 available" },
  { title: "Handheld Vacuum", shop: "Petra Power Tools", glyph: "vacuum", grade: "C", now: 34.0, stock: "1 available" },
  { title: "2-Slice Toaster", shop: "Amman Kitchen Co.", glyph: "kettle", grade: "B", now: 24.0, was: 33.0, stock: "1 available" },
  { title: "Precision Screwdriver Set", shop: "Petra Power Tools", glyph: "drill", grade: "A", now: 20.0, stock: "3 available" },
  { title: "Canvas Tool Bag", shop: "Petra Power Tools", glyph: "drill", grade: "C", now: 26.0, stock: "1 available" },
];

function faedRenderGrid(id, count, opts) {
  const el = document.getElementById(id);
  if (!el) return;
  el.innerHTML = FAED_PRODUCTS.slice(0, count).map((p) => faedCard(p, opts)).join("");
}
