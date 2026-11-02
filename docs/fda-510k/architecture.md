# System and software architecture

```mermaid
flowchart LR
  ui[WPF Views and ViewModels] --> svc[Services]
  svc --> db[DatabaseService SQLite]
  svc --> settings[settings.json]
  svc --> logs[AppLog files]
  analyzer[AntibodyAnalyzer] --> db
  analyzer --> snap[analysis_snapshots]
  db --> audit[audit_events]
```

## Layers

- **Views / ViewModels:** WPF tabs (Worklist through Rules) and dialogs.
- **Services:** `AntibodyAnalyzer`, `ReportService`, `SettingsService`, `SoftwareIdentity`, `AppLog`, seeders, vendor import.
- **Data:** `DatabaseService` — schema, migrations, audit, snapshots, lock checks.
- **Models:** specimens, panels, reactions, analysis DTOs, `RecordLockedException`.

## Safety-related modules

- Identification engine: [AntibodyAnalyzer.cs](../../AntigramIQ/Services/AntibodyAnalyzer.cs)
- Confirmation / lock: [DatabaseService.cs](../../AntigramIQ/Data/DatabaseService.cs), [AnalysisViewModel.cs](../../AntigramIQ/ViewModels/AnalysisViewModel.cs)
- Labeling: [SoftwareIdentity.cs](../../AntigramIQ/Services/SoftwareIdentity.cs), [ReportService.cs](../../AntigramIQ/Services/ReportService.cs)

## Data stores

SQLite file with foreign keys enabled. Audit table is append-only (no delete API). Analysis snapshots cascade-delete with the specimen.

## External interfaces

Optional vendor HTTPS clients under `AntigramIQ/Services/Vendors`.
