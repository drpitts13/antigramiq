# Rules

Rules are optional, per-antibody overrides of the default rule-out settings. When a rule exists for an antibody, analysis uses that rule. When it does not, analysis uses **Default rule-outs** from Preferences.

## The list

- **Name** is the label you gave the rule.
- **Antibody** is the specificity it applies to.
- **Het OK** means a heterozygous antigen-positive cell may count toward the rule-out.
- **Add** creates a rule. Ctrl+N does the same while this tab is selected. **Edit** opens the selected rule. **Delete** removes it after you confirm. **Refresh** reloads the list.
- Rules change which antibodies are ruled out. Once users are registered, adding, editing and deleting rules needs the Supervisor or Administrator role; other users see the reason and nothing changes.
- Every add, edit and delete is written to the audit log with all the rule's settings: name, antibody, exception antigen, heterozygous cells, minimum count and separation exemption. An edit keeps the old and new values, and a delete keeps the deleted rule. Saving an edit that changes nothing writes no entry.
- After a rule changes, open investigations analyzed before the change appear on the Worklist as **Stale analysis** until they are analyzed again. A changed clinical Preference has the same effect. Confirmed identifications are not reopened.
- **Help** opens this chapter.

## The detail pane

The right pane is read-only. It shows the name, antibody, exception antigen, whether heterozygous cells are allowed, the minimum rule-out count, and the description. Change those fields with **Edit**.

**Exception antigen** names an antigen that changes how the rule is applied for that antibody, when your laboratory’s rule needs one. Leave it blank when the rule is only a count and a zygosity choice.

**Min Ruleout Count** replaces the default count for this antibody only. The Ruled Out tab shows that number in the **Needed** column and explains when the custom rule was used.

Rules apply the next time you run analysis. They do not rewrite a final ID you have already confirmed.
