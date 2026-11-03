# Specimen record

Use this dialog to add or edit an accession. Open it with **Add** or **Edit** on the Specimens tab, or with Ctrl+N while Specimens is selected.

## Fields

- **Accession Number** identifies the specimen. Use the accession your laboratory already uses.
- **Type** is serum, plasma, or eluate. The default comes from Preferences.
- **Expiration Date** is the specimen dating date. Preferences can pre-fill it from the specimen-dating setting. Clear the date if this specimen should not expire.
- **DAT** is the direct antiglobulin test result: NT, Negative, W+, 1+, 2+, 3+, or 4+.
- **Transfusion status** is Unknown, Not transfused, or Transfused. Analysis reads this field before it reads Notes.
- **Last transfusion** is the date of the most recent red-cell transfusion. It is available only when the status is Transfused. A date in the future is refused.
- **Phenotype** is the serologic type, for example R1r, K−, Fy(a−b+). Analysis does not use the phenotype to exclude alloantibodies when the last transfusion falls within the Preferences recent-transfusion window, or when the status is Transfused with no date.
- **Genotype** is the predicted type from molecular results, for example RHCE*ce/ce. It stays available for review after transfusion. It is evidence for the reader, not a diagnosis.
- **Previous antibodies** records antibodies already known for this patient, for example anti-E.
- **Active** hides the specimen from the default lists when it is unchecked. Turn on **Show Inactive** to see it again.
- **Notes** holds free text such as medications or why the workup was ordered. When Transfusion status is Unknown, analysis still checks Notes and Phenotype for wording such as "transfused". Analysis names that keyword check in Patient Typing, so record the status instead. When the status is Not transfused, the status decides and any transfusion wording in Notes is named for review.

Each change to the transfusion status or date is written to the audit log with the old and new values. The fields cannot be changed after an identification is confirmed.

**Save** writes the record. **Cancel** discards the changes. **Help** opens this chapter.
