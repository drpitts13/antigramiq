# Reports

The Reports tab previews a text report and can copy, print, or export it. Every report begins with the product name, version, and intended-use statement.

## Choose a report

The **Report** list has eight types:

1. **Specimen Summary** — one accession, its clinical context, linked panels, and final ID. Requires a specimen.
2. **Panel Summary** — one lot’s identity and antigen grid. Requires a panel.
3. **Analysis Results** — the analysis text for one specimen. Requires a specimen.
4. **All Specimens** — every specimen in the database.
5. **All Panels** — every panel lot.
6. **Clinical Identification** — the identification report for one specimen, including the confirmed ID when you have confirmed one. Requires a specimen. When two or more antibodies are called, the interpretation includes a **Multiple-antibody separation** section. It shows each antibody's isolated reactive cells against the required number, marked **SHORT** when they fall short, or the exemption. After confirmation it checks the confirmed antibodies; before confirmation it checks the suspected antibodies that meet the ID rule.
7. **Panel Antigram** — the bench antigram for one lot. Requires a panel.
8. **Pending Work** — specimens and panels that still need attention. It does not ask for a specimen or a panel.

**Find** filters the specimen list by accession, type, phenotype, notes, or final ID. **Specimen** or **Panel** appears only for the report types that need one.

## Send it out

- **Copy** puts the full preview on the clipboard.
- **Print** prints the preview. Ctrl+P does the same while this tab is open.
- **Export CSV** saves a spreadsheet. Availability depends on the report type. Specimen and panel lists, and the single-specimen and single-panel summaries, export as CSV.
- **Export PDF** saves a PDF. Clinical Identification and Panel Antigram use a landscape page.

The preview is read-only. To change what it says, update the specimen, the panel, the grades, or the confirmed ID, then return to this tab. The preview refreshes when you change the report type or the selected specimen or panel.

**Help** opens this chapter.

## Report record

Every **Print**, **Export PDF**, and **Export CSV** is written to the audit log. The record holds the report type, the output, the file name, the software version, and a SHA-256 fingerprint of exactly what was issued. Specimen reports also record the confirmed identification and supervisor review at that moment, and appear on the specimen's Timeline, for example *Report exported to PDF: Clinical Identification (A123.pdf); ID at issue: anti-E; fingerprint 3F2A…*. Panel reports appear in the panel's audit history. An export that fails writes no record.

If a Clinical Identification report was issued and the identification was later cleared and confirmed again, the specimen's status says **Reissue the Clinical Identification report** until a new one is printed or exported.
