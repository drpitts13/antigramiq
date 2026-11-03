# Specimens

The Specimens tab is where you keep accessions and the clinical context that analysis uses as evidence.

## List

The left pane lists accession number, type, final ID, expiration, created date, and whether the specimen is active.

- **Find** filters by accession, type, phenotype, previous antibodies, notes, DAT, or final ID.
- **Add** opens a blank specimen record. **Edit** opens the selected record. Double-click a row to edit it.
- **Delete** removes the selected specimen after you confirm.
- **Refresh** reloads the list.
- **Show Inactive** includes specimens that were marked inactive.
- **Help** opens this chapter.

Row colors: green when a final ID is confirmed (the tooltip shows the confirmed antibodies), yellow when the specimen is nearing expiration, and red when it is expired.

## Details

The right pane follows the selected specimen.

- **Enter Reactions** opens Reactions for this accession.
- **Analyze** opens Analysis.
- **Clinical Report** opens Reports on the clinical identification report.

**Clinical Context** shows phenotype, genotype, previous antibodies, DAT, notes, and the confirmed final ID. These fields are edited from the specimen record.

**Linked Panels** are the lots used for this workup. **Attach Panel** picks a lot. **Detach** removes the selected link. Linking a panel makes it available when you enter reactions.

**Suspected Antibodies** and **Ruled-out Antibodies** are read-only summaries from the latest analysis. Change them by entering grades and running analysis, not by typing in these grids.

## Patient history

When the specimen has an MRN, the detail pane lists the patient's other specimens, newest first. Each line shows the confirmed identification, DAT, and transfusion status. Specimen Summary and the identification worksheet print the same list. Search and the specimen filter match MRN.

## Patient record

The **Patient (permanent, shared by MRN)** box shows information that belongs to the patient rather than to this specimen: name, date of birth, special transfusion requirements, and permanent blood bank comments. Select **Edit Patient** to change it. The button needs an MRN on the specimen. Every change is written to the audit log with the old and new values. A confirmed identification on one specimen does not lock the patient record, because the record spans all of the patient's specimens.

Write antigen-negative requirements so analysis can read them, for example `K-negative`, `E neg`, or `Fy(a-)`. Capital and lower-case letters are different antigens (C is not c). When an antibody is known for the MRN (Previous antibodies, a confirmed call on this specimen, or a confirmed call on another specimen with the same MRN) and its antigen-negative requirement is not written on the patient record, analysis lists a **REVIEW** suggestion at the top. It is a prompt to check the record, not a transfusion order.
