# Worklist

The Worklist is the operational list for specimens that still need work and for specimens or panels that are near or past expiration.

## Count cards

Four cards sit across the top:

- **Incomplete** — reaction grids that are not finished, for specimens that do not yet have a confirmed antibody ID.
- **Stale analysis** — grades were saved after the last analysis, or analysis rules or clinical Preferences changed after it, so the evidence on the Analysis tab is out of date. The detail says which. Only investigations without a confirmed identification are listed.
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

**Expired reagent** items list open investigations (not yet confirmed, or confirmed but not supervisor-reviewed) with reactions recorded on a panel lot after its expiration date. The recording date is the last day graded results were saved on that run. Open goes to Analysis, where the same finding is a **REVIEW** note at the top of the suggestions. HemoIQ does not exclude the results; the laboratory decides whether they may be used. The item clears once the investigation is confirmed and supervisor-reviewed.

**Untyped cells** items list active panels with cells that have no antigen typed positive. Every real red cell types positive for something: c or C, e or E, and k or K. A new panel's cells start typed negative, so a cell with nothing positive almost certainly has no typing entered yet. Analysis would read it as antigen-negative for every antibody. The item names the cells, or says the whole panel is untyped. Open the panel, enter the typing from the vendor antigram, and the item clears. An investigation that uses such a panel also shows a **REVIEW** note on Analysis.

**Inconsistent status** items list investigations that are cancelled but still hold a confirmed identification. HemoIQ does not let you cancel an investigation with an identification, or confirm one on a cancelled investigation, so this state only comes from older or imported data. Analytics does not count these records. Open the investigation and either **Reinstate** it to keep the identification, or clear the identification with a reason.

**Duplicate lot** items list a lot number stored on two or more panels when at least one of them is active. Lots match ignoring capitals and surrounding spaces. Vendors match ignoring capitals, and a panel with no vendor matches any vendor. The detail lists each copy with its number, name, vendor and active state. Results can end up recorded against the wrong copy, so keep one active and deactivate or correct the others. Adding, editing or copying a panel with a lot number that is already stored asks for confirmation first.

**Check typing** items list extra-antigen columns (for example Doa) that read negative on every cell of a panel with no record that anyone typed or verified them. Older versions filled a newly added extra antigen in as negative automatically, so such a column may be a default rather than real typing, and analysis would read it as antigen-negative. Compare the column with the vendor antigram. If it is correct, select the item and choose **Mark typing verified**; the confirmation is written to the audit log and the item clears (a supervisor is required once users are registered). If it is wrong, **Open** the panel and use **Edit** to correct the values or set them to NT. Nothing is changed automatically.
