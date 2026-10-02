# Tekla-Codes

Tools for **Tekla Structures** by Rajeev Pulari:

- **HTML catalog tools** - prepare bolt, nut, stud and washer catalog data in the browser and export it in Tekla's `.bolts` format.
- **Macros** - C# macros that run inside Tekla (file conversion, copy between models, pin creation, path cuts).

## Branches

| Branch | Contents |
|---|---|
| [`main`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/main) | HTML catalog tools + this overview |
| [`Macro`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro) | Tekla macros (`.cs`) with full documentation, plus a copy of the HTML tools |

---

## HTML catalog tools

| Tool | Folder | What it prepares |
|---|---|---|
| [Bolt Manager](Tekla%20Bolt%20Manager/) | `Tekla Bolt Manager/` | Bolt catalog entries |
| [Nut Manager](Tekla%20Nut%20Manager/) | `Tekla Nut Manager/` | Nut catalog entries |
| [Stud Manager](Tekla%20Stud%20manager/) | `Tekla Stud manager/` | Stud catalog entries |
| [Washer Manager](Tekla%20Washer%20manager/) | `Tekla Washer manager/` | Washer catalog entries |

### How to use

1. Download the tool's `index.html` and open it in any web browser (no install, works offline).
2. Enter the part dimensions.
3. Export the data in Tekla's `.bolts` format.
4. Import the exported file into Tekla Structures' bolt catalog.
5. Create the bolt assembly in Tekla separately.

---

## Macros

The macros live on the [`Macro`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro) branch,
where each one is documented in detail.

| Macro | File | What it does | Needs |
|---|---|---|---|
| [XLS to XLSX Converter](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro#xls-to-xlsx-converter) | `XlsToXlsxConverter.cs` | Batch-converts Excel 97-2003 `.xls` files (e.g. Tekla reports) to `.xlsx` | Microsoft Excel |
| [Object Transporter](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro#object-transporter) | `ObjectTransporter.cs` | Copies beams/columns and contour plates from one model and pastes them into another via a base point | - |
| [Dynamic Pin Creator](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro#dynamic-pin-creator) | `DynamicPinCreator.cs` | Creates a round pin with shop-welded end caps between two picked points | Round bar profile `D...` |
| [Interactive Polybeam Cut](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro#interactive-polybeam-cut) | `InteractivePolybeamCut.cs` | Cuts a part with a body that follows other parts' path, with a live preview | `Zero_Density` material |

### Installing a macro (short version)

1. Copy the `.cs` file into your Tekla macro folder - usually
   `C:\ProgramData\Trimble\Tekla Structures\<version>\Environments\common\macros\modeling\`
   (the folder set by the advanced option `XS_MACRO_DIRECTORY`).
2. In Tekla open **Applications & components** (side pane) > **Macros**, refresh, and run the macro.

See the [`Macro` branch README](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro#installing-a-macro) for details.
