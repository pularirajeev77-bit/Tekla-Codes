# Tekla-Codes - `Macro` branch

Tekla Structures **macros** (C# Akit scripts) and the standalone HTML catalog tools.

Other branches: [`main`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/main) (HTML tools overview)

## Macros

| Macro | File | What it does |
|---|---|---|
| [XLS to XLSX Converter](#xls-to-xlsx-converter) | `XlsToXlsxConverter.cs` | Batch-converts Excel 97-2003 `.xls` files (e.g. Tekla reports) to `.xlsx` |
| [Object Transporter](#object-transporter) | `ObjectTransporter.cs` | Copies beams/columns and contour plates between models via a base point |
| [Dynamic Pin Creator](#dynamic-pin-creator) | `DynamicPinCreator.cs` | Creates a round pin with welded end caps between two picked points |
| [Interactive Polybeam Cut](#interactive-polybeam-cut) | `InteractivePolybeamCut.cs` | Cuts a part with a body that follows other parts' path, with live preview |

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

## Dynamic Pin Creator

**File:** `DynamicPinCreator.cs`  &middot;  NickName `UIPin`

Enter the pin diameter, cap diameter, cap thickness and material, click
**Pick Points & Create Pin**, then pick the pin's start and end point. It creates:

| Part | Profile | Class | Name | Prefixes |
|---|---|---|---|---|
| Pin (between the picked points) | `D<pin dia>` | 5 | `Ø<dia>_PIN` | part `r-`, assembly `PIN-` |
| 2 caps (outside each end, cap thickness long) | `D<cap dia>` | 6 | `Ø<dia>_CAP` | part `r-` |

Both caps are **shop-welded** to the pin, so the whole pin is one `PIN-` assembly.

**Good to know**
- **Decimal fix:** sizes are written with a dot whatever the Windows number
  format, so `12.5` gives `D12.5` (before, a comma-decimal PC produced `D12,5`,
  which Tekla doesn't recognise). Both `12.5` and `12,5` are accepted in the form.
- **No half pins:** each insert is checked; if anything fails, everything already
  created is deleted again and the reason is shown. Before, a failed cap still got
  welded and a broken pin was left in the model.
- Picking the same point twice is caught (it used to give a zero-length direction).
- Zero/negative sizes and an empty material are rejected; a cap that is not
  larger than the pin asks for confirmation.
- Cancelling the point pick creates nothing.
- Uses the profile catalog's round bar `D...` - make sure your environment has it.

---

## Interactive Polybeam Cut

**File:** `InteractivePolybeamCut.cs`

Cuts a **main part** with a box-shaped body that follows the centre-line of one
or more **secondary parts** (beams / polybeams) where they meet the main part -
e.g. a clean cut-out where a member runs into or through another one. The cut
body is shown first as a **yellow preview** (class 6) you can reposition.

1. Set **On plane** / **On depth** and their offsets (default RIGHT / BEHIND, 0).
2. **Select Part to Cut (Main)** - pick the part that gets the cut.
3. **Select Parts for Cut (Sec)** - pick the path parts, middle-click.
4. **Preview Profile** - creates the yellow body; change the position values and
   press **Modify Preview** to move it.
5. **Execute Final Cut** - turns the body into a cut on the main part and removes it.

Closing the dialog without executing deletes the preview.

**Settings** (constants at the top of the file)

| Constant | Default | Meaning |
|---|---|---|
| `PROFILE_CLEARANCE` | 150 | Added to the secondary profile's height and width |
| `CUT_LENGTH` | 200 | Path length either side of the point nearest the main part |
| `CUTTING_MATERIAL` | `Zero_Density` | Material of the cut body - must exist in your catalog |

**Good to know**
- **Dialog fix:** the dialog was hidden with `Hide()` while picking. Hiding a modal
  dialog ends it, so the first pick closed the tool (and deleted the preview).
  It is now made invisible during picking instead.
- **Path-end fix:** when the secondary part *ends* at the main part, the cut was
  meant to extend past that end into the main part, but the point was placed back
  along the first segment, so the body folded back on itself. It now extends
  beyond the end.
- **Decimal fix:** the cut profile is written as `PL250*250.5`, never
  `PL250*250,5` on comma-decimal PCs; offsets accept `.` or `,`.
- The preview copied the secondary part's *Position object itself*, so changing the
  dialog values also changed that part's position data in memory. It now copies
  only the values it needs.
- A failed preview insert (e.g. missing `Zero_Density` material) is now reported
  instead of leaving a phantom preview; a failed cut turns the body back into a
  visible preview so you can adjust and retry.
- Secondary parts that don't connect end-to-end give a notice; duplicate points
  are removed; the main part can't be picked as a path part.
- Profile size is read from `PROFILE.HEIGHT/WIDTH`, falling back to `HEIGHT/WIDTH`.

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
