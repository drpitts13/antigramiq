using System.Text.Json;
using HemoIQ.Models;
using HemoIQ.Services;
using HemoIQ.Tests.Infrastructure;

namespace HemoIQ.Tests;

[Collection("PersistentDatabase")]
public class SpecimenEditAuditTests
{
    private static List<AuditEvent> Audits(IsolatedDatabase iso, string acc, string action) =>
        iso.Db.GetAuditEventsForEntity("specimen", acc).Where(e => e.Action == action).ToList();

    [Fact]
    public void AddSpecimen_IsAudited()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SE-1", "plasma", "2026-12-01", createdDate: "2026-11-01");

        var audit = Assert.Single(Audits(iso, "SE-1", "add_specimen"));
        using var doc = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal("plasma", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("2026-11-01", doc.RootElement.GetProperty("createdDate").GetString());
    }

    [Fact]
    public void UpdateSpecimen_AuditsOnlyChangedFields_WithBeforeAndAfter()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SE-2", "serum", null, notes: "routine", phenotype: "R1r");

        iso.Db.UpdateSpecimen("SE-2", "serum", null, true, "hemolyzed, recollect requested", "R1R1", null, null, null);

        var audit = Assert.Single(Audits(iso, "SE-2", "update_specimen"));
        using var before = JsonDocument.Parse(audit.BeforeJson!);
        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(new[] { "notes", "phenotype" }, after.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("routine", before.RootElement.GetProperty("notes").GetString());
        Assert.Equal("R1R1", after.RootElement.GetProperty("phenotype").GetString());

        var timeline = InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("SE-2")!);
        Assert.Contains(timeline, e => e.What == "Specimen details changed: notes, phenotype");
    }

    [Fact]
    public void UpdateSpecimen_Unchanged_WritesNothing_EvenWhenLocked()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SE-3", "serum", null, notes: "n");
        iso.Db.SetSpecimenFinalCall("SE-3", "anti-K", null, "TE");

        iso.Db.UpdateSpecimen("SE-3", "serum", null, true, "n", null, null, null, null);

        Assert.Empty(Audits(iso, "SE-3", "update_specimen"));
        Assert.Throws<RecordLockedException>(() =>
            iso.Db.UpdateSpecimen("SE-3", "serum", null, true, "changed", null, null, null, null));
        Assert.Equal("n", iso.Db.GetSpecimen("SE-3")!.Notes);
    }

    [Fact]
    public void SetSpecimenActive_IsAudited_OnlyWhenItChanges()
    {
        using var iso = new IsolatedDatabase();
        iso.Db.AddSpecimen("SE-4", "serum", null, isActive: true);

        iso.Db.SetSpecimenActive("SE-4", true);
        iso.Db.SetSpecimenActive("SE-4", false);

        Assert.Single(Audits(iso, "SE-4", "set_specimen_active"));
        Assert.False(iso.Db.GetSpecimen("SE-4")!.IsActive);
        Assert.Contains(InvestigationTimeline.Build(iso.Db, iso.Db.GetSpecimen("SE-4")!),
            e => e.What == "Specimen marked inactive");
    }

    [Fact]
    public void DialogSave_EditingOnlyNotes_WritesOneSpecimenAudit()
    {
        using var iso = new IsolatedDatabase();
        SpecimenSaveService.Save(iso.Db, new SpecimenEdit { AccessionNumber = "SE-5", Notes = "a" }, isNew: true);
        var before = iso.Db.GetAuditEventsForEntity("specimen", "SE-5").Count;

        SpecimenSaveService.Save(iso.Db, new SpecimenEdit { AccessionNumber = "SE-5", Notes = "b" }, isNew: false);

        var added = iso.Db.GetAuditEventsForEntity("specimen", "SE-5").Skip(before).ToList();
        Assert.Equal("update_specimen", Assert.Single(added).Action);
    }
}
