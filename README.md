# Tekla-Codes
Standalone HTML tools for preparing bolt catalog data for Tekla Structures.
Choose a manager to open its page:

- [Bolt Manager](Tekla%20Bolt%20Manager/)
- [Nut Manager](Tekla%20Nut%20Manager/)
- [Stud Manager](Tekla%20Stud%20manager/)
- [Washer Manager](Tekla%20Washer%20manager/)

## Use

Open a manager's `index.html` file in a web browser, enter the part dimensions,
and export the data in Tekla's `.bolts` format. Import the exported catalog data
into Tekla Structures. Create the bolt assembly in Tekla separately.

## Macros

Tekla Structures macros (C# Akit scripts) live on the
[`Macro`](https://github.com/pularirajeev77-bit/Tekla-Codes/tree/Macro) branch:

| Macro | What it does |
|---|---|
| XLS to XLSX Converter | Batch-converts Excel 97-2003 `.xls` files (e.g. Tekla reports) to `.xlsx` |
| Object Transporter | Copies beams/columns and contour plates between models via a base point |
| Dynamic Pin Creator | Creates a round pin with welded end caps between two picked points |
