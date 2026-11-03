# Specimen record

Use this dialog to add or edit an accession. Open it with **Add** or **Edit** on the Specimens tab, or with Ctrl+N while Specimens is selected.

## Fields

- **Accession Number** identifies the specimen. Use the accession your laboratory already uses.
- **MRN** is the patient's medical record number. It is stored in capitals. Specimens with the same MRN share history: an antibody confirmed on an earlier specimen appears on this specimen's Patient Typing tab as historical evidence, with the accession it came from. Suspected antibodies that were never confirmed are not carried forward. MRN changes are audited and locked after an identification is confirmed.
- **Type** is serum, plasma, or eluate.
- **Eluate of** is available when Type is eluate. Enter the accession of the serum or plasma specimen whose red cells were eluted. The eluate's results then appear in that specimen's analysis as reviewable notes (see *Eluate evidence* in the Specimens chapter). The link is audited and cannot point to the eluate itself or to another eluate. The default comes from Preferences.
- **Expiration Date** is the specimen dating date. Preferences can pre-fill it from the specimen-dating setting. Clear the date if this specimen should not expire.
- **DAT polyspecific**, **DAT IgG**, and **DAT C3** are the direct antiglobulin test results: NT, Negative, W+, 1+–4+, or MF (mixed field). Any reactive result makes the DAT positive for the pattern notes. The DAT is negative only when every recorded result is negative. C3 reactive with IgG negative is noted as more typical of cold or complement-mediated reactivity. A mixed-field DAT is read against the transfusion record in the same way as a mixed-field autocontrol. IgG and C3 changes are audited and locked after an identification is confirmed.
- **Transfusion status** is Unknown, Not transfused, or Transfused. Analysis reads this field before it reads Notes.
- **Last transfusion** is the date of the most recent red-cell transfusion. It is available only when the status is Transfused. A date in the future is refused.
- **Phenotype** is the serologic type, for example R1r, K−, Fy(a−b+). Analysis does not use the phenotype to exclude alloantibodies when the last transfusion falls within the Preferences recent-transfusion window, or when the status is Transfused with no date.
- **Genotype** is the predicted type from molecular results, for example RHCE*ce/ce. It stays available for review after transfusion. It is evidence for the reader, not a diagnosis.
- **Previous antibodies** records antibodies already known for this patient, for example anti-E.
- **Active** hides the specimen from the default lists when it is unchecked. Turn on **Show Inactive** to see it again.
- **Notes** holds free text such as medications or why the workup was ordered. When Transfusion status is Unknown, analysis still checks Notes and Phenotype for wording such as "transfused". Analysis names that keyword check in Patient Typing, so record the status instead. When the status is Not transfused, the status decides and any transfusion wording in Notes is named for review.

Each change to the transfusion status or date is written to the audit log with the old and new values. The fields cannot be changed after an identification is confirmed.

**Save** writes the record. **Cancel** discards the changes. **Help** opens this chapter.
