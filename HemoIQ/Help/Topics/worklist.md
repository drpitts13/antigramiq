# Worklist

The Worklist is the operational list for specimens that still need work and for specimens or panels that are near or past expiration.

## Count cards

Four cards sit across the top:

- **Incomplete** — reaction grids that are not finished, for specimens that do not yet have a confirmed antibody ID.
- **Stale analysis** — grades were saved after the last analysis, so the evidence on the Analysis tab is out of date.
- **Expiring** — specimens or panel lots inside the expiration warning window from Preferences.
- **Expired** — specimens or panel lots past their expiration date.

Click a card to show only that category. Click the same card again to show every category that is still checked. A thicker border means that card is the active filter.

## Filters

- **Find** narrows the grid by accession, panel name, kind, urgency, or detail.
- **Show all** clears the card filter.
- The **Show** checkboxes turn Incomplete, Stale analysis, Expiring, and Expired on or off together.
- **Open** goes to the selected row. Double-click a row, or press **Enter**, to do the same.
- **Refresh** reloads the counts and the grid.
- **Help** opens this chapter.

## What a row opens

- Incomplete reactions open the Reactions tab for that specimen.
- A stale analysis opens the Analysis tab.
- Specimen rows open Specimens.
- Panel rows open Panels.

Row colors match the category: amber for incomplete, orange for stale, yellow for expiring, and red for expired.

The worklist does not include a confirmed identification as unfinished work. After you confirm an ID, incomplete and stale rows for that specimen drop off.

**Supervisor review** items appear only when Preferences requires supervisor review. Each lists a confirmed identification that has not been reviewed yet. Open goes to the specimen, where **Supervisor Review…** records the review.

**Check typing** items list extra-antigen columns (for example Doa) that read negative on every cell of a panel with no record that anyone typed or verified them. Older versions filled a newly added extra antigen in as negative automatically, so such a column may be a default rather than real typing, and analysis would read it as antigen-negative. Compare the column with the vendor antigram. If it is correct, select the item and choose **Mark typing verified**; the confirmation is written to the audit log and the item clears (a supervisor is required once users are registered). If it is wrong, **Open** the panel and use **Edit** to correct the values or set them to NT. Nothing is changed automatically.
