# Preferences

Open **Tools → Preferences** to set laboratory defaults. **Save** writes them to this computer. **Cancel** discards the dialog. **Help** opens this chapter.

## Identity and thresholds

- **Lab name** and **Department** identify the laboratory on this workstation.
- **Score threshold** is the minimum combined score for the suspected list. The allowed range is 0.30 to 0.95.
- **ID cell rule** is how many antigen-positive reactive cells and antigen-negative nonreactive cells are required before a row is marked as meeting the identification rule. AABB 3 + 3 is the usual standard. The Suspected Antibodies tab turns those rows green.
- **Multiple-Ab separation** applies when two or more antibodies are called together. It is the number of reactive cells each antibody needs that carry its antigen and are negative for every other called antigen. For anti-e, anti-f, and anti-Lea, that means reactive e+ f- Lea- cells, reactive e- f+ Lea- cells, and reactive e- f- Lea+ cells. The range is 0 to 5; 0 turns the check off, and the default is 1. An antibody known from patient history needs no cells of its own, and a rule on the Rules tab can exempt an antibody. This is a suggested criterion: HemoIQ warns but does not block.
- **ACS rule-outs** is how many times each clinically significant antibody must be ruled out before the application can suggest that all clinically significant antibodies are ruled out. The range is 1 to 5.
- **Default rule-outs** is the minimum number of qualifying antigen-positive nonreactive cells required to rule out an antibody when no custom rule exists. The range is 1 to 5. A rule on the Rules tab overrides this for that antibody.

## Specimens and the database

- **Default specimen type** pre-selects serum, plasma, or eluate on a new specimen.
- **Specimen dating (days)** pre-fills expiration when you add a specimen. 0 leaves the date blank. Three days is a typical antibody workup dating interval.
- **Expiration warning (days)** is how far ahead the Worklist and the lists mark a specimen or panel as expiring.
- **Require supervisor review before an investigation is Completed** adds a sixth status step. After an identification is confirmed, a second person must record **Supervisor Review…** on the Specimens tab before the investigation counts as Completed. Until then it shows *Awaiting supervisor review* and appears on the Worklist.
- **Dating look-back (days)** is 1–365, default 90. A transfusion this recent before collection makes the specimen-dating check apply (collection + Specimen dating days).
- **RhIG passive window (days)** is 1–365, default 84 (12 weeks). Anti-D within this many days after the recorded RhIG date is flagged for review as possibly passive.
- **Turnaround threshold (days)** is 1–60, default 3. Open investigations older than this are listed as overdue on Analytics.
- **Recent transfusion (days)** is 1–365, default 90. If the specimen is Transfused and its last transfusion is this many days or fewer before the specimen date, the serologic phenotype is treated as possible donor cells and is not used against an alloantibody. The value is saved with each analysis snapshot.
- **Max database size (MB)** warns when the database file reaches 80 percent of this size. The allowed range is 50 to 10240 MB. The warning can offer to open Purge Database.
- **Default initials** pre-fills **Initials** on Confirm ID. Confirming an identification updates this value to the initials you used.
- **Show inactive specimens and panels by default** turns on **Show Inactive** when those tabs open.

## Phases and the reaction grid

- **Hide ruled-out antigen columns on the Reactions tab** starts Reactions with **Hide ruled-out** checked.
- **Extra phases** adds optional columns after the classic IS / 37°C / AHG / CC grid. Leave the box blank to keep the classic grid.
- **RT**, **PEG**, **Gel**, and **Solid** are suggested columns. Gel, Solid, and PEG count as IAT-like evidence. You can also type custom names.
- **Titer grid (Dil1–Dil128)** adds serial-dilution columns. The last reactive tube is the titer endpoint. Dilution grades are not IAT, and they do not rule out or identify an antibody.

Changes apply to the next analysis and to new specimens. They do not rewrite identifications you have already confirmed.

## Users and roles

**Users & roles…** registers people by their Windows login, with initials, name, and a role:

| Role | Can also |
|---|---|
| Technologist | Everything not listed below: specimens, reactions, analysis, confirming an identification, cancelling, reports |
| Supervisor | Record supervisor review, clear a confirmed identification, reinstate a cancelled investigation |
| Administrator | Everything a Supervisor can, plus change Preferences and manage users |

While **no user is registered**, everyone may do everything, as in earlier versions. Once the first user is saved, the rules apply to every Windows login, and an unregistered login can do only Technologist work. To start, select **Use my login**, enter your initials, and save yourself as an **Administrator**. The first user must be an Administrator, the last active Administrator cannot be demoted or deactivated, and initials must be unique among active users.

When users are registered, the initials entered for a supervisor review must be the signed-in user's own. Every audit entry records the signed-in Windows login, and user changes are audited.
