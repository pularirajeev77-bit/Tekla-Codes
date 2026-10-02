# Tekla-Codes - `Macro` branch

C# **macros for Tekla Structures** by Rajeev Pulari. Each macro is a single `.cs`
file: copy it into your macro folder and run it from Tekla - no compiling needed.

Other branch: [`main`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/main) - overview and the HTML catalog tools
(also included here, see [HTML catalog tools](#html-catalog-tools)).

## Contents

| Macro | File | What it does | Needs |
|---|---|---|---|
| [XLS to XLSX Converter](#xls-to-xlsx-converter) | `XlsToXlsxConverter.cs` | Batch-converts Excel 97-2003 `.xls` files (e.g. Tekla reports) to `.xlsx` | Microsoft Excel |
| [Object Transporter](#object-transporter) | `ObjectTransporter.cs` | Copies beams/columns and contour plates from one model and pastes them into another via a base point | Tekla with the newer macro format (see below) |
| [Dynamic Pin Creator](#dynamic-pin-creator) | `DynamicPinCreator.cs` | Creates a round pin with shop-welded end caps between two picked points | Round bar profile `D...` |
| [Interactive Polybeam Cut](#interactive-polybeam-cut) | `InteractivePolybeamCut.cs` | Cuts a part with a body that follows other parts' path, with a live preview | `Zero_Density` material |

Also: [Installing a macro](#installing-a-macro) &middot; [Changelog](#changelog) &middot; [HTML catalog tools](#html-catalog-tools)

---

## Installing a macro

1. Download the `.cs` file.
2. Copy it into a Tekla macro folder:
   - Modelling macros: `...\Environments\common\macros\modeling\`
   - The exact folder is set by the advanced option **`XS_MACRO_DIRECTORY`**
     (e.g. `C:\ProgramData\Trimble\Tekla Structures\<version>\Environments\common\macros`).
3. In Tekla open **Applications & components** (side pane), find **Macros**,
   refresh if needed, and double-click the macro to run it.

**Macro formats.** Three macros use the classic `Tekla.Technology.Akit.UserScript`
format, which works in all recent versions. **Object Transporter** uses the newer
`[MacroEntryPoint]` format (`#pragma reference` lines at the top), which needs a
Tekla version that supports it (recent releases). If Tekla reports a compile
error for it, your version is too old for that format.

**General tips**
- Run macros on a **saved** model; use **Undo** (Ctrl+Z) if a result isn't right.
- Messages and pick prompts appear in Tekla's status bar / dialogs - read them while picking.
- Press **Esc** while picking to cancel; nothing is created when you cancel.

---

## XLS to XLSX Converter

**File:** `XlsToXlsxConverter.cs` &middot; **Needs:** Microsoft Excel installed (it converts through Excel via PowerShell)

Converts one or more **.xls** files (Excel 97-2003, e.g. Tekla report or
Organizer exports) to **.xlsx**, saved next to the originals.

**How to use**
1. Run the macro - a window opens.
2. **Browse...** - pick `.xls` files (multi-select). The dialog opens in the current model folder.
3. Optional: untick **Delete original .xls after a successful conversion** to keep the originals.
4. **Convert to XLSX** - the log shows `OK` / `FAIL` for every file.

| Control | What it does |
|---|---|
| Browse... | Add `.xls` files to the list |
| Remove Selected / Clear All | Edit the list |
| Delete original .xls ... | On (default) = delete each `.xls` only **after** its `.xlsx` is confirmed |
| Convert to XLSX | Run the batch |

**Notes**
- Originals are opened **read-only**; a failed file is never touched.
- Files that fail stay in the list so you can retry; converted files are removed from it.
- An existing `.xlsx` with the same name is **overwritten**.
- The batch stops after **5 minutes**; the log says so. If that happens, check Task
  Manager for a leftover `EXCEL.EXE`.
- "Excel not installed" and missing files are reported in the log.

---

## Object Transporter

**File:** `ObjectTransporter.cs` &middot; **Format:** newer `[MacroEntryPoint]` macro

Copy parts from one Tekla model and paste them into another (or the same) model,
**relative to a base point** - like Ctrl+C / Ctrl+V between models.

**How to use**
1. In the **source** model: run the macro > **Copy** > pick the parts (middle-click
   to finish) > pick a **base point**.
2. Open the **target** model: run the macro > **Paste** > pick the **target point**.
   The parts are recreated, moved from base point to target point.

The "clipboard" is a text file, `%TEMP%\Tekla_Macro_Clipboard.csv`, so it works
between models on the **same PC**. The dialog shows when it was last copied, and
**Paste** stays greyed out until something has been copied. Each Copy replaces the
previous clipboard.

**What is copied**

| Part type | Copied |
|---|---|
| Beams / columns | Start & end point, profile, material, class, name, finish, position (depth / plane / rotation and their offsets) |
| Contour plates | Contour points with chamfers, profile, material, class, name, finish, depth and depth offset |

**Not copied:** polybeams, bent plates and other part types (the message says how
many were skipped), and cuts, fittings, welds, bolts, reinforcement and UDAs.

**Notes**
- Copy and paste work in **global coordinates**, so the result is the same whatever
  work plane is active; your work plane is restored afterwards.
- Profiles and materials must exist in the **target** model's catalogs - parts Tekla
  refuses to create are counted in the result message.

---

## Dynamic Pin Creator

**File:** `DynamicPinCreator.cs` &middot; NickName `UIPin` &middot; **Needs:** round bar profile `D...` in the profile catalog

Creates a **pin with an end cap at each end**, between two picked points.

**How to use**
1. Run the macro and fill in the window:

   | Field | Default | Meaning |
   |---|---|---|
   | Pin Diameter (mm) | 30 | Pin profile `D30` |
   | Cap Diameter (mm) | 60 | Cap profile `D60` |
   | Cap Thickness (mm) | 12 | Length of each cap |
   | Material | S235JR | Material of pin and caps |

2. Click **Pick Points & Create Pin**, then pick the pin's **start** and **end** point.

**What it creates**

| Part | Profile | Class | Name | Prefixes |
|---|---|---|---|---|
| Pin, between the picked points | `D<pin dia>` | 5 | `Ø<dia>_PIN` | part `r-`, assembly `PIN-` |
| 2 caps, outside each end | `D<cap dia>` | 6 | `Ø<dia>_CAP` | part `r-` |

Both caps are **shop-welded** to the pin, so each pin is one `PIN-` assembly.

**Notes**
- Decimals can be typed as `12.5` or `12,5`.
- If any part can't be created (e.g. unknown material), nothing is left behind and the reason is shown.
- A cap not larger than the pin asks for confirmation.

---

## Interactive Polybeam Cut

**File:** `InteractivePolybeamCut.cs` &middot; **Needs:** material `Zero_Density` (or change the setting)

Cuts a **main part** with a box-shaped body that follows the centre-line of one
or more **secondary parts** (beams / polybeams) where they meet the main part -
e.g. a clean cut-out where a member runs into or through another one. The body is
first shown as a **yellow preview** (class 6) that you can reposition.

**How to use**
1. Set **On plane** / **On depth** and their offsets (defaults RIGHT / BEHIND, 0).
2. **1. Select Part to Cut (Main)** - pick the part that gets the cut.
3. **2. Select Parts for Cut (Sec)** - pick the path parts, then middle-click.
4. **3. Preview Profile** - creates the yellow body. Change the position values and
   press **Modify Preview** to move it.
5. **4. Execute Final Cut** - turns the body into a cut on the main part and removes it.

Closing the window without executing removes the preview.

**Settings** (constants at the top of the file)

| Constant | Default | Meaning |
|---|---|---|
| `PROFILE_CLEARANCE` | 150 | Added to the secondary profile's height and width |
| `CUT_LENGTH` | 200 | Path length either side of the point nearest the main part |
| `CUTTING_MATERIAL` | `Zero_Density` | Material of the cut body - must exist in your material catalog |

**Notes**
- The secondary parts should connect end-to-end (within 2 mm); otherwise you get a
  notice and should check the preview.
- If the main part is at the **end** of the path, the body extends past that end into the main part.
- If the cut fails, the body turns back into the yellow preview so you can adjust and retry.

---

## Changelog

**v2.1 - October 2026** (debugged versions of the original macros)

*XLS to XLSX Converter*
- The original `.xls` is deleted only after the `.xlsx` is verified (before: as soon as Excel's SaveAs returned); new option to keep originals.
- Originals opened read-only; failed files stay in the list; unique temp files per run.
- 5-minute timeout now stops PowerShell instead of carrying on mid-conversion; window repaints while waiting.

*Object Transporter*
- Fixed a crash on paste: the field-count checks were one short for beams (12 vs 13) and plates (5 vs 6).
- Works in global coordinates (a different work plane used to misplace parts).
- Cancelling the base-point pick cancels the copy (used to fall back to 0,0,0).
- Now also copies chamfers, name, finish and position offsets; old clipboards still paste.
- One bad row no longer stops the whole paste.

*Dynamic Pin Creator*
- Sizes always written with a dot (`D12.5`); comma-decimal Windows produced `D12,5`, which Tekla rejects.
- Every insert is checked; a failure removes the parts already created (no half pins).
- Same point picked twice, zero sizes and empty material are caught.

*Interactive Polybeam Cut*
- Dialog no longer closes on the first pick (it was hidden with `Hide()`, which ends a modal dialog).
- Path end fix: the body extends past the end of the path instead of folding back on itself.
- Profile written with a dot (`PL250*250.5`); offsets accept `.` or `,`.
- Preview no longer shares (and alters) the secondary part's position data.
- Failed preview / cut are reported; duplicate path points removed; main part can't be a path part.

---

## HTML catalog tools

Standalone browser tools (also on [`main`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/main)):

| Tool | Folder |
|---|---|
| [Bolt Manager](Tekla%20Bolt%20Manager/) | `Tekla Bolt Manager/` |
| [Nut Manager](Tekla%20Nut%20Manager/) | `Tekla Nut Manager/` |
| [Stud Manager](Tekla%20Stud%20manager/) | `Tekla Stud manager/` |
| [Washer Manager](Tekla%20Washer%20manager/) | `Tekla Washer manager/` |

Open a tool's `index.html` in a web browser, enter the part dimensions and export
the data in Tekla's `.bolts` format. Import the file into Tekla Structures' bolt
catalog; create the bolt assembly in Tekla separately.
