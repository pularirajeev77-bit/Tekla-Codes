# Tekla-Codes - `Macro` branch

Tekla Structures **macros** (C# Akit scripts) and the standalone HTML catalog tools.

Other branches: [`main`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/main) (HTML tools overview)

## Macros

| Macro | File | What it does |
|---|---|---|
| [XLS to XLSX Converter](#xls-to-xlsx-converter) | `XlsToXlsxConverter.cs` | Batch-converts Excel 97-2003 `.xls` files (e.g. Tekla reports) to `.xlsx` |
| [Object Transporter](#object-transporter) | `ObjectTransporter.cs` | Copies beams/columns and contour plates between models via a base point |

### How to install a macro

1. Copy the `.cs` file into a Tekla macro folder, e.g.
   `C:\ProgramData\Trimble\Tekla Structures\<version>\Environments\common\macros\modeling\`
   (the folder set by `XS_MACRO_DIRECTORY`).
2. In Tekla: **Applications & components > Macros** (or the Applications side pane),
   refresh, and run the macro by its file name.

---

## XLS to XLSX Converter

**File:** `XlsToXlsxConverter.cs`

A small window to pick one or more **.xls** files (it opens in the current model
folder) and convert them to **.xlsx** next to the originals, with a log of every
file.

**Requirements:** Microsoft Excel installed on the PC (the conversion runs through
Excel via PowerShell).

| Control | What it does |
|---|---|
| Browse... | Add `.xls` files (multi-select) |
| Remove Selected / Clear All | Edit the list |
| Delete original .xls ... | On (default) = remove each `.xls` **after** its `.xlsx` is confirmed |
| Convert to XLSX | Run the batch; the log shows OK / FAIL per file |

**Good to know**
- **Safer delete:** the original `.xls` is only deleted once the `.xlsx` exists and
  is not empty. Before, it was deleted as soon as Excel's SaveAs returned. You can
  now also untick the option to keep the originals.
- Originals are opened **read-only**, so a failed conversion can't touch them.
- **Failed files stay in the list** for a retry; converted ones are removed (before,
  the whole list was cleared even when files failed).
- **Timeout handled:** after 5 minutes PowerShell is stopped and the log says so;
  before, the macro carried on and deleted its temp files while the conversion
  was still running. The window keeps repainting while it waits.
- Unique temp file names, so two Tekla sessions running the macro can't collide.
- Missing files and "Excel not installed" are reported clearly in the log.
- An existing `.xlsx` with the same name is overwritten.

---

## Object Transporter

**File:** `ObjectTransporter.cs`

Copy parts from one Tekla model and paste them into another (or the same) model,
**relative to a base point** - like Ctrl+C / Ctrl+V between models.

1. In the **source** model run the macro > **Copy** > pick the parts (middle-click
   to finish) > pick a **base point**.
2. Open the **target** model, run the macro > **Paste** > pick the **target point**.
   The parts are recreated, moved from base point to target point.

The "clipboard" is a text file, `%TEMP%\Tekla_Macro_Clipboard.csv`, so it works
across models on the same PC. The dialog shows when it was last copied, and
**Paste** is greyed out until something has been copied.

**What is copied**

| Part type | Copied |
|---|---|
| Beams / columns | Start & end point, profile, material, class, name, finish, position (depth / plane / rotation **and their offsets**) |
| Contour plates | Contour points **with chamfers**, profile, material, class, name, finish, depth and depth offset |

Polybeams, bent plates and other part types are skipped (the message says how
many). Cuts, fittings, welds, bolts and UDAs are **not** copied.

**Good to know**
- **Crash fix:** paste checked for 12 fields on a beam row but read the 13th, and
  5 fields on a plate row but read the 6th, so a short row crashed the whole paste.
  Bad rows are now counted and skipped; the rest still paste.
- **Work-plane fix:** copy and paste now run in **global** coordinates and restore
  your work plane afterwards. Before, a different work plane in the target model
  put the parts in the wrong place.
- Cancelling the base-point pick now cancels the copy (before it silently used
  `0,0,0`).
- Chamfers, name, finish and position offsets are new; clipboards copied by the
  old version still paste.
- `;` and `|` inside names/profiles are replaced with spaces so they can't break
  the file format.
- Paste reports parts that Tekla refused to create (e.g. an unknown profile in the
  target model's catalog).

---

## HTML catalog tools

Standalone HTML tools for preparing bolt catalog data for Tekla Structures:

- [Bolt Manager](Tekla%20Bolt%20Manager/)
- [Nut Manager](Tekla%20Nut%20Manager/)
- [Stud Manager](Tekla%20Stud%20manager/)
- [Washer Manager](Tekla%20Washer%20manager/)

Open a manager's `index.html` file in a web browser, enter the part dimensions,
and export the data in Tekla's `.bolts` format. Import the exported catalog data
into Tekla Structures. Create the bolt assembly in Tekla separately.
