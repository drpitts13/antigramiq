# Panels

The Panels tab stores panel lots and the antigen profile of each cell. Analysis uses this grid together with the grades you enter.

## List

- **Typing** shows how completely each lot is typed, counting the cells but not the autocontrol. For example, *24 antigens · Fya partial · cell 3 untyped*:
  - the number is how many antigens are typed on every cell;
  - **partial** antigens are typed on some cells but NT on others;
  - **untyped** cells have nothing typed positive.

  Partial and untyped usually mean the antigram entry is unfinished, and the column turns amber. Antigens that are NT on every cell are simply not on the antigram. A new lot shows **Not typed**. The column updates when you save typing.
- **Find** filters by name, lot, vendor, or expiration date. Find is unavailable while you are editing antigens.
- **Add** creates a lot. **Details** edits the name, lot, vendor, cell count, and expiration. **Copy** duplicates a lot so you can start a new lot from an existing antigram.
- **Edit** turns on antigen editing for the selected panel. **Save** writes only the cells you changed and records each change (for example *Cell 3 Jka: + → -*) in the audit log. If the lot holds test history (recorded reactions or a confirmed identification), Save lists the changes and asks for a reason, such as the vendor's corrected antigram; without one nothing is saved. The edit then appears on the timeline of every specimen tested with the lot. **Delete** removes the lot after you confirm. A lot that holds test history cannot be deleted: if any reactions were recorded on it, or it is linked to a specimen with a confirmed identification, HemoIQ refuses and says why. Deactivate it instead so it is no longer offered for new testing. Deleting a lot that is only linked (no reactions, no confirmed identification) removes the links too, and the audit entry lists the specimens. Importing a lot that is already stored never replaces one that holds test history, and an import or copy that fails part-way leaves the stored panels exactly as they were.
- **Show Inactive** includes lots that are not active. Imported lots stay inactive until you review them.
- Yellow rows are nearing expiration. Red rows are expired.

## Bring a lot in

- **Import** creates a panel from a CSV lot sheet. The file uses a header row such as Cell, D, C, c, and the rest of the antigens.
- **Download…** opens the vendor catalog, or lets you import a vendor PDF or CSV you already have.
- After an import, the review dialog lets you **Activate now** or **Keep inactive**. You can also select an inactive lot later and click **Activate**.
- **Artifact** opens the retained import file and its SHA-256 hash.
- **Export** saves the selected antigen grid as CSV. **Antigram** saves a bench-copy PDF.

## Antigen grid

The grid on the right is the antigram. Press **Edit** before changing it.

- Click a cell, or press Space or Enter, to cycle **NT → + → ++ → −**. Antigens without an antithetical pair skip ++.
- Press **+**, **−** or **N** to set the current cell to +, − or NT directly.
- **Fill NT with −** types every NT cell in the selected antigen column as negative, skipping the autocontrol. For a new lot, enter the positives from the vendor antigram, then fill each column the antigram lists. Leave antigens the antigram does not list as NT. Nothing is stored until **Save**, which audits each change.
- Green is + or ++. Light gray is −. NT means the antigen was not typed.
- Drag column headers to reorder while editing.
- **Save** writes the grid (Ctrl+S does the same on this tab). **Cancel** leaves edit mode without saving.

**Add antigen…** adds a non-standard antigen from the warehouse catalog to this panel only. Every cell starts as NT (not tested), so analysis treats the antigen as unknown until you type each cell with **Edit**; a new column is never assumed negative. **Remove antigen…** drops one of those extra columns. If any cell is typed for that antigen and the lot holds test history, removal is refused because the typing is evidence behind past results; mark wrong values NT with **Edit** instead. Adding, removing, and reordering antigen columns are recorded in the audit log. **Compare…** diffs this antigram against another stored lot.

Antigen typing on the grid is the profile analysis uses. A blank or NT antigen is not treated as negative.
