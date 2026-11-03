# Software version history

| Version | Notes |
|---|---|
| 2.0.0 | C# / WPF rewrite. Antibody identification engine, worklist, reports, vendor import. 510(k) software controls added: intended-use labeling, no production auto-seed, audit trail, confirmed-record lock, analysis snapshots, file logging, starter DHF. |
| 2.0.0 (rename) | Product renamed from "Antibody Panel Management System" (AntibodyPanels) to HemoIQ (briefly AntigramIQ in pre-release builds). Executable, namespaces, installer, and the data folder `%AppData%\HemoIQ` use the new name. On first launch, data in `%AppData%\AntigramIQ` or, if absent, `%AppData%\AntibodyPanels` is moved and its database renamed to `hemoiq.db`; `ANTIBODY_PANELS_DB` is still honored. No change to identification logic. |
