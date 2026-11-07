# Suspected Antibodies

This tab lists antibodies whose combined score meets the threshold from Preferences, and it is where a qualified professional records the final identification.

## The score grid

- **Antibody**, **Score**, **Fisher p-value**, and **Pattern Score** describe the statistical support.
- **Ag+ reactive** is the number of antigen-positive cells that reacted.
- **Ag- nonreactive** is the number of antigen-negative cells that did not react.
- **ID rule** shows whether the row meets the identification cell rule from Preferences. The usual standard is 3 antigen-positive reactive cells and 3 antigen-negative nonreactive cells.

- **Separation** appears when two or more rows meet the ID rule. It counts reactive cells that carry this antigen and are negative for every other called antigen, against the number set in Preferences (**Multiple-Ab separation**), for example **2 / 1**. A cell with an untested (NT) partner antigen does not count. **Exempt (history)** means the antibody is known from this patient's history, either in the specimen's previous antibodies or from a confirmed identification on another specimen with the same MRN. **Exempt (rule)** means a rule on the Rules tab exempts it. A count that falls short is shown in bold amber, and Suggestions says which phenotype to test next.

Green rows meet that rule. Yellow rows do not. Double-click a row to add it to **Final ID**. A green row is a candidate that meets the cell rule. A yellow row can still be added, and the tooltip says that it does not yet meet the rule.

## Supporting and conflicting cells

**Supporting cells** are antigen-positive cells that reacted, with the panel, run, cell number, and phase grades.

**Conflicting cells (Ag− but reactive)** are cells that lack the antigen and still reacted. Conflicts mean another antibody, a typing discrepancy, or a reaction that the single specificity does not explain. Resolve them before you confirm.

## Final ID

- **Final ID** is the confirmed text, for example anti-E; anti-K. Separate specificities with a semicolon. Green rows are pre-filled when they meet the ID rule.
- **Add selected** appends the highlighted row.
- **Comment** is an optional note stored with the confirmation.
- **Initials** identifies who confirmed the ID. Preferences can pre-fill this, and a successful confirmation updates the default initials.
- **Confirm ID** locks the identification onto the specimen. When Final ID lists two or more antibodies and one falls short of the separation criterion, HemoIQ lists the shortfall and asks whether to confirm anyway. It does not block confirmation. Reports, Analytics, and the Worklist then treat the specimen as confirmed.
- **Clear** removes a confirmed ID. You must enter a reason. The reason is written to the audit log.

Confirm only after you agree with the evidence. The software does not confirm an identification for you.
