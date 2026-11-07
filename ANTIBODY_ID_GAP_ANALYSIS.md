# Antibody identification capability matrix

Decision-support software for trained blood-bank professionals. The engine
computes evidence; a qualified user must confirm identification. Scores and
rule-outs are analytical evidence, not a diagnosis.

Status: **Present** (usable), **Partial** (exists but incomplete or silent),
**Missing**.

Last updated: 2026-11-03 (iteration 66 — local users and roles enforcing review, clear, reinstate, Preferences).

## Capability matrix

| Area | Capability | Status | Current evidence | Next action |
|---|---|---|---|---|
| Gap file | Living capability matrix | Present | This file | Update every iteration |
| Import | Provider adapter framework | Partial | `IVendorPanelSource` + Bio-Rad/Quotient public catalogs; Ortho/Immucor/Grifols/Medion file-only (Ortho's public Quick Antigram PDFs are barcode/QR sheets with no readable antigen table) | Authenticated portals only with vendor permission |
| Import | CSV | Present | Lab CSV + vendor CSV parser; lab import now reviews before activation | — |
| Import | PDF | Partial | PdfPig positional grid; layout-fragile | Harden after schema work |
| Import | XLSX / XML / JSON panels | Present | JSON/XML structured schema; first-sheet XLSX via zip/shared strings; same inactive+artifact review | Multi-sheet XLSX later |
| Import | Vendor APIs / authenticated portals | Missing | Public HTML + file import only. Do not bypass auth, CAPTCHA, licensing, or terms | Secrets storage only if a vendor authorizes access |
| Common model | Manufacturer, lot, expiration, cell, ABO/Rh, standard antigens | Partial | `Panel` / `PanelCell`; ++ vs partner+ and rare-null (K0, Jk(a−b−), Rhnull-like) on import review | More ISBT alleles later |
| Common model | Homozygous / heterozygous / negative / unknown / NT | Present | Import and panel editor persist `++` / NT; rule-out and search use explicit ++ or typed partner − | Combo/list editors later |
| Import validation | Required fields, duplicate lot | Partial | Parse fails without cells; `FindPanelByVendorLot` | — |
| Import validation | Impossible values, schema-vs-prior-lot, unknown antigens | Partial | Schema vs prior lot; ++ vs partner+; unknown columns; rare-null notes; Weiner label vs typed D/C/c/E/e | More ISBT alleles later |
| Import traceability | Original artifact retained | Present | Vendor and lab CSV store SHA-256 + file; Artifact viewer verifies and opens | — |
| Reaction entry | IS / 37°C / AHG / CC | Present | Keyboard 0–4, W/M/H, N, Enter | — |
| Reaction entry | w+, MF, hemolysis | Present | Combo + keys; treated as reactive evidence | — |
| Reaction entry | RT, PEG, gel, solid phase as phases | Partial | Extra JSON plus Preferences RT/PEG/Gel/Solid and titer-grid checkboxes; empty stays tube-only; Gel/Solid/PEG are IAT-like; Dil1–Dil128 are titer only | Custom method names later |
| Analysis | Transparent candidate evidence | Partial | Explain Analysis lists suspected, in-progress, historical, untypeable, pattern, enzyme, DTT, and cell-level adsorption comparisons. Adsorbed aliquots do not rule out or enter ordinary scores for specificities they are expected to remove. A panreactive untreated run paired with autoadsorption or rr/R1R1/R2R2 adsorption is omitted from ordinary scores. Unpaired ficin, papain, and DTT runs are named and are not compared with another panel | Cross-panel enzyme and DTT pairing stays out of scope |
| Analysis | Configurable visible rule-out | Partial | `Rule.MinRuleoutCount` is now applied; lab default + explanation sentences | Watch seeded anti-D/anti-k rules; ACS remains separate |
| Analysis | Single / multiple / dosage scoring | Partial | Fisher + pattern; pairwise combinations; dosage averages | Label as evidence, not diagnosis |
| Audit | Investigation timeline | Present | `InvestigationTimeline` merges receipt, panel link/unlink (now audited), runs, grouped reaction saves, analysis snapshots, confirm/clear/review, MRN/transfusion/DAT/patient edits; Specimens Timeline expander and worksheet section | Report print/PDF/CSV audited with SHA-256 and shown on the timeline |
| Reporting | Turnaround metrics | Present | `TurnaroundService`: received→confirmed (days) and confirmed→reviewed (hours) median and nearest-rank 90th percentile by received date, cancelled excluded; overdue open list with status and Open; configurable threshold; CSV | Uses recorded received time when present |
| Reporting | Workload by technologist | Present | `WorkloadService`: per initials, confirmations (by confirmed date), multi-antibody confirmations, supervisor reviews (by review date), median received→confirmed; names from the user registry; cancelled excluded; on Analytics and in CSV | Reaction entry and amendments per user are on the Activity by user tab (by login, from the audit log) |
| Specimen | Collector, collection and ordering location, ordering provider | Present | Four optional specimen columns with DB length CHECK (128); trimmed and whitespace-collapsed; audited `update_origin`; locked after final call; on the timeline, detail pane, worksheet, and Search; `WorkloadService.ComputeByLocation` groups received investigations by ordering location on Analytics and CSV | Free text, not a configured location list |
| Specimen | Collection/receipt time and dating check | Present | Audited `collected_at`/`received_at`; expiration defaults from collection; `SpecimenDating` REVIEW when transfused within the look-back and expiration exceeds collection + dating days, or cannot be checked | Pregnancy (current or within look-back) is a dating trigger; RhIG date flags possible passive anti-D |
| Tests | Isolation from user data | Present | `AppDataPaths.RootOverride` set by a test-module initializer to a temp folder; live vendor test persists only to its isolated database; full run verified to leave %AppData%\HemoIQ untouched | One intermittent failure seen once and not reproduced in two reruns |
| Search | Investigation search | Present | Search tab › Investigations: accession, MRN, patient name/DOB, final/previous antibodies, technologist; every word must match; inclusive date range; status column; Open jumps to Specimens | Panel lot/manufacturer used in an investigation not yet searchable |
| Workflow | Investigation status and next step | Partial | `InvestigationStatusService` derives five steps and a status (awaiting panel → testing → analysis needed → awaiting ID → completed / amended / reopened / inactive) from panels, grades, analysis time, final call, and audit; shown on Specimens and the worksheet; Worklist shares the completeness check | Optional supervisor review step (two-person, audited, invalidated by clear/re-confirm, Worklist queue); audited Cancel/Reinstate with reason, locked and off work queues; local users/roles enforce review, clear, reinstate, Preferences, user management |
| Patient | Permanent record and transfusion requirements | Present | `patients` table (MRN, name, DOB, comments, free-text requirements, structured `antigen_negative`); `AntigenNegativeRequirement` parses codes (case-sensitive C/c, E/e, K/k, S/s; Fy(a-) notation) and refuses non-antigens; audited add/update; Edit Patient on Specimens; reports and detail pane print E−, K−, Fy(a−); analysis raises REVIEW when a known antibody's antigen is not in the structured list, and asks to add it when only free text mentions it | Expiry or source of each requirement; product-level requirements (irradiated, CMV-safe) remain free text |
| Patient | MRN and cross-specimen history | Partial | Indexed `specimens.mrn`; audited; confirmed final calls on the same MRN become Historical evidence citing the source accession; detail pane and reports list other specimens | Encounter, location, ordering provider, sex, pregnancy history |
| Specimen | DAT polyspecific / IgG / C3 | Present | `DatProfile` on specimen; MF grade; audited IgG/C3; positive if any reactive, negative only if all recorded negative; C3-only caveat on autoantibody notes; MF DAT donor-cell note | Eluate specimens link to their source; `EluateEvidence` reads specificity, panreactivity, and nonreactivity against DAT and transfusion |
| Analysis | Panreactive / cold / warm / HTLA / auto / HFA / LFA models | Partial | DAT scores; named LFA/pan cells; enzyme lost/gained/resistant; DTT lost/resistant vs untreated. Unpaired runs, comparisons with fewer than 3 paired cells, and comparisons that meet no threshold are each named | Cells tested on only one of the paired runs stay unnamed |
| Analysis | Selected-cell recommendations with “why” | Partial | Unused in-date vials preferred; P1−/Ch−/Sd(a−) selected cells ranked when neutralization favors soluble substance | More ISBT alleles later |
| Specimen | Structured transfusion context | Partial | Status (Unknown / Not transfused / Transfused) + last-transfusion date on the specimen; DB CHECK constraint; audited; locked after final call; Preferences window (default 90 days) in analysis snapshot; MF autocontrol named against the record | Product counts/types, pregnancy, HSCT/BMT, mixed-field concern |
| Analysis | Patient phenotype / genotype / transfusion limits | Partial | Structured transfusion date vs configurable window decides phenotype reliability and is explained; note keywords only when status is Unknown; GYPA null/Mk En(a); RHCE*Ce vs ce case-sensitive; CeRN variant-C; RHD DAR/DVI/DNB partial D does not rule out anti-D; GATA still does not support anti-Fya | More ISBT alleles later |
| UI | Antigram freeze / slashes / run compare | Partial | Frozen cell; slashes; compare extras; Show dosage; filter/sort; panel lot compare | — |
| UI | Explain Analysis panel | Present | Explain tab + Summary + clinical report assemble rule-out, support, conflict, phenotype, additional testing | Keep wording as evidence, not diagnosis |
| Audit | Panel, reactions, rules, settings, final ID | Partial | Snapshots store rule-engine version, rules JSON, operator, and the adsorption comparison (conclusions plus ordinary-score exclusions) | Cross-panel enzyme/DTT pairing later |
| Tests | Synthetic cases with intermediate reasoning | Partial | Enzyme and DTT cases cover unpaired runs, short comparisons, mixed loss/gain/persistence that fits no pattern, and a one-loss two-persistence case that stays a partial destroyed pattern | Expand case library |

## Iteration 1 (this change)

**Deficiency:** Labs can configure `MinRuleoutCount` and heterozygous exceptions,
but the analyzer treated any single qualifying homozygous cell as ruled out and
never explained the policy.

**Bounded improvement:** Apply `DefaultMinRuleoutCount` (lab default 1) and
per-antibody `Rule.MinRuleoutCount`. Separate observed qualifying cells from
“meets configured rule-out.” Show an explainable sentence. ACS continues to use
`AcsRuleoutCount` against **observed** qualifying counts.

**Not in this tick:** import NT semantics, selected cells, phenotype engine,
XLSX/portals.

## Iteration 2 (this change)

**Deficiency:** Vendor sheets that omit an antigen column, or mark a cell NT,
were persisted as typed `-`. That made E+ look homozygous when `e` was never
typed, and let untested antigens enter Ag− scoring.

**Bounded improvement:** Persist missing/NT as not-tested. Analysis only uses
antigens the panel actually types. Antithetical zygosity requires a typed
partner. Summary and suggestions list clinically significant antigens that
were not tested.

**Not in this tick:** selected cells, phenotype engine, XLSX/portals, import
review-before-activation.

## Iteration 3 (this change)

**Deficiency:** After remaining candidates were listed, the app only said
“add more Ag+ cells.” It did not rank unused inventory cells or explain why
a cell would discriminate competing antibodies.

**Bounded improvement:** Score unused active-panel cells for remaining
candidates. Prefer homozygous expression of the antigen of interest, cells
that lack competing antigens, and cells that resolve the most competing
pairs. Show explanations on the Selected Cells tab, Summary, and suggestions.

**Not in this tick:** phenotype/genotype limits, full per-candidate narrative
for non-suspected antibodies, XLSX/portals.

## Iteration 4 (this change)

**Deficiency:** Specimen phenotype and previous antibodies were display-only.
A patient who typed E+ could still be left with silent anti-E suspicion, and
recent transfusion was not treated as a limit on interpretation.

**Bounded improvement:** Parse Weiner and antigen text into support / against /
uninterpretable / historical considerations. Do not auto-remove candidates.
Notes mentioning transfusion make phenotype evidence uninterpretable.

**Not in this tick:** structured genotype columns, per-candidate narrative for
non-suspected antibodies, XLSX/portals.

## Iteration 5 (this change)

**Deficiency:** Supporting and conflicting cells existed only for antibodies
that already crossed the suspected threshold. Ruled-out, in-progress, and
historical specificities had no assembled narrative a reviewer could read
in one place.

**Bounded improvement:** `AnalysisExplainer` builds a per-candidate explanation
from existing rule-out evaluations, suspected evidence, dosage, phase scores,
patient typing, and selected-cell recommendations. The Analysis Explain tab,
Summary, suggestions, and clinical report show that text. Nothing is
auto-confirmed.

**Not in this tick:** import review-before-activation, genotype columns,
XLSX/portals, broader reaction grades/phases.

## Iteration 6 (this change)

**Deficiency:** Vendor lots became active as soon as parse succeeded. The
original file was not retained, and a new lot that dropped or added an
antigen column was not compared to the previous lot from that vendor.

**Bounded improvement:** Vendor imports stay inactive. SHA-256 of the source
bytes is stored and the file is copied beside the database. The review
sentence names added/removed antigens versus the prior lot. A qualified user
must activate the panel before it enters inventory.

**Not in this tick:** lab-CSV artifact retention, XLSX/portals, broader
reaction grades/phases, structured genotype fields.

## Iteration 7 (this change)

**Deficiency:** Lab CSV import typed missing antigen columns as `-`, kept
no source file, and activated from the details dialog without comparing the
sheet to the prior lot.

**Bounded improvement:** Missing CSV columns stay untyped. `LabPanelImportService`
stores SHA-256 + the original file, compares antigen headers to the prior
lot, and leaves the panel inactive until review. Same review dialog as vendor
import.

**Not in this tick:** artifact viewer, XLSX/portals, broader reaction
grades/phases, structured genotype fields.

## Iteration 8 (this change)

**Deficiency:** Reaction entry and scoring only knew 0–4+ and NT. A weak,
mixed-field, or hemolyzed result had no grade. `ReactionToNumeric` treated
those strings as 0, so they could disappear from “strongest phase” even when
`IsPositive` saw them as non-zero text.

**Bounded improvement:** Canonical grades `w+`, `MF`, and `H` are selectable
and typed (W/M/H). They count as reactive evidence, cannot support rule-out,
and produce a reviewable sentence. RT/PEG/gel remain later as extra phases.

**Not in this tick:** configurable extra phases, artifact viewer, XLSX/portals.

## Iteration 9 (this change)

**Deficiency:** Labs that record RT, PEG, or gel reactivity had no column.
Those results were either omitted or stuffed into AHG, so a room-temp-only
pattern could look like a valid AHG-negative rule-out.

**Bounded improvement:** Lab Preferences accepts extra phase names. Grades
are stored as JSON on the reaction. Extra-phase reactivity counts as
positive evidence and cannot support rule-out. Prewarm still suppresses RT
with IS. Classic IS / 37°C / AHG / CC is unchanged when the setting is blank.

**Not in this tick:** artifact viewer, XLSX/portals, compare-grid extra columns.

## Iteration 10 (this change)

**Deficiency:** Vendor and lab CSV imports stored a SHA-256 and a copy of
the source file, but the UI never showed whether that file still existed or
still matched.

**Bounded improvement:** The Panels **Artifact** button inspects the retained
file, reports present / missing / hash-mismatch, previews CSV text, and can
open the original. Panel details show the stored hash. Nothing is
auto-activated.

**Not in this tick:** XLSX/XML/JSON adapters, compare-grid extra columns.

## Iteration 11 (this change)

**Deficiency:** Labs and vendors could import PDF or CSV only. Spreadsheet
exports and structured panel files were rejected or misread as CSV.

**Bounded improvement:** Vendor and lab import accept JSON, XML, and first-sheet
XLSX. JSON/XML carry lot, catalog, and per-cell antigens without inventing
types for omitted antigens. XLSX uses the same grid rules as CSV (Cell column
plus recognized antigen headers). Imports stay inactive with a retained
artifact. No new packages; no authenticated vendor portals.

**Not in this tick:** multi-sheet workbooks, antigram grouping / dosage toggle,
compare-grid extra columns, authenticated APIs.

## Iteration 12 (this change)

**Deficiency:** Extra phases (RT, PEG, gel) were entered and scored, but the
run-vs-run compare grid only showed IS / 37°C / AHG / CC. A cell that was
AHG-negative on both runs and RT 2+ on one looked unchanged.

**Bounded improvement:** Compare rows include configured extra phases (and any
extras stored on either run). A difference at RT/PEG/gel marks the row changed
and produces a sentence such as `Cell 1 differs: RT (this 2+ vs other 0).`
Missing extra on the other run is NT. Classic phases still compare as before.

**Not in this tick:** antigram grouping / dosage toggle, solid-phase defaults,
authenticated APIs.

## Iteration 13 (this change)

**Deficiency:** The reaction antigram showed only +/−. Homozygous and
heterozygous cells looked the same, and columns followed sheet order with no
blood-group grouping. Dosage lived only on a later Analysis tab.

**Bounded improvement:** Reactions **Show dosage** groups visible columns by
system (Rh, Kell, Duffy, …) and marks homozygous antigen-positive cells as
`++`, heterozygous as `+`, and `+/?` when the antithetical partner was never
typed. Tooltips name the cell and zygosity. Off restores the panel column
order and plain +/−. Rule-out is unchanged.

**Not in this tick:** filter/sort, panel compare, solid-phase defaults,
authenticated APIs.

## Iteration 14 (this change)

**Deficiency:** The reaction antigram always showed every cell in vial order.
A reviewer looking for homozygous E+ or reactive cells had to scan the whole
sheet, and there was no way to hide Ag− rows without losing them from save.

**Bounded improvement:** Filter the visible grid by antigen and zygosity, and
sort by cell number, reactive first, or selected-antigen first. Autocontrol
stays visible. Hidden cells remain in the working set for save, compare, and
analysis. A sentence states how many cells match.

**Not in this tick:** panel compare, solid-phase defaults, authenticated APIs.

## Iteration 15 (this change)

**Deficiency:** Import review compared antigen *columns* to the prior lot, but
labs could not open two stored panels and see which *cells* changed typing.
A new lot that flipped cell 1 from E+ to E− looked the same if the header
list matched.

**Bounded improvement:** Panels **Compare…** diffs the selected lot against
another stored panel (defaults to the prior vendor/product lot). It reports
added/removed antigens, cells present on only one lot, and per-cell typing
changes. Missing types stay NT, not invented negatives.

**Not in this tick:** solid-phase defaults, authenticated APIs.

## Iteration 16 (this change)

**Deficiency:** Phase scores existed, but a sheet that was all IS 2+ / AHG 0
looked like ordinary negative AHG rule-outs. Gel and Solid reactivity had no
IAT-like meaning in the narrative.

**Bounded improvement:** Classify cold (IS/RT+ AHG/IAT−), warm (IAT+ IS/RT−),
mixed-phase, and panreactive patterns when at least three evaluated cells
match. Gel, Solid, and PEG count as IAT-like. Prewarm still ignores IS/RT.
Sentences appear on Explain, Summary, and suggestions. Nothing is
auto-confirmed.

**Not in this tick:** HTLA / HFA / LFA models, ExtraPhases lab default list,
authenticated APIs.

## Iteration 17 (this change)

**Deficiency:** Weak pan-AHG (w+/1+) looked like an ordinary warm pattern.
Panagglutination did not use the autocontrol to separate autoantibody from
high-prevalence alloantibody, and a single unexplained reactive cell had no
low-frequency note.

**Bounded improvement:** Weak IAT without IS/RT (4+ cells) is labeled HTLA-like.
Panreactive plus a nonreactive AC favors high-prevalence; a reactive AC favors
autoantibody. One or two reactive cells among six or more is labeled
low-frequency / extra antibody / mistype. Still evidence, not a diagnosis.

**Not in this tick:** ExtraPhases lab default list, titration workflow,
authenticated APIs.

## Iteration 18 (this change)

**Deficiency:** Genotype lived only as free-text phenotype. After
transfusion the entire type was uninterpretable, so a recorded *RHCE*ce/ce*
or *FY*02/FY*02* result could not argue for or against an alloantibody.

**Bounded improvement:** Specimen `genotype` is stored and parsed into
predicted antigens (ISBT-style *RHCE* / *RHD* / *FY* / *JK* / *KEL* pairs,
or the same Weiner/Fy(a−b+) marks). Serology still becomes
uninterpretable after transfusion; genotype remains Predicted evidence.
Conflicts are explained. Nothing is auto-ruled-out or auto-confirmed.

**Not in this tick:** ExtraPhases lab default list, variant-allele risk
catalog, authenticated APIs.

## Iteration 19 (this change)

**Deficiency:** Extra phases existed only as a comma-separated string. Labs
did not have a visible RT / PEG / Gel / Solid default, and empty settings
gave no hint that Solid is an IAT-like Capture column.

**Bounded improvement:** Preferences now toggles the suggested extras.
`FromSuggested` / `Toggle` keep custom names, refuse reserved IS/AHG
columns, and leave a blank list blank so tube-only labs stay on IS / 37°C /
AHG / CC. Solid-phase 0 with AHG 0 can still rule out; Solid+ remains
IAT-like evidence, not a diagnosis.

**Not in this tick:** Variant-allele catalog, titration workflow,
authenticated APIs.

## Iteration 20 (this change)

**Deficiency:** Selected-cell suggestions treated every unused inventory cell
the same. A leftover ID-panel cell could outrank an unused Selectogen /
0.8% vial, and patient E− (or a post-transfusion genotype prediction)
did not raise an E+ cell.

**Bounded improvement:** Unused vials whose name looks like selected cells
get a ranking boost and an explanation. Patient Ag− (including Predicted
genotype after transfusion) boosts matching Ag+ cells; patient Ag+ only
softens the score and never drops a distinguishing cell. Still
recommendations, not a diagnosis.

**Not in this tick:** HTLA titration workflow, expired-vial filtering,
authenticated APIs.

## Iteration 21 (this change)

**Deficiency:** HTLA notes only said “consider titration.” A recorded titer
in Notes or a Titer extra-phase was ignored, and unused Yt(a−) / Vel−
cells were never ranked when the sheet looked HTLA or high-prevalence.

**Bounded improvement:** Parse a recorded titer (notes or Titer/Dilution
column). Titer ≥16 strengthens the HTLA sentence; a low titer warns that
it may not be HTLA. When HTLA or high-prevalence is present, unused cells
typed or annotated as high-prevalence Ag− are recommended. Nothing is
auto-identified.

**Not in this tick:** Neutralization workflow, expired-vial filtering,
authenticated APIs.

## Iteration 22 (this change)

**Deficiency:** Selected-cell ranking could name an expired Selectogen vial
ahead of an in-date ID-panel cell. Expiration was not explained.

**Bounded improvement:** In-date unused cells are preferred. Expired vials
are omitted when any in-date recommendation exists. If only expired
inventory remains, at most two cells are listed with a “do not use”
warning. Expiring lots stay recommended and ask the user to confirm the
date. Still not a diagnosis.

**Not in this tick:** Allele variant catalog, neutralization workflow,
authenticated APIs.

## Iteration 23 (this change)

**Deficiency:** Any `N` in an *RHD* allele was treated as a deletion, so
*RHD*DNB* became D−. *FY*01N (GATA) predicted Fy(a−) and treated that as
support for alloanti-Fya, which is usually wrong.

**Bounded improvement:** Null *RHD* alleles are *01N* / deletion / DEL only.
Weak D, partial D, DEL, GATA *FY*01N, FyX, and *RHCE*ceAR-like haplotypes
add Variant notes. GATA suppresses “predicted Fya− supports anti-Fya.”
Nothing is auto-identified.

**Not in this tick:** Full ISBT catalog, neutralization workflow,
authenticated APIs.

## Iteration 24 (this change)

**Deficiency:** HTLA notes said “consider neutralization,” but a Neut/Inhib
column or “neutralized with plasma” note was never compared to IAT.

**Bounded improvement:** Pair IAT with Neut/Neutral/Inhib grades. Loss of
reactivity favors a soluble-substance / HTLA-like pattern (plasma, urine,
saliva, P1). Persistence argues against readily neutralized Ch/Rg, Sd(a),
or Lewis. Notes without paired grades stay qualitative. Nothing is
auto-identified.

**Not in this tick:** Full titer grid, broader ISBT catalog, authenticated
APIs.

## Iteration 25 (this change)

**Deficiency:** Vendor `++` / `+/+` / HOMO was flattened to `+`. Homozygous
rule-out then required a typed antithetical partner, so a marked homozygous
cell with `e` NT could not rule out anti-E. A lone `+` with an untyped
partner still had to stay unknown.

**Bounded improvement:** Persist explicit `++`. Rule-out, selected-cell
ranking, dosage homo/het, antigram dosage, and inventory search treat `++`
as homozygous without inventing a partner-negative. A typed partner `+`
overrides `++` (conflict, not homozygous). A single `+` with an untyped
partner is still not homozygous. Nothing is auto-identified.

**Not in this tick:** Panel-editor `++` toggle, broader ISBT catalog,
authenticated APIs.

## Iteration 26 (this change)

**Deficiency:** *JK**, *KEL* null, and *GYPB* tokens were parsed for
predicted Jk/K/S types, but there were no review notes. *JK*01N/JK*02N*
looked like ordinary Jk(a−b−) support and never mentioned anti-Jk3.
Weak *JK*01W was treated as a normal Jk(a+).

**Bounded improvement:** True JK nulls stay predicted antigen-negative
(unlike GATA-FY). Two JK null alleles add a Jk3 review note. Weak JK
warns that predicted Jk+ does not rule out alloanti-Jk. KEL-null / K0
and GYPB null / U-var add Variant notes. Nothing is auto-identified.

**Not in this tick:** Full ISBT catalog, panel-editor zygosity marks,
authenticated APIs.

## Iteration 27 (this change)

**Deficiency:** The panel antigen editor toggled only `+`/`−` and displayed
`++` as `+`. A lab could not mark homozygous or NT without inventing a
partner-negative, and an imported `++` was lost on the first click.

**Bounded improvement:** Click cycles NT → + → ++ → − (unpaired antigens
such as D skip ++). The editor shows and saves `++` and NT. Saved `++`
with an untyped partner still rules out; saved NT is not treated as
antigen-negative. Nothing is auto-identified.

**Not in this tick:** Full titer grid, more ISBT alleles, authenticated
APIs.

## Iteration 28 (this change)

**Deficiency:** A titer was only a notes word or a single Titer/Dilution
number. Serial tubes (Dil1–Dil128 or 1:2 / 1:4 …) were ignored, and a
grade on a dilution column could look like a panel phase.

**Bounded improvement:** Last reactive serial-dilution extra phase is the
endpoint (prozone uses the highest reactive tube). `1+` is still a grade,
not titer 1. Dilution columns are not IAT and do not change rule-out.
Nothing is auto-identified.

**Not in this tick:** Preferences titer-grid checkbox, more ISBT alleles,
authenticated APIs.

## Iteration 29 (this change)

**Deficiency:** Serial-dilution columns parsed as a titer only if the lab
typed Dil1–Dil128 (or 1:n) into Extra phases. Tube-only Preferences had
no control to add that grid, and the extra-phase list capped at 12 names
so a titer grid plus Solid/Neut could be truncated.

**Bounded improvement:** Preferences **Titer grid (Dil1–Dil128)** adds
those columns. Unchecking removes them and keeps RT/PEG/Gel/Solid and
custom names such as Neut. Dilution grades remain titer evidence, not
IAT, and cannot prevent AHG-negative rule-out. Extra-phase cap raised
to 24. Nothing is auto-identified.

**Not in this tick:** More ISBT alleles, impossible-value import catalog,
authenticated APIs.

## Iteration 30 (this change)

**Deficiency:** Import review compared antigen *headers* to the prior lot,
but a cell marked E++ with e+ still looked like a clean homozygous type,
and columns the catalog does not know (Fy3, vendor extras) disappeared
without a review sentence.

**Bounded improvement:** `PanelTypingInspector` flags homozygous ++ with
a positive antithetical partner, unrecognized type tokens, and unknown
antigen columns. Issues appear on the inactive-lot review. Heterozygous
+/+ and ++ with an untyped or negative partner stay silent. Unknown
columns are not imported. Nothing is auto-activated or identified.

**Not in this tick:** Null-phenotype catalogs (K0, Rhnull), more ISBT
alleles, authenticated APIs.

## Iteration 31 (this change)

**Deficiency:** Both-negative antithetical types on a reagent cell looked
like ordinary negatives. K−k−, Jk(a−b−), or C−c− plus E−e− could be a
true null cell or a mistype, and Lewis Lea−Leb− (common) would have been
noisy if every pair were flagged.

**Bounded improvement:** Import review notes rare-null patterns (K0-like,
Jk(a−b−), Fy(a−b−), S−s−, M−N−, Lu(a−b−), Rhnull/D−−-like). Lewis both
negative is not flagged. An untyped partner is not treated as negative.
Lots stay inactive. Nothing is auto-identified.

**Not in this tick:** More ISBT alleles, phenotype-string vs typed-antigen
conflicts, authenticated APIs.

## Iteration 32 (this change)

**Deficiency:** A stored Weiner label (R1R1) was never compared with typed
D/C/c/E/e. An R1R1 cell that was c+ looked like a normal heterozygous
C+c+ type and could be used for homozygous anti-c rule-out by mistake.

**Bounded improvement:** Import review flags Weiner/Rh labels that
disagree with typed Rh antigens. C++ still matches an expected C+.
Untyped antigens and matching rr / R1R1 cells stay silent. Lots stay
inactive. Nothing is auto-identified.

**Not in this tick:** More ISBT alleles, ABO vs reverse-cell notes,
authenticated APIs.

## Iteration 33 (this change)

**Deficiency:** Neutralization notes explained IAT vs Neut, but unused
inventory was never ranked for the matching antigen-negative cell
(P1− after P1 substance, Ch− after plasma). Ordinary ID-panel P1−
rows would have drowned those vials.

**Bounded improvement:** When neutralization favors a soluble
substance (lost IAT reactivity or a qualitative note), unused
selected-cell vials or Special Types annotations that lack P1, Ch/Rg,
Sd(a), or Lewis are recommended with a why sentence. Persistence after
neutralization does not boost those cells. Ordinary panel P1− rows are
not ranked. Nothing is auto-identified.

**Not in this tick:** More ISBT alleles, saliva Le(a−b−) panel-wide
ranking, authenticated APIs.

## Iteration 34 (this change)

**Deficiency:** Analysis snapshots stored software version and lab
settings, but not the rule-engine version or per-antibody MinRuleoutCount
/ heterozygous exceptions. A later rule edit left no trace of the policy
that produced a prior analysis.

**Bounded improvement:** Snapshots record `rule-engine-v1` plus the
active antibody rules. Explain Analysis, Summary, and the report trace
line state lab default vs overrides (for example anti-D requires 3,
heterozygous C allowed). Changing a rule writes a new snapshot. Nothing
is auto-identified.

**Not in this tick:** More ISBT alleles, operator identity on the
snapshot row, authenticated APIs.

## Iteration 35 (this change)

**Deficiency:** GYPA tokens were recognized but GYPA*01N was treated as
ordinary GYPA*01 (predicted M+). Mk / En(a−) genotypes never produced a
review note, so M−N− looked like a typing gap rather than a GYPA null.

**Bounded improvement:** GYPA nulls persist as antigen-negative (M0/N0),
not as M+ or N+. GYPA*01N/GYPA*02 stays M−N+ without an En(a) note.
Two GYPA nulls or Mk alleles add an anti-En(a) review note. Ordinary
GYPA*01/GYPA*02 stays silent. Nothing is auto-identified.

**Not in this tick:** RHCE*CeRN / more RHD alleles, operator on snapshots,
authenticated APIs.

## Iteration 36 (this change)

**Deficiency:** RHCE haplotype parsing used case-insensitive "ce", so
RHCE*Ce and RHCE*CeRN were treated as little-c haplotypes. Variant C
(CeRN / HAR / C^w) had no review note, so predicted C+ looked like it
could rule out anti-C.

**Bounded improvement:** C vs c in RHCE alleles is case-sensitive. CeRN
still predicts C+e+ (with c+ if the partner is ce) and adds a variant-C
note that predicted C+ does not rule out anti-C. Ordinary RHCE*Ce/ce
stays silent. ceAR remains a variant-e note. Nothing is auto-identified.

**Not in this tick:** More RHD DAR/DVI catalog rows, never-imported
specificity classifiers, authenticated APIs.

## Iteration 37 (this change)

**Deficiency:** Analysis snapshots recorded software version and rules
but not who ran the analysis. Audit events already stored Windows login;
Explain Analysis and the report trace line did not.

**Bounded improvement:** Snapshots store `analyzed_by` (lab initials when
set, otherwise Windows login). Explain, Summary, and the report name the
operator and still say this is not a diagnosis. Blank initials fall back
to Windows login; neither recorded is stated as such. Nothing is
auto-identified.

**Not in this tick:** Never-imported specificity classifiers,
authenticated APIs.

## Iteration 38 (this change)

**Deficiency:** Antigens missing from imported sheets were listed as a
flat “not tested” name list. Antibodies such as anti-Kpa or anti-Cw
never appeared in Explain Analysis, so a reviewer could not see that
the current panels cannot type those specificities.

**Bounded improvement:** Classify standard-catalog antibodies whose
antigens were never imported. Explain Analysis, Summary, and
suggestions state that they cannot be typed (not a rule-out, not an
identification). Clinically significant antigens keep the existing
untreated list. Historical antibodies without a typed column are
labeled historical and untypeable. Warehouse antigens stay silent
unless a panel types them. Nothing is auto-identified.

**Not in this tick:** Pattern classifiers (HTLA/HFA/LFA beyond titer),
authenticated APIs.

## Iteration 39 (this change)

**Deficiency:** Pattern notes labeled cold, warm, panagglutination, and
HTLA but did not name which cells supported or conflicted, did not use
the recorded DAT, and did not attach an evidence score. Panagglutination
without an autocontrol could not lean auto vs high-prevalence.

**Bounded improvement:** Each pattern records supporting/conflicting
cells and an analytical evidence score (not a diagnosis). Low-frequency
notes name the reactive cell. Incomplete panagglutination names the
nonreactive cells. DAT positive with panagglutination and no autocontrol
favors autoantibody; DAT negative favors high-prevalence. A reactive
autocontrol with a negative DAT is shown as conflicting evidence.
Nothing is auto-identified.

**Not in this tick:** Enzyme-treated vs untreated pattern comparison,
authenticated APIs.

## Iteration 40 (this change)

**Deficiency:** Ficin/papain runs gated destroyed-antigen rule-outs, but
lost vs gained vs surviving reactivity was only inferred for antibodies
already suspected. Explain Analysis did not show a panel-wide enzyme
comparison, and ReactivityGainedOnEnzyme was unused.

**Bounded improvement:** Compare untreated vs ficin/papain runs on the
same panel. Lost reactivity names cells and enzyme-destroyed antigens
(Fya, M, S, …). Newly reactive cells favor enzyme-enhanced antigens
(Rh, Kidd). Survival of reactivity argues against destroyed antigens as
the sole explanation. Mixed lost/persisted is conflicting evidence.
Scores and named cells are not a diagnosis.

**Not in this tick:** Cross-panel enzyme pairing, more RHD DAR/DVI
catalog rows, authenticated APIs.

## Iteration 41 (this change)

**Deficiency:** RHD*DAR was treated as ordinary D+ with no review note,
so serologic D+ looked like it could rule out anti-D. DVI/DNB used a
generic weak/partial sentence that did not say D+ does not rule out
anti-D.

**Bounded improvement:** Catalog partial D alleles (DAR, DVI, DIII–DVII,
DNB, DAU, DFR, DOL, DBT, DHAR, and similar). They do not predict D+ and
add a note that serologic or predicted D+ does not rule out anti-D.
Weak D (RHD*01W) still predicts D+ with the existing weak-D note.
Ordinary RHD*01 is unchanged. A D+ tube type with RHD*DAR is conflicting
evidence, not a rule-out. Nothing is auto-identified.

**Not in this tick:** DTT-treated vs untreated pattern comparison,
authenticated APIs.

## Iteration 42 (this change)

**Deficiency:** DTT runs gated Kell/Lutheran rule-outs, but lost vs
surviving reactivity was only inferred for already-suspected antibodies.
Explain Analysis had no panel-wide DTT comparison parallel to ficin/papain.

**Bounded improvement:** Compare untreated vs DTT runs on the same panel.
Lost reactivity names cells and DTT-destroyed antigens (K/k, Lua/Lub, Yt,
Knops, Do, …). Survival argues against DTT-destroyed antigens as the sole
explanation. Mixed lost/persisted is conflicting evidence. Scores and named
cells are not a diagnosis. Enzyme kinds stay separate from DTT kinds.

**Not in this tick:** Cross-panel DTT pairing, absorption evidence in
Explain, authenticated APIs.

## Iteration 43 (this change)

**Deficiency:** Adsorption conclusions labeled a suspected antibody
“survived” when any cell on the aliquot was reactive. Explain Analysis
did not name the antigen-positive cells that lost or kept reactivity,
and a negative on an rr / R1R1 / R2R2 aliquot could count as a rule-out
of the antibody that aliquot is expected to remove.

**Bounded improvement:** Compare each adsorbed aliquot with the matching
untreated run on the same panel. For allogeneic adsorption, name
antigen-positive cells that lost reactivity (supporting removal), cells
that stayed reactive (conflicting), and typed-negative cells whose
remaining reactivity is not explained by the expected specificities.
Autoadsorption names cells that lost panreactivity and cells that
remained reactive. Homozygous cells are marked. Scores are analytical
evidence. Explain Analysis, Summary, suggestions, the adsorption grid,
and the clinical report show the sentences. Negatives on an aliquot do
not rule out antibodies that aliquot is expected to remove. Nothing is
auto-identified.

**Not in this tick:** Using the adsorbed aliquot as ordinary probability
evidence, cross-panel pairing, snapshot storage of the comparison,
authenticated APIs.

## Iteration 44 (this change)

**Deficiency:** An rr (or R1R1 / R2R2) aliquot no longer ruled out the
antibodies it is expected to remove, but those same reactions still
entered Fisher's exact test, pattern matches, phase scores, dosage, and
combination counts. Antigen-positive cells that became negative after
adsorption looked like evidence against anti-c. Cells that stayed
reactive were listed again as ordinary supporting cells.

**Bounded improvement:** For each specificity an aliquot is expected to
remove, skip that aliquot in ordinary probability and pattern scores.
Removal and survival stay on the absorption comparison. Explain Analysis,
Summary, suggestions, and the clinical report say the aliquot was
excluded and that this is not a diagnosis. The analysis snapshot stores
the comparison and the exclusion sentences. Antibodies the aliquot is
not expected to remove, including residual anti-K after rr adsorption,
still use the aliquot. Autoadsorption has no specific allo list, so its
aliquot remains ordinary evidence. Explain Analysis keeps anti-c and
anti-C as separate candidates.

**Not in this tick:** Once autoadsorption is present, stop using the
untreated panreactive run as ordinary alloantibody evidence. Authenticated
vendor portals stay later and must not bypass auth, CAPTCHA, licensing,
or terms.

## Iteration 45 (this change)

**Deficiency:** Autoadsorption has no specific alloantibody list, so the
aliquot stayed in ordinary scores. The paired untreated run, often
panreactive, stayed in those scores too. Antigen-negative cells that
reacted before adsorption looked like mismatches against an underlying
alloantibody.

**Bounded improvement:** When an untreated run is panreactive (at least 4
evaluated cells and at least 80 percent reactive) and an autoadsorbed
aliquot on the same panel and cell treatment has reactions, ordinary
probability and pattern scores use the aliquot and omit that untreated
run. A 3-of-4 reactive untreated run still counts. Explain Analysis,
Summary, suggestions, and the clinical report name the excluded run and
say this is not a diagnosis. The absorption comparison still uses both
runs. Allogeneic adsorption is unchanged.

**Not in this tick:** Applying the same omission to a panreactive
untreated run paired only with rr / R1R1 / R2R2 adsorption. Authenticated
vendor portals stay later and must not bypass auth, CAPTCHA, licensing,
or terms.

## Iteration 46 (this change)

**Deficiency:** A panreactive untreated run was omitted from ordinary
scores only when the paired aliquot was autoadsorbed. The same warm
panagglutinin beside rr, R1R1, or R2R2 adsorption still entered Fisher's
exact test and pattern matches. Antigen-negative cells that reacted
before adsorption looked like mismatches against an antibody that
remained on the aliquot.

**Bounded improvement:** The same panreactive rule (at least 4 evaluated
cells and at least 80 percent reactive) now applies when the paired
aliquot is allogeneic. Ordinary scores use that aliquot for specificities
it is not expected to remove. Specificities it is expected to remove stay
on the absorption comparison, including survival when antigen-positive
cells remain reactive. A 3-of-4 reactive untreated run still counts.
Explain Analysis names the excluded run and says this is not a diagnosis.

**Not in this tick:** Cross-panel pairing of enzyme or DTT runs.
Authenticated vendor portals stay later and must not bypass auth,
CAPTCHA, licensing, or terms.

## Iteration 47 (this change)

**Deficiency:** A ficin, papain, or DTT run with no untreated run on the
same panel was skipped without a sentence. An untreated run on a different
panel was also ignored silently, so a reviewer could not tell that no
enzyme or DTT comparison had been made.

**Bounded improvement:** Those runs now produce an evidence note naming
the treated run and panel, stating that no same-panel untreated run was
available, and stating that a different panel was not paired. A same-panel
comparison with fewer than three paired cells stays silent. Explain
Analysis, Summary, suggestions, the clinical report, and the Special
Panels tab show the note. Nothing is identified.

**Not in this tick:** Explaining a same-panel comparison that has too few
paired cells. Authenticated vendor portals stay later and must not bypass
auth, CAPTCHA, licensing, or terms.

## Iteration 48 (this change)

**Deficiency:** A ficin, papain, or DTT run that shared a panel with an
untreated run, but had fewer than three cells that could be paired, was
skipped. Two cells that lost reactivity looked like an enzyme-destroyed
or DTT-destroyed pattern and were not distinguished from a comparison
that never happened.

**Bounded improvement:** Those comparisons now produce an evidence note
naming the treated run, how many cells were paired, which cells, and how
many lost, gained, or kept reactivity. The note says three paired cells
are required and that no destroyed, enhanced, or resistant pattern was
assigned. Explain Analysis, Summary, suggestions, the clinical report,
and the Special Panels tab show the note. Nothing is identified.

**Not in this tick:** A same-panel comparison with three or more paired
cells that still fits no pattern. Authenticated vendor portals stay later
and must not bypass auth, CAPTCHA, licensing, or terms.

## Iteration 49 (this change)

**Deficiency:** A ficin, papain, or DTT comparison with three or more
paired cells was silent when the counts missed every pattern threshold.
One cell that lost reactivity, one that gained it, and one that kept it
was indistinguishable from a comparison that was never reviewed.

**Bounded improvement:** Those comparisons now produce an evidence note
naming the treated run, the paired cells, and how many lost, gained, or
kept reactivity. The note states the destroyed, enhanced, and resistant
thresholds and says no pattern was assigned. A one-loss two-persistence
comparison still stays a partial destroyed pattern. Explain Analysis,
Summary, suggestions, the clinical report, and the Special Panels tab
show the note. Nothing is identified.

**Not in this tick:** Naming cells that were tested on only one of the
two runs. Authenticated vendor portals stay later and must not bypass
auth, CAPTCHA, licensing, or terms.

## Iteration 50 (this change)

Product review: iterations 44–49 all refined enzyme and DTT comparisons.
This tick moves to specimen and patient context, which the workflow
starts from.

**Deficiency:** Recent transfusion was detected only by keywords in the
specimen notes or phenotype. "Received 2 PRBC" was missed, so a likely
donor-cell E+ phenotype argued against anti-E. "No transfusion history"
was flagged as transfused. No last-transfusion date was recorded, and
the window was not configurable.

**Bounded improvement:** Specimens now store a transfusion status and a
last-transfusion date. The status column has a database CHECK
constraint, and changes are audited and blocked after a final call.
Preferences has a recent-transfusion window (1–365 days, default 90),
which is saved with each analysis snapshot. Analysis uses the structured
status first. Transfused within the window, with no date, or with a date
after the specimen makes the phenotype uninterpretable. A date outside
the window leaves the phenotype usable. The note keywords decide only
when the status is Unknown. Under Not transfused, transfusion wording in
the notes is named for review. Each explanation states the date, the day
count, and the window. Summary, suggestions, the specimen detail, and
both specimen reports show it. Nothing is identified.

**Not in this tick:** Product counts and types, pregnancy history,
HSCT/BMT, mixed-field concern, and connecting transfusion context to
mixed-field reaction notes. The legacy keyword check remains as the
fallback for Unknown.

## Iteration 51 (this change)

**Deficiency:** A mixed-field autocontrol produced no note, because the
special-grade notes skip the AC row. After a recent transfusion that
result is the classic sign of alloantibody coating surviving donor
cells (possible delayed serologic or hemolytic transfusion reaction).

**Bounded improvement:** Every autocontrol phase graded MF, including
extra phases, is named once per row and read against the transfusion
assessment from iteration 50. The note covers four cases: recent
transfusion (suggest a DAT and an eluate), Not transfused (review
history, transplant, and the specimen), Transfused outside the window
(donor cells less likely), and Unknown (record the status). Panel-cell
MF is unchanged. Suspected and ruled-out lists are unchanged. The note
reaches Summary, Explain Analysis, suggestions, and reports through the
existing special-reaction notes.

**Not in this tick:** DAT has no mixed-field option, and DAT IgG/C3 are
not recorded separately. Eluate results are not structured.

## Iteration 52 (this change)

**Deficiency:** The DAT was one polyspecific value with no IgG or C3
result and no mixed-field grade. Pattern notes could not tell a warm
IgG picture from C3-only reactivity. A mixed-field DAT after
transfusion could not be recorded.

**Bounded improvement:** Specimens store IgG and C3 DAT results beside
the polyspecific value. MF is now a DAT grade. A `DatProfile` makes the
DAT positive if any recorded result is reactive, and negative only when
every recorded result is negative. Autoantibody-favoring pattern notes
add a cold or complement caveat when only C3 reacts. A mixed-field DAT
gets the donor-cell note from iteration 51. IgG and C3 changes are
audited, validated against the grade list, and locked after a final
call. The dialog, specimen detail, and reports show the full DAT.
Polyspecific-only wording is unchanged.

**Not in this tick:** Eluate results are not structured, and DAT history
across specimens is not tracked by patient (there is no patient entity
yet).

## Iteration 53 — product review and patient link (this change)

**Product review.** Complete: panel import with review, reaction entry,
transparent analysis with Explain Analysis, final call with audit and
lock, structured transfusion and DAT context. Fragmented: everything is
keyed by accession, with no patient identity, so history does not carry
between specimens. No investigation object or status workflow exists
beyond the Worklist categories. Analysis explanations are strong, but
iterations 44–49 went deep on one niche (enzyme/DTT) while patient and
specimen basics lagged. The roadmap below re-weights toward patient and
investigation workflow.

**Deficiency:** A confirmed antibody (for example an evanescent anti-Jka)
did not reach the patient's next specimen unless someone retyped it into
Previous antibodies.

**Bounded improvement:** Specimens carry an indexed, audited MRN, locked
after a final call. Analysis adds each antibody confirmed on another
specimen with the same MRN as Historical evidence, citing the earliest
accession and date, without duplicating Previous antibodies. Unconfirmed
suspected lists are not carried. The detail pane, Specimen Summary, and
worksheet list the patient's other specimens. The filter matches MRN.

**Not in this tick:** A patient table (name, DOB, permanent blood-bank
comments, special transfusion requirements) and warning when two
specimens share an MRN but disagree on ABO/Rh or phenotype.

## Roadmap after review

1. Patient record: name, DOB, permanent comments, special transfusion
   requirements (antigen-negative, irradiated), shown on every specimen
   with that MRN.
2. Investigation status workflow (created → testing → analysis → review
   → completed/amended) with a visible step indicator per specimen.
3. Eluate results as structured data feeding analysis.
4. Product counts and types, pregnancy, HSCT/BMT on the transfusion record.
5. Enzyme/DTT: name cells tested on only one run.

## Iteration 54 (this change)

**Deficiency:** The MRN linked specimens, but there was nowhere to keep
permanent patient information. Nothing checked whether a known
antibody's antigen-negative requirement was recorded.

**Bounded improvement:** A `patients` table keyed by MRN holds name,
date of birth, permanent blood bank comments, and special transfusion
requirements. Add and update are audited with before and after values.
Future or invalid dates of birth are refused. The Specimens tab shows
the patient in its own box, separate from specimen context, with Edit
Patient. Specimen Summary and the worksheet print it. Analysis collects
antibodies known for the MRN (Previous antibodies, this specimen's
confirmed call, confirmed calls on other specimens). Each one whose
antigen-negative requirement is not written on the patient record
gives a REVIEW suggestion. Requirement matching is case-sensitive
(C vs c) and accepts `K-negative`, `K neg`, `K−`, and `Fy(a-)`.

**Not in this tick:** Requirements are free text, not a structured
list. Encounter, location, ordering provider, sex, and pregnancy history
are not recorded. There is no patient search screen beyond the
specimen filter.

## Iteration 55 (this change)

**Deficiency:** Where an investigation stood had to be pieced together
from panel links, the reaction grid, analysis time, and the final call.
A cleared and re-confirmed identification looked the same as one never
changed.

**Bounded improvement:** `InvestigationStatusService` derives five steps
and a status from recorded data. No status is stored, so it cannot
drift. Amended means a confirmed identification was cleared and
confirmed again (from the audit log). Reopened means cleared and not yet
re-confirmed. The next action names incomplete panels with
entered/total counts. The Specimens detail pane shows a status strip,
and the worksheet prints the status line. The Worklist now uses the same
`GetIncompleteReactionPanels` check.

**Not in this tick:** Separate technologist and supervisor review steps,
an explicit Cancel action with reason, and a status column in the
specimen list.

## Iteration 56 (this change)

**Deficiency:** The Search tab only searched reagent cells by antigen
profile. A patient's earlier workups could not be found by MRN or name,
and there was no way to list every anti-Jka identification or every
call by one technologist.

**Bounded improvement:** The Search tab now opens on **Investigations**.
`InvestigationSearch` matches accession, MRN, patient name and DOB,
final and previous antibodies, final comment, and technologist
initials. Every word must match. An optional inclusive date range can be
used alone. Rows show the derived status and sort newest first. Open
(or double-click) goes to the specimen. Empty searches and reversed date
ranges are refused with a message. The antigen-profile search moved
unchanged to its own section.

**Not in this tick:** Searching by panel lot or manufacturer used in an
investigation. The specimen-list filter still matches MRN but not
patient name.

## Iteration 57 (this change)

**Deficiency:** A confirmed identification was final the moment one
person entered it. There was no second-person review, no queue of calls
awaiting review, and nothing on the report showing sign-off.

**Bounded improvement:** A Preferences option, *Require supervisor
review*, adds a sixth status step. `SetSpecimenSupervisorReview` records
reviewer initials and time on the specimen. It refuses a review when
there is no confirmed call, when the call was already reviewed, when
initials are blank, or when the reviewer is the person who confirmed the
call. Each review is audited with the confirmed antibodies. Confirming
or clearing the call removes any earlier review. Status shows *Awaiting
supervisor review* until it is done. The Worklist adds a Supervisor
review queue. The worksheet prints the reviewer or *PENDING*.

**Not in this tick:** Role-based permissions (initials are not
authenticated users), a Cancel action with reason, and a turnaround
report for review time.

## Iteration 58 (this change)

**Deficiency:** Reconstructing an investigation meant reading the raw
audit log, run records, and analysis snapshots separately. Attaching or
removing a panel was not audited at all.

**Bounded improvement:** Panel link and unlink are now audited with the
panel name and lot, and an index on audit entity lookups was added.
`InvestigationTimeline` builds one ordered list from receipt, runs
(untreated or additional testing), reaction saves (grouped per run when
less than 15 minutes apart, with save and cell counts), each analysis
snapshot with operator and engine version, confirm, clear with reason,
supervisor review, MRN, transfusion, and DAT edits, and the patient
record's audit. Only this specimen's events appear. The Specimens tab
has a Timeline expander, and the worksheet prints the timeline.

**Not in this tick:** Report printing or export is not yet audited, so
it does not appear. Panels linked before this change show only their
run start date.

## Iteration 59 — product review (this change)

**Product review.** Iterations 50–58 moved the product from
accession-only records to a patient-linked investigation workflow:
structured transfusion and DAT data, MRN and patient record, derived
status with supervisor review, investigation search, and a timeline.
The app was renamed back to HemoIQ by merging the existing rename
branch. While doing that, the review found the most serious open
problem: **the test suite wrote to the user's real data.** The log
was written on every run, and a live vendor-download test imported
downloaded vendor lots into `%AppData%\HemoIQ\hemoiq.db` (and a
database beside the Release exe) with `replaceExisting: true`. It also
rewrote antigen order on other imported panels from the same vendor.
On this machine that added vendor panels 17, 19, and 21–24 and deleted
18 and 20, which were themselves earlier test imports. None were linked
to a specimen or had runs.

**Bounded improvement:** `AppDataPaths.RootOverride` lets the test
project point the data root (default database, settings, logs,
artifacts) at a per-run temp folder through a module initializer. The
live vendor test persists only to its isolated database. New tests
assert the root, log directory, settings path, and default database all
resolve under the test folder. A full run was checked against the real
%AppData% file timestamps and sizes, and nothing changed.

**Not in this tick:** The six test-imported vendor panels remain in the
user's database pending the user's decision. One intermittent test
failure appeared once and could not be reproduced.

## Roadmap after review

1. Audited report generation (print/PDF) so reports appear on the timeline.
2. Structured eluate results feeding analysis.
3. Cancel action with reason, and turnaround metrics from the timeline.
4. Role-based permissions (initials are not authenticated users).

## Iteration 60 (this change)

**Deficiency:** Printing and exporting a report left no record. Nobody
could tell which identification a report carried or whether a report
was issued before the identification was later amended.

**Bounded improvement:** `ReportService` writes a `report_output` audit
event after each successful PDF export, CSV export, or print. The event
holds the report type, output, file name, SHA-256 of the issued
content, software version, and, for specimen reports, the confirmed
identification and reviewer at issue. Specimen reports attach to the
specimen, panel reports to the panel, and list reports to `report`.
The timeline shows each issue. When the latest Clinical Identification
report predates the latest clear, an Amended investigation's next
action is to reissue the report, until a newer report is issued. A
failed export writes nothing.

**Not in this tick:** Printing has no confirmation that the printer
completed the job (the event is written when the job is handed to
Windows). There is no Worklist queue for reports needing reissue.

## Iteration 61 (this change)

**Deficiency:** An eluate could be entered as a specimen of type eluate,
but nothing tied it to the patient specimen it came from. Eluate
findings, which are central to recognizing delayed transfusion reactions
and warm autoantibodies, never reached the source investigation.

**Bounded improvement:** `specimens.source_accession` (indexed) links an
eluate to its source. `SetEluateSource` refuses non-eluates,
self-links, missing sources, and eluate sources, is audited, and is
locked after the eluate's final call. `EluateEvidence` counts graded and
reactive cells on each linked eluate's untreated runs and adds one note
per eluate to the source analysis. The note covers: not yet tested;
specificity (confirmed preferred over suspected), worded as possible
DHTR/DSTR after a recent transfusion and naming antibodies not suspected
in the source; panreactive (at least 80% of four or more cells); or
nonreactive with a positive DAT, explained for a C3-only DAT. The dialog
has *Eluate of*. The detail pane shows the link both ways, and the
timeline shows link changes.

**Not in this tick:** Elution method and last-wash control are not
recorded. Eluate antigen-negative rule-outs are not used for the source.

## Iteration 62 (this change)

**Deficiency:** An investigation could not be cancelled. A mislabeled
or unacceptable specimen could only be deleted, which loses the record,
or set inactive, which is meant for expiry and leaves the specimen
editable and on work queues.

**Bounded improvement:** `CancelInvestigation` and
`ReinstateInvestigation` each require a reason and are audited.
Cancelling is refused while an identification is confirmed or when
already cancelled. The lock (`EnsureSpecimenUnlocked`, analysis save,
and confirming a final call) now also covers cancelled records, with
its own message. Status shows Cancelled with date and reason. The
Worklist skips cancelled specimens. The timeline shows both events.
Confirm ID on a cancelled record shows a message instead of an
unhandled error.

**Not in this tick:** Cancel reasons are free text (no configurable
reason list). Turnaround metrics from the timeline remain.

## Iteration 63 (this change)

**Deficiency:** Analytics only counted confirmed antibodies. There was
no measure of investigation turnaround time and no view of open
investigations that were running late.

**Bounded improvement:** `TurnaroundService` is a reporting-only
service, separate from workflow code. For investigations received in
the Analytics date range, with cancelled ones excluded, it computes
received→confirmed in days and confirmed→reviewed in hours, each as a
median and nearest-rank 90th percentile with a count. It also lists
active open investigations older than the new Preferences turnaround
threshold (1–60 days, default 3), oldest first, with their derived
status. Analytics shows three cards and an overdue list with Open.
Export CSV appends the figures and the list.

**Not in this tick:** A received time (only the date is recorded),
workload by technologist, and a Worklist queue for overdue
investigations.

## Iteration 64 — product review and specimen timing (this change)

**Product review.** Iterations 60–63 completed audited report output,
eluate evidence, cancel/reinstate, and turnaround metrics. The weakest
remaining part of the specimen workflow was timing. Specimens had only
a received date set at record creation and no collection or receipt
time. Expiration defaulted from *today* instead of from collection.
Nothing checked dating against recent transfusion. That is the
highest-value gap and ties to the iteration-50 transfusion record.

**Bounded improvement:** Specimens carry collection and receipt
date-times. They are validated (not in the future, receipt not before
collection), audited, locked after a final call, and on the timeline.
On a new specimen, receipt defaults to now and choosing a collection
date sets expiration from collection. `SpecimenDating` raises a REVIEW
(Specimens pane, top of analysis suggestions, worksheet) when the
patient was transfused within a configurable look-back (default 90
days) and the expiration is later than collection + dating days, or
when it cannot be checked because the collection time or expiration is
missing. Turnaround uses the recorded receipt time.

**Not in this tick:** Pregnancy within 3 months as a dating trigger
(no structured pregnancy field yet), collector and location.

## Roadmap after review

1. Structured pregnancy history on the patient record (also a dating trigger).
2. Role-based permissions (initials are not authenticated users).
3. Workload by technologist on Analytics.

## Iteration 65 (this change)

**Deficiency:** Pregnancy could only be written in notes. It did not
trigger the specimen-dating check, although pregnancy within 3 months
triggers that rule just as transfusion does. RhIG was not recorded, so
anti-D in a recently treated patient looked the same as an immune
alloantibody.

**Bounded improvement:** Specimens carry pregnancy status (Unknown,
Not pregnant, Pregnant, Recently pregnant with a required end date) and
an RhIG date. The status column has a CHECK constraint, and changes are
validated (no future dates), audited, locked after a final call, and on
the timeline. `SpecimenDating` now applies when the patient is
pregnant, or the pregnancy ended within the look-back, and names every
reason. When anti-D is suspected or confirmed and RhIG was given within
the configurable window (default 84 days) before collection, analysis
raises a REVIEW for possible passive anti-D. The dialog, detail pane,
and worksheet show pregnancy and RhIG.

**Not in this tick:** Gravida/para counts, anti-D titer tracking across
pregnancies, and HSCT/BMT context.

## Iteration 66 (this change)

**Deficiency:** Anyone at the workstation could record a supervisor
review under any initials, change analysis Preferences, clear a
confirmed identification, or reinstate a cancelled investigation.

**Bounded improvement:** A `users` table keyed by Windows login holds
initials, name, and role (Technologist, Supervisor, Administrator), with
CHECK constraints. `AccessControl` gates actions in the service layer:
supervisor review, clearing an identification, and reinstating require
Supervisor or Administrator; Preferences and user management require
Administrator. While no user is registered, everything stays allowed,
so existing installs keep working. With users registered, review
initials must be the signed-in user's own, unregistered logins are
refused protected actions with a clear message, the first user must be
an Administrator, the last active Administrator is protected, and
initials are unique. User changes are audited, and every audit entry
records the signed-in login. Preferences has **Users & roles…**.

**Not in this tick:** A read-only role, per-lab configurable
permissions, and password or second-factor sign-in (identity is the
Windows login).

## Iteration 67 (this change)

**Deficiency:** Analytics showed turnaround for the lab as a whole but
not who did the work. A supervisor could not see how confirmations and
reviews were spread across technologists, or whose cases took longest.

**Bounded improvement:** `WorkloadService` is a reporting-only model
over specimens. For each set of initials it counts confirmed
identifications by confirmed date (Identified by), confirmations
naming more than one antibody, and supervisor reviews by review date
(Reviewed by), with the median received-to-confirmed time. Initials
match case-insensitively; names come from Users and roles when the
initials are registered. Cancelled investigations, unconfirmed cases
and blank initials are excluded, and a confirmation recorded before
receipt is counted but left out of the median. Analytics shows a
**Workload by technologist** table beside the overdue list, and
**Export CSV** appends it.

**Not in this tick:** Reaction-entry and amendment counts per user
(audit entries record the Windows login, so they can be joined only
when users are registered), and workload by location (no location
field yet).

## Product review (after iteration 67)

**Complete enough to rely on:** specimen receipt through confirmation
(status strip, worklist, timeline), transfusion/pregnancy/DAT context
feeding analysis as named REVIEW notes, supervisor review with roles,
cancel/reinstate, eluate linking, report-output audit, and
turnaround/workload analytics.

**Still fragmented:**
- Specimen origin (collector, locations, provider) was not recorded,
  so reports could not show where a specimen came from and workload by
  location was impossible. Addressed in iteration 68.
- Patient transfusion requirements are free text; the requirement
  check relies on keywords. A structured antigen-negative requirement
  list would make the check exact.
- Reaction entry and amendments per user cannot be counted by
  initials because the audit log stores the Windows login.
- PDF import remains layout-fragile; vendor portals stay file-only.

**Screens growing complex:** the Specimen dialog now has 21 rows. It
should be split into Specimen and Patient context sections (or tabs)
before more fields are added.

**Testing gaps:** no UI automation; dialog validation is covered only
through the service methods it calls.

## Roadmap after review

1. Specimen origin and workload by ordering location (iteration 68).
2. Structured antigen-negative transfusion requirements on the patient record.
3. Split the Specimen dialog into sections.
4. Per-user reaction-entry and amendment counts from the audit log (registered users).

## Iteration 68 (this change)

**Deficiency:** Specimens had no collector, collection location,
ordering location, or ordering provider. The worksheet could not say
where a specimen came from, Search could not find cases by unit or
provider, and Analytics could not show workload by location.

**Bounded improvement:** Four optional specimen columns, each with a
database CHECK of 128 characters. `UpdateSpecimenOrigin` trims and
collapses whitespace, writes an `update_origin` audit with before and
after values only when something changed, and is locked after a final
call. The Specimen dialog has **Collected by / at** and **Ordered by /
from**; the detail pane, timeline, and worksheet show the origin, and
Search matches it. Analytics has a **By ordering location** tab
(received, confirmed, open, median TAT; cancelled excluded; unrecorded
last), included in Export CSV.

**Not in this tick:** A configured location list (values are free
text, so spelling variants count separately), and location on the
patient record.

## Iteration 69 (this change)

**Deficiency:** Antigen-negative requirements lived only in free text,
and the requirement check found them with a keyword pattern. A typo,
an unusual phrasing, or C written for c could make the check pass or
fail silently.

**Bounded improvement:** Patients carry a structured **Antigen-negative
units** list (`patients.antigen_negative`), stored as canonical codes
in panel order. Entry accepts commas, semicolons or spaces, a trailing
-, neg or negative, and Fy(a-) notation; case decides C/c, E/e, K/k,
S/s, and other antigens match case-insensitively only when one fits.
Anything that is not a known antigen is refused by the dialog and by
`SavePatient`, so the list never holds an unreadable entry. Saves are
audited (re-entering the same set differently is not a change). The
requirement check uses the structured list exactly. A requirement
written only in the free text still raises a REVIEW asking the user to
add it to the list; existing records are never rewritten
automatically. The detail pane and worksheet print E−, K−, Fy(a−).

**Not in this tick:** A one-click "add to patient record" action from
the REVIEW note, requirement source and date, and product-level
requirements (irradiated, CMV-safe) as structured fields.

## Iteration 70 (this change)

**Deficiency:** The Specimen dialog had grown to 21 rows in one column.
Transfusion, pregnancy, DAT, and typing fields sat between identity and
notes, so users scrolled past fields they did not need and could miss
the ones that change analysis. Validation errors did not say where the
field was.

**Bounded improvement:** The dialog has four tabs: Specimen (identity,
collection, origin, expiration, Active), Clinical context (transfusion,
pregnancy, RhIG, with a note on how they affect analysis), Serology
(DAT, phenotype, genotype, previous antibodies), and Notes. A
`SpecimenContextSummary` line under the tabs lists every recorded
context and serology value as the user types, leaving out Unknown, Not
transfused, Not pregnant, and NT, so nothing on a closed tab is hidden.
Each validation error opens the tab holding the field and focuses it;
pregnancy and RhIG future dates now have separate messages. Saved data
and the save sequence are unchanged.

**Not in this tick:** Saving the specimen's fields in one transaction
(the view model still calls several audited update methods in turn, so
a later failure can leave earlier fields saved), and remembering the
last tab.

## Iteration 71 (this change)

**Deficiency:** Saving the Specimen dialog ran eight audited updates in
turn. If a later one was refused (an eluate source that does not exist,
received before collected), the earlier ones stayed saved. A new
specimen could be created half-filled, and the audit log recorded
changes the user was told had failed.

**Bounded improvement:** `DatabaseService.RunInTransaction` wraps work
in a SQL savepoint, so every existing update method takes part without
change and calls may nest. `SpecimenSaveService.Save` runs the whole
dialog save inside it, for both add and edit. A refused step rolls back
the specimen row, every field, and every audit entry from that save. The
message now says "Nothing was saved" / "No changes were saved". Each
step keeps its own validation and audit.

**Not in this tick:** Other multi-step writes (panel import with cells,
patient save plus MRN link) still run step by step and should move to
`RunInTransaction`.

## Iteration 72 (this change)

**Deficiency:** Replacing an already-stored lot during import deleted
the old panel first and then inserted the new one step by step. A
failure part-way left the lot missing. Worse, `DeletePanel` cascades to
specimen links and panel runs, and runs cascade to reactions, so
deleting or replacing a lot that had been used for testing silently
erased patients' recorded reactions. Panel copy and cell replacement
were also multi-step without a transaction.

**Bounded improvement:** `GetPanelUsage` reports linked specimens,
recorded reaction rows, and linked specimens with a confirmed
identification. `DeletePanel` refuses a panel with reactions or a
confirmed identification ("Deactivate it instead"), and the audit
entry of an allowed delete lists the links it removed. Vendor and lab
imports refuse to replace a stored lot that holds test history and
otherwise run delete-plus-import in one `RunInTransaction`, so a
failure keeps the stored lot. `ReplacePanelCells` and Copy Panel are
transactional. The Panels Delete button shows the refusal instead of
failing. A `RunInTransaction<T>` overload returns a value.

**Not in this tick:** A source artifact saved to disk before a failed
import is left in the artifact folder (content-addressed, harmless,
not referenced). Specimen deletion and archive purge were not reviewed
in this pass.

## Iteration 73 (this change)

**Deficiency:** Deleting a specimen wrote no audit entry and was allowed
even with a confirmed identification, erasing the reactions, analysis,
and final call by cascade. It could also leave eluates pointing at a
missing source. Purge Database could permanently delete confirmed
identifications with no archive, and any user could run it.

**Bounded improvement:** `DeleteSpecimen` refuses a specimen with a
confirmed identification (cancel the investigation or clear the
identification first) and one named as an eluate source. Allowed
deletions run in one transaction with a `delete_specimen` audit entry
listing the MRN, type, received date, linked panels, reaction rows, and
cancelled state. Purge needs the new `PurgeData` permission
(Administrator once users are registered), refuses to run without an
archive when any matching specimen has a confirmed identification, and
deletes and audits in one transaction before vacuuming. The Specimens
Delete button shows the refusal.

**Not in this tick:** A purge can still remove an eluate's source when
the eluate is newer than the cutoff (the eluate keeps the accession as
text). Per-specimen delete has no reason prompt.

## Iteration 74 (this change)

**Deficiency:** Every reaction save wrote an audit entry with only the
new value, even when nothing changed. A changed result could not be
told apart from first entry, the original value was lost from the
audit trail, and per-user counts would have been inflated by repeated
grid saves. Workload covered confirmations and reviews by initials only.

**Bounded improvement:** `SaveReaction` compares with the stored row:
unchanged saves write nothing; changes record the previous phases and
extra phases as the audit before-value. The timeline names how many
recorded results changed in each entry session. `UserActivityService`
counts, per Windows login and local date range, reaction cells entered,
recorded results changed, specimens touched, identifications confirmed,
identifications cleared, and supervisor reviews, with registered names.
Analytics shows it on an **Activity by user** tab and Export CSV
includes it.

**Not in this tick:** Reaction-change reasons (a change is recorded but
no reason is asked), and per-user counts for panel and rule edits.

## Iteration 75 (this change)

**Deficiency:** A recorded reaction result could be changed after
analysis with no reason, so the audit log showed what changed but not
why. The grid also saved cell by cell, so a failure part-way left a mix
of old and new grades. Iteration 74's change count also treated a
stored empty extra-phase object as a recorded result, overcounting
changes.

**Bounded improvement:** `DescribeReactionChanges` lists each recorded
(non-NT) phase a save would replace ("Cell 3 AHG: 2+ → 0").
`SaveReactions` saves a run's grid in one transaction; once the
specimen has been analyzed, it refuses changes to recorded results
without a reason (`ReasonRequiredException`, nothing saved). The grid
shows the list and asks for the reason; the reason is stored only on
the audit entries that replaced a result, and the timeline shows it.
First entries never need a reason. Change detection now reads extra
phases properly.

**Not in this tick:** Paste/fill and Add Run copy still use the
single-cell save (they write new runs or empty cells), and a reason is
not asked before analysis has run.

## Product review (after iteration 75)

**Method:** every public write method in `DatabaseService` was checked
for an audit entry and for protection of test history.

**Now complete:** specimen save, panel import, panel delete, specimen
delete, purge, and reaction-grid save are all-or-nothing and audited;
lots and specimens that hold test history cannot be deleted; recorded
reaction results changed after analysis need a reason; workload and
activity are reported by initials, location, and login.

**Writes still without an audit entry (ranked):**
1. Panel cell typing edits (`UpdatePanelCell`): editing a lot used for a
   confirmed identification silently changed the evidence behind it.
   Addressed in iteration 76.
2. Run and reaction deletion (`DeletePanelRun`, `DeleteReactions` from
   the Reactions tab): removes recorded results with no audit entry.
3. `UpdateSpecimen` (notes, phenotype, genotype, previous antibodies,
   expiration, type) and `AddSpecimen`: no before/after values.
4. `SetSpecimenActive`, `RemovePanelExtraAntigen`, `SetPanelAntigenOrder`.
5. Analysis outputs (`AddSpecimenAntibody`, rule-outs) are regenerated
   by analysis and covered by analysis snapshots; acceptable.

**Screens:** Analytics now has three workload tabs and stays readable;
the Specimen dialog is tabbed. Reactions tab Clear and Delete Run are
the remaining one-click destructive actions on clinical data.

## Roadmap after review

1. Panel typing edits audited and reason-gated (iteration 76).
2. Audit and protect run/reaction deletion (refuse after a confirmed
   identification; reason after analysis).
3. Audit `UpdateSpecimen` with before/after values and `AddSpecimen`.
4. Audit activation, extra-antigen removal, and antigen order changes.

## Iteration 76 (this change)

**Deficiency:** Editing a panel's antigen typing wrote every cell with
no audit entry, even for a lot used for confirmed identifications.
Analysis reads typing live, so a later re-analysis or reprint of those
investigations would silently use different evidence.

**Bounded improvement:** `DescribePanelCellChanges` lists each typing or
cell-metadata difference ("Cell 3 Jka: + → -"). `SavePanelCellEdits`
saves only real changes in one transaction and writes an
`edit_panel_typing` audit entry with the change list. When the lot holds
test history, a reason is required (`ReasonRequiredException`
otherwise) and the audit entry names the linked specimens. The Panels
Save button lists the changes and asks for the reason; every linked
specimen's timeline shows the edit and its reason.

**Not in this tick:** Extra-antigen removal and antigen display order
are still unaudited (roadmap item 4).

## Iteration 77 (this change)

**Deficiency:** Clear (all reactions on a run) and Delete Run removed
recorded results with no audit entry and only a Yes/No prompt, even
after analysis had used them. The deleted grades could not be
recovered or reviewed.

**Bounded improvement:** `DeleteReactions` and `DeletePanelRun` run in
one transaction and write `clear_reactions` / `delete_run` audit
entries holding every deleted grade, the panel and run name, and the
number of graded cells. After analysis, deleting graded cells requires
a reason (`ReasonRequiredException` otherwise); the Reactions tab asks
for it in place of Yes/No. Clearing an empty run writes nothing.
Deletion stays refused after a confirmed identification. The timeline
shows both actions with the reason.

**Not in this tick:** Restoring deleted grades from the audit snapshot
(they are kept and readable in the audit log, not one-click
restorable).

## Iteration 78 (this change)

**Deficiency:** Creating a specimen and editing its type, expiration,
Active flag, notes, phenotype, genotype, previous antibodies, or
polyspecific DAT wrote no audit entry, so a changed phenotype or a
removed "previous anti-K" left no trace. Every dialog save rewrote these
fields whether or not they changed.

**Bounded improvement:** `AddSpecimen` writes `add_specimen`.
`UpdateSpecimen` compares with the stored record, writes nothing when
nothing changed, and otherwise writes one `update_specimen` entry with
before and after values of only the changed fields; a locked specimen is
refused only when something would change. `SetSpecimenActive` is audited
when the value changes. The timeline shows "Specimen details changed:
notes, phenotype" and active/inactive changes.

**Not in this tick:** Roadmap item 4 (panel activation reasons,
extra-antigen removal, antigen order).

## Iteration 79 (this change)

**Deficiency:** Adding an extra (warehouse) antigen to a panel filled
every cell with "-", a typed negative, so a column nobody had typed was
analyzed as antigen-negative on every cell. Removing an extra antigen
deleted its typing with no audit entry, even on lots used for testing,
and antigen order changes were unaudited.

**Bounded improvement:** New extra-antigen cells start as NT, so the
antigen is unknown until typed (analysis already skips untyped cells).
`add_extra_antigen` is audited when the column is new.
`RemovePanelExtraAntigen` refuses to remove an antigen typed on any
cell of a lot that holds test history; otherwise it runs in one
transaction with a `remove_extra_antigen` entry holding the removed
values. `SetPanelAntigenOrder` audits a change to a previously saved
order. The Panels tab shows the refusal. Three tests that relied on the
negative default now type the values they need.

**Not in this tick:** Existing panels already carry "-" for extra
antigens added before this change; they are not rewritten
automatically, because some may be real typing. A data-quality check
listing extra-antigen columns that are "-" on every cell would let a
supervisor review them.

## Iteration 80 (this change)

**Deficiency:** Panels that gained an extra antigen before iteration 79
still hold "-" (typed negative) on every cell for that column. Some may
be real typing, some the old automatic default; analysis cannot tell,
and nothing pointed a person at them.

**Bounded improvement:** `FindUnverifiedAllNegativeExtraAntigens` lists
extra-antigen columns that are "-" on every non-AC cell (two or more)
with no `add_extra_antigen`, no audited typing edit for that antigen,
and no `verify_extra_antigen` entry. Each appears on the Worklist as
**Check typing** (opens Panels). **Mark typing verified** records a
`verify_extra_antigen` audit entry (supervisor permission once users are
registered) and clears the item without changing typing; an audited
Edit also clears it. Nothing is rewritten automatically.

**Not in this tick:** A count card for data-quality items on the
Worklist header, and other data-quality checks (duplicate lots, panels
with no typed antigens).

## Iteration 81 (this change)

**Deficiency:** Reactions could be recorded on a panel lot after its
expiration date with no warning, and analysis and reports used them
like any other results. Testing with expired reagent red cells is a
quality-system nonconformance that should be visible and reviewed.

**Bounded improvement:** `ExpiredReagentCheck` finds runs with graded
results whose last recorded save (from the audit log, or the run date)
is after the lot's expiration date. Analysis adds a REVIEW note per run
("recorded 2026-11-05, after the lot expired 2026-11-04 … Not an
exclusion"). The Worklist lists open or unreviewed investigations as
**Expired reagent** (checked only for specimens linked to a lot that
has expired, to keep the Worklist fast). The Reactions tab asks for
confirmation before saving graded results on an expired lot.

**Not in this tick:** The worksheet does not yet print the note outside
the analysis suggestions; a lab-configurable "block" mode is not
offered (warn-and-review only).

## Iteration 82 (this change)

**Deficiency:** When several antibodies were called together, HemoIQ
checked only each one's 3 + 3 rule. It did not check that the reactions
could tell them apart. A pair where every K+ reactive cell was also E+
could be pre-filled as "anti-E; anti-K" with no warning.

**Bounded improvement:** `MultipleAntibodySeparation` takes the antibodies
called together (suspected rows meeting the ID rule, or the Final ID text
at confirmation). For each, it counts reactive cells that carry its antigen
and are typed negative for every other called antigen. A cell whose partner
antigen is NT does not count. The required number is the lab setting
**Multiple-Ab separation**: default 1, range 0–5, 0 is off. Two kinds of
exemption apply:
- **Patient history.** Antibodies in the specimen's previous antibodies, or
  confirmed on another specimen with the same MRN, need no isolated cells
  of their own. Partners still need to be negative for them.
- **Rules tab.** A rule's new "Separation not required" option can list the
  partner antigens it covers, for example anti-f: e, c, or leave the list
  blank to cover every partner.

The result appears in a Suspected Antibodies **Separation** column and in
Suggestions, and as a Yes/No prompt at Confirm ID. The criterion only
warns. The rule engine version is now 2, and the setting and rule fields
are in the analysis snapshot's rule trace.

**Not in this tick:** The clinical report does not print the separation
evaluations. Selected-cell recommendations do not yet rank cells by their
ability to separate the called antibodies.

## Likely next tick

Recommend selected cells that would satisfy a separation shortfall (for
example, a reactive-candidate K+ E- cell). After that, the Worklist
integrity checks: an active panel with no typed antigens, and duplicate
vendor lot numbers.
