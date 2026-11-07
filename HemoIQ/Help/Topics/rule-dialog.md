# Rule editor

This dialog adds or edits one custom rule-out rule.

- **Rule Name** is the label shown in the list.
- **Antibody** is the specificity. Pick one from the list or type a name.
- **Description** is a short note about why the laboratory uses this rule.
- **Exception Antigen** is optional. Pick or type an antigen when the rule depends on one.
- **Heterozygous OK** allows a heterozygous cell to count toward the rule-out. Leave it unchecked when only homozygous cells should count.
- **Min Ruleout Count** is how many qualifying antigen-positive nonreactive cells are required. The box starts at 3. Preferences limits the default count to 1–5; a custom rule replaces that default for this antibody.

- **Separation not required** exempts this antibody from the multiple-antibody separation criterion. Normally, when two or more antibodies are called together, each needs reactive cells that carry its antigen and are negative for the other called antigens.
- **Exempt from (antigens)** limits the exemption to the listed partner antigens, separated by commas. For example, anti-f with **e, c** (f-positive cells are always c-positive and e-positive). Leave it blank to exempt the antibody from every partner. Antigen names are case-sensitive: E and e are different.

**Save** stores the rule. The next analysis uses it for that antibody in place of the default count. **Cancel** discards the dialog. **Help** opens this chapter.

Delete a rule from the Rules tab if you want that antibody to return to the default.
