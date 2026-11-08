# Panel details

This dialog creates a lot, edits its details, or copies an existing lot. Antigen typing for each cell is entered on the Panels grid after you save the details. The cells of a new lot start as NT (not tested), never as negative, so a lot whose antigram has not been entered cannot be read as antigen-negative. The Worklist lists it as **Untyped cells** until the typing is in.

Editing the details keeps every cell's typing. Changing **Number of Cells**, **Starting Cell #** or **Include AC** adds the new cells as NT and removes only the cells that fall outside the new range. Removing a cell with recorded grades is refused, because its typing is the evidence behind those results. Keep the range, or copy the panel to a new lot. A lot number that another stored panel already uses asks for confirmation before saving.

## Fields

- **Panel Name** is the name shown in lists.
- **Lot Number** and **Vendor** identify the manufacturer lot.
- **Number of Cells** is how many donor cells the panel contains. The usual screen-and-panel size is 16, but enter the count printed on the lot sheet.
- **Starting Cell #** is the first cell number. Enter 12 if this panel begins at cell 12.
- **Expiration Date** drives the expiring and expired warnings on the Worklist and Panels list.
- **Include AC** adds an autocontrol row.
- **Active** makes the lot available for new work. Leave it unchecked until an imported antigram has been reviewed.
- **Catalog #** and **Product line** describe the vendor product.
- **Enzyme treated** marks a lot that was manufactured as an enzyme-treated panel.

Imported lots may show a short note about the retained source file. Open **Artifact** on the Panels tab to see the hash and preview.

**Save** stores the details. **Cancel** closes without saving. **Help** opens this chapter.
