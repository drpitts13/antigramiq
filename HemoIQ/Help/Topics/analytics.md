# Analytics

Analytics counts confirmed antibody identifications over a date range. It uses only identifications a user confirmed with **Confirm ID**. Suspected rows that were never confirmed are not included. Cancelled investigations are left out of every figure on this tab: antibody counts, turnaround and workload.

## Date range

- **From** includes confirmations on or after that date.
- **To** includes confirmations on or before that date.
- **30 days**, **90 days**, and **12 months** set the range through today.
- **All time** includes every confirmed identification, including any that have no confirmation date.
- **Refresh** reloads the cards, charts, and grid.
- **Export CSV** saves the detail rows.
- **Help** opens this chapter.

The line under the toolbar names the range that is actually in use.

## What the numbers mean

- **Confirmed specimens** is how many specimens had a confirmation in the range.
- **Distinct IDs** is how many different specificities appear.
- **Total occurrences** counts each specificity once per specimen. A call of anti-E; anti-K adds one to anti-E and one to anti-K. Antigen names are case-sensitive, so anti-E and anti-e are counted separately; only the "anti-" prefix ignores capitals.
- **Most common** is the specificity with the highest occurrence count.

The two charts show counts by specificity and the trend over time. The grid under the charts lists the same breakdown. Use the grid when you need the exact counts behind a bar.

## Drill-down

Select a row to list the investigations behind its count in the panel beside the antibody grid. Then choose **Open**, or double-click, to go to the investigation on the Specimens tab. You can select a row in:
- the antibody grid: the confirmed investigations with that specificity in the range, newest first, with who confirmed each;
- **Workload by technologist**: that person's confirmations and supervisor reviews in the range;
- **By ordering location**: the investigations received from that location in the range, marked Confirmed, Open, or Inactive.

Each list uses the same rules as the count it came from, so the number of rows matches the count. Changing the date range or choosing **Refresh** clears the list, because the old list may no longer match the new counts.

Analytics describes work this laboratory confirmed in the application. It is not a population frequency and it does not change any specimen.

## Turnaround

The purple and red cards measure workflow time for investigations **received** in the selected date range. Cancelled investigations are not counted.

- **Received → confirmed** is the median time from the received date to the confirmed identification, in days, with the 90th percentile and the number of investigations. When a received time is recorded on the specimen it is used exactly; otherwise the received date is counted from midnight and the figure is an upper bound.
- **Confirmed → reviewed** is the median time from confirmation to supervisor review, in hours, for investigations that have a review.
- **Overdue open investigations** counts active investigations with no confirmed identification that are older than the Preferences **Turnaround threshold (days)**, default 3.

The overdue list at the bottom shows each case, oldest first, with its current status. Select one and choose **Open** to go to it. **Export CSV** adds the turnaround figures and the overdue list after the antibody counts. The 90th percentile uses the nearest-rank method.

## Workload by technologist

The table beside the overdue list shows work done in the selected date range by each set of initials.

- **Confirmed** counts identifications whose **Identified by** is these initials and whose confirmation date is in the range.
- **Multiple Ab** counts the confirmations that name more than one antibody, such as anti-E; anti-K.
- **Reviews** counts supervisor reviews recorded under these initials with a review date in the range.
- **Median TAT** is the median time from receipt to confirmation, in days, for this person's confirmations.

Initials are matched regardless of case. When the initials belong to a user in **Preferences > Users & roles**, the name is shown too. Cancelled investigations are not counted. **Export CSV** adds this table after the turnaround figures.

## By ordering location

The **By ordering location** tab beside **Workload by technologist** groups investigations **received** in the date range by the ordering location recorded on the specimen. Locations match regardless of case and surrounding spaces. Specimens with no ordering location are listed last as **(not recorded)**.

- **Received** is how many investigations came from that location.
- **Confirmed** is how many of them have a confirmed identification.
- **Open** is how many are active with no confirmed identification.
- **Median TAT** is the median time from receipt to confirmation, in days.

Cancelled investigations are not counted. **Export CSV** adds this table at the end.

## Activity by user

The **Activity by user** tab counts work recorded in the audit log, by Windows login and by the local date of each event in the range. It covers reaction entry and amendments, which initials-based workload cannot.

- **Entered** is reaction cells saved for the first time, or saved where every phase was still NT.
- **Changed** is reaction cells whose recorded result was replaced. The audit entry keeps the original value.
- **Specimens** is how many different specimens the user saved reactions on, confirmed, cleared, or reviewed.
- **Confirmed**, **Cleared**, and **Reviews** count identifications confirmed, identifications cleared (amendments), and supervisor reviews.

When the login belongs to a user in **Preferences > Users & roles**, the name and initials are shown with it. Saving a reaction grid without changing a cell writes nothing to the audit log, so it is not counted. **Export CSV** adds this table at the end.
