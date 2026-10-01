# Tekla-Codes - `Macro` branch

Tekla Structures **macros** (C# Akit scripts) and the standalone HTML catalog tools.

Other branches: [`main`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/main) (HTML tools overview)

## Macros

| Macro | File | What it does |
|---|---|---|
| [XLS to XLSX Converter](#xls-to-xlsx-converter) | `XlsToXlsxConverter.cs` | Batch-converts Excel 97-2003 `.xls` files (e.g. Tekla reports) to `.xlsx` |

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

## HTML catalog tools

Standalone HTML tools for preparing bolt catalog data for Tekla Structures:

- [Bolt Manager](Tekla%20Bolt%20Manager/)
- [Nut Manager](Tekla%20Nut%20Manager/)
- [Stud Manager](Tekla%20Stud%20manager/)
- [Washer Manager](Tekla%20Washer%20manager/)

Open a manager's `index.html` file in a web browser, enter the part dimensions,
and export the data in Tekla's `.bolts` format. Import the exported catalog data
into Tekla Structures. Create the bolt assembly in Tekla separately.
