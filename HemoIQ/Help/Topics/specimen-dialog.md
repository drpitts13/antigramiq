# Specimen record

Use this dialog to add or edit an accession. Open it with **Add** or **Edit** on the Specimens tab, or with Ctrl+N while Specimens is selected.

## Sections

The fields are on four tabs. Press Ctrl+Tab and Ctrl+Shift+Tab to move between them.

- **Specimen**: accession, MRN, type, eluate source, collection and receipt, origin, expiration, and Active.
- **Clinical context**: transfusion status and date, pregnancy, and RhIG.
- **Serology**: DAT results, phenotype, genotype, and previous antibodies.
- **Notes**: free text.

The **Context** line under the tabs always lists what is recorded on Clinical context and Serology, for example *Context: Transfused (last 2026-10-30) · DAT IgG 2+ · previous anti-K*, so a value on a tab you have not opened is never hidden. Unknown, Not transfused, Not pregnant, and NT are left out. When **Save** finds a problem, it opens the tab that holds the field and puts the cursor there.

## Fields

- **Accession Number** identifies the specimen. Use the accession your laboratory already uses.
- **MRN** is the patient's medical record number. It is stored in capitals. Specimens with the same MRN share history: an antibody confirmed on an earlier specimen appears on this specimen's Patient Typing tab as historical evidence, with the accession it came from. Suspected antibodies that were never confirmed are not carried forward. MRN changes are audited and locked after an identification is confirmed.
- **Collected** and **Received** are date and time (HH:mm, 24-hour). Received defaults to now on a new specimen. Neither may be in the future, and Received cannot be before Collected. On a new specimen, choosing a collection date sets Expiration to collection + the Preferences specimen-dating days. Changes are audited and locked after an identification is confirmed.
- **Collected by / at** is who collected the specimen and where (unit, clinic, or draw site). **Ordered by / from** is the ordering provider and ordering location. Each is optional free text of up to 128 characters. They print on the worksheet, are searchable on Search, and the ordering location feeds **By ordering location** on Analytics. Changes are audited and locked after an identification is confirmed.
- **Type** is serum, plasma, or eluate.
- **Eluate of** is available when Type is eluate. Enter the accession of the serum or plasma specimen whose red cells were eluted. The eluate's results then appear in that specimen's analysis as reviewable notes (see *Eluate evidence* in the Specimens chapter). The link is audited and cannot point to the eluate itself or to another eluate. The default comes from Preferences.
- **Expiration Date** is the specimen dating date. Preferences can pre-fill it from the specimen-dating setting. Clear the date if this specimen should not expire.
- **DAT polyspecific**, **DAT IgG**, and **DAT C3** are the direct antiglobulin test results: NT, Negative, W+, 1+–4+, or MF (mixed field). Any reactive result makes the DAT positive for the pattern notes. The DAT is negative only when every recorded result is negative. C3 reactive with IgG negative is noted as more typical of cold or complement-mediated reactivity. A mixed-field DAT is read against the transfusion record in the same way as a mixed-field autocontrol. IgG and C3 changes are audited and locked after an identification is confirmed.
- **Transfusion status** is Unknown, Not transfused, or Transfused. Analysis reads this field before it reads Notes.
- **Last transfusion** is the date of the most recent red-cell transfusion. It is available only when the status is Transfused. A date in the future is refused.
- **Pregnancy** is Unknown, Not pregnant, Pregnant, or Recently pregnant. Recently pregnant requires the date the pregnancy ended. Pregnant, or a pregnancy that ended within the Preferences dating look-back, applies the specimen-dating check in the same way as a recent transfusion.
- **RhIG given** is the date Rh immune globulin was last given. If anti-D is suspected or confirmed within the Preferences RhIG window after that date, analysis notes that passive anti-D is possible.
- **Phenotype** is the serologic type, for example R1r, K−, Fy(a−b+). Analysis does not use the phenotype to exclude alloantibodies when the last transfusion falls within the Preferences recent-transfusion window, or when the status is Transfused with no date.
- **Genotype** is the predicted type from molecular results, for example RHCE*ce/ce. It stays available for review after transfusion. It is evidence for the reader, not a diagnosis.
- **Previous antibodies** records antibodies already known for this patient, for example anti-E.
- **Active** hides the specimen from the default lists when it is unchecked. Turn on **Show Inactive** to see it again.
- **Notes** holds free text such as medications or why the workup was ordered. When Transfusion status is Unknown, analysis still checks Notes and Phenotype for wording such as "transfused". Analysis names that keyword check in Patient Typing, so record the status instead. When the status is Not transfused, the status decides and any transfusion wording in Notes is named for review.

Each change to the transfusion status or date is written to the audit log with the old and new values. The fields cannot be changed after an identification is confirmed.

**Save** writes the record. **Cancel** discards the changes. **Help** opens this chapter.
