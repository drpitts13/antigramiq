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

- Identification engine: [AntibodyAnalyzer.cs](../../HemoIQ/Services/AntibodyAnalyzer.cs)
- Confirmation / lock: [DatabaseService.cs](../../HemoIQ/Data/DatabaseService.cs), [AnalysisViewModel.cs](../../HemoIQ/ViewModels/AnalysisViewModel.cs)
- Labeling: [SoftwareIdentity.cs](../../HemoIQ/Services/SoftwareIdentity.cs), [ReportService.cs](../../HemoIQ/Services/ReportService.cs)

## Data stores

SQLite file with foreign keys enabled. Audit table is append-only (no delete API). Analysis snapshots cascade-delete with the specimen.

## External interfaces

Optional vendor HTTPS clients under `HemoIQ/Services/Vendors`.
