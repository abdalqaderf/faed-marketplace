# Image credits

Every photograph used by Faed's **Development-only demo seed**
(`src/Faed.Web/Data/Seed/DemoDataSeeder.cs`) is openly licensed. The originals live in
`tools/demo-images/sources/`; the committed, processed PNGs under
`src/Faed.Web/Data/Seed/Assets/Images/` are produced from them by
`tools/demo-images/generate-demo-images.ps1` (source list and this attribution data:
`tools/demo-images/manifest.json`).

These images ship only for the local demo. They are excluded from `dotnet publish` output
and are never served to real users.

Processing, per `docs/DESIGN-BRIEF.md` §8:

- **Product photos** — background removed with the [remove.bg](https://www.remove.bg) API,
  then composited onto the `--faed-card-bg` card colour (`#E7E5DE`).
- **Defect photos** — background kept; square-cropped only. A pool of six is reused across
  every defect-disclosing listing and is deliberately not matched to the products.

## Product photos

| File | Source photo | Author | Licence |
|---|---|---|---|
| `kettle.png` | [Electric kettle](https://commons.wikimedia.org/w/index.php?curid=10619785) | Schekinov Alexey Victorovich | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) |
| `hand-mixer.png` | [Viking 5-Speed Hand Mixer](https://commons.wikimedia.org/w/index.php?curid=35716892) | Veganbaking.net | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0/) |
| `toaster.png` | [Toaster Toast](https://stocksnap.io/photo/toaster-toast-0UTZ00FWC6) | Łukasz Popardowski (StockSnap) | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| `blender.png` | [Vitamix Blender](https://commons.wikimedia.org/w/index.php?curid=130206127) | Didriks | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0/) |
| `steam-iron.png` | [Cordless steam iron CSI-M701A](https://commons.wikimedia.org/w/index.php?curid=40981566) | Lombroso | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |
| `upright-vacuum.png` | [Sebo Felix 'Fun' Upright Vacuum Cleaner](https://commons.wikimedia.org/w/index.php?curid=140909533) | Jettmuss | [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) |
| `handheld-vacuum.png` | [Fujitek handheld cyclone vacuum cleaner](https://commons.wikimedia.org/w/index.php?curid=51536441) | Mk2010 | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |
| `cordless-drill.png` | [Battery Construction Cordless Drill](https://commons.wikimedia.org/w/index.php?curid=162885317) | Tool Dude8mm | [CC BY 2.0](https://creativecommons.org/licenses/by/2.0/) |
| `circular-saw.png` | [Circular saw – Ozito 2](https://commons.wikimedia.org/w/index.php?curid=74227482) | HutheMeow | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |
| `angle-grinder.png` | [Bosch angle grinder, Norfolk](https://commons.wikimedia.org/w/index.php?curid=81294208) | Kolforn | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) |
| `screwdriver-set.png` | [Precision Screwdriver Set 2](https://commons.wikimedia.org/w/index.php?curid=23322726) | oomlout | [CC BY-SA 2.0](https://creativecommons.org/licenses/by-sa/2.0/) |
| `tool-bag.png` | [Ideal tool bag 2](https://commons.wikimedia.org/w/index.php?curid=32929435) | J.C. Fields | [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) |

## Defect photos

All from [Pexels](https://www.pexels.com), used under the [Pexels License](https://www.pexels.com/license/).

| File | Source photo |
|---|---|
| `defect-01.png` | <https://www.pexels.com/photo/18500278/> — open box with bubble-wrapped contents |
| `defect-02.png` | <https://www.pexels.com/photo/7362843/> — damaged fragile parcel on a ledge |
| `defect-03.png` | <https://www.pexels.com/photo/5497872/> — scratched brushed-metal surface |
| `defect-04.png` | <https://www.pexels.com/photo/4498135/> — opened cardboard box on a workbench |
| `defect-05.png` | <https://www.pexels.com/photo/12903526/> — torn and peeled sheet-metal edge |
| `defect-06.png` | <https://www.pexels.com/photo/7363100/> — damaged fragile parcel on a doorstep |

## Category images

`src/Faed.Web/wwwroot/images/categories/*.png` are original placeholder graphics generated
for this project; no third-party source.
