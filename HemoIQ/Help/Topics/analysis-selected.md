# Selected Cells

Selected Cells lists unused panel cells ranked by how well they separate the antibodies that are still candidates.

The note on the tab says these are testing suggestions, not identifications. The grid columns are:

- **Panel**, **Lot**, and **Cell** — where to find the cell.
- **Profile** — the antigen pattern that makes the cell useful.
- **Score** — how much the cell would discriminate among the remaining candidates.
- **Why this cell** — the short reason, such as which antibody it could rule out or support.

When two or more antibodies are called together and one falls short of the multiple-antibody separation criterion (see **Separation** on Suspected Antibodies), cells with the missing phenotype rank first. For anti-E with anti-K, a K+ E- cell would rank first if anti-K falls short. **Why this cell** says the cell would count toward the criterion if the serum reacts with it, and Suggestions names these cells. A rule on the Rules tab that exempts a partner antigen removes that antigen from the phenotype needed.

Use the list to choose the next cells to test. Enter the new grades on **Reactions**, then **Run Analysis** again. The suggestions box at the top of Analysis repeats the highest-priority next steps when the engine has them.

A suggestion does not add a grade and does not change the final ID. If you test the cell, the new grades become part of the evidence on the next analysis.
