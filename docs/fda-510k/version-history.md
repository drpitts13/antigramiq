# Software version history

| Version | Notes |
|---|---|
| 2.0.0 | C# / WPF rewrite. Antibody identification engine, worklist, reports, vendor import. 510(k) software controls added: intended-use labeling, no production auto-seed, audit trail, confirmed-record lock, analysis snapshots, file logging, starter DHF. |
| 2.0.0 (rename) | Product renamed from "Antibody Panel Management System" (AntibodyPanels) to AntigramIQ. Executable, namespaces, installer, and the data folder `%AppData%\AntigramIQ` use the new name. Data in `%AppData%\AntibodyPanels` (including `antibody_panels.db`) is moved on first launch; `ANTIBODY_PANELS_DB` is still honored. No change to identification logic. |
