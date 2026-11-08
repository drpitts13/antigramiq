using HemoIQ.Models;
using HemoIQ.Services;

namespace HemoIQ.Tests;

public class PanelCompareTests
{
    [Fact]
    public void TypingChange_AndUntypedPartner_AreListedWithoutInventingNegatives()
    {
        var left = new[]
        {
            Cell("1", ("E", "+"), ("e", "-"), ("K", "+")),
            Cell("2", ("E", "-"), ("e", "+"))
        };
        var right = new[]
        {
            Cell("1", ("E", "-"), ("e", "+"), ("Fya", "+")),
            Cell("2", ("E", "-"), ("e", "+")),
            Cell("3", ("E", "+"))
        };

        var result = PanelComparer.Compare(
            "Lot A", left, new[] { "E", "e", "K" },
            "Lot B", right, new[] { "E", "e", "Fya" });

        Assert.True(result.HasDifferences);
        Assert.Contains("Fya", result.Schema.Added);
        Assert.Contains("K", result.Schema.Removed);
        Assert.Equal(new[] { "3" }, result.OnlyRightCells);
        Assert.Empty(result.OnlyLeftCells);

        var e1 = result.TypingDiffs.Single(d => d.CellNumber == "1" && d.Antigen == "E");
        Assert.Equal("+", e1.Left);
        Assert.Equal("-", e1.Right);
        Assert.Contains("Cell 1 E is + on the selected panel and - on the comparison panel", e1.Note);

        var k1 = result.TypingDiffs.Single(d => d.CellNumber == "1" && d.Antigen == "K");
        Assert.Equal("+", k1.Left);
        Assert.Equal("NT", k1.Right);
        Assert.DoesNotContain(result.TypingDiffs, d => d.Antigen == "K" && d.Right == "-");

        Assert.Contains("Added on the comparison panel: Fya", result.Explanation);
        Assert.Contains("Missing from the comparison panel: K", result.Explanation);
        Assert.Contains("Cells only on the comparison panel: 3", result.Explanation);
    }

    [Fact]
    public void IdenticalTypings_Match()
    {
        var cells = new[] { Cell("1", ("D", "+"), ("C", "+")) };
        var result = PanelComparer.Compare("A", cells, new[] { "D", "C" }, "B", cells, new[] { "D", "C" });
        Assert.False(result.HasDifferences);
        Assert.Contains("typed cell values match", result.Explanation);
    }

    [Fact]
    public void MissingAntigenOnOneCell_IsNtNotNegative()
    {
        var left = new[] { Cell("1", ("E", "+")) };
        var right = new[] { Cell("1") };
        var result = PanelComparer.Compare("A", left, new[] { "E" }, "B", right, new[] { "E" });
        var diff = Assert.Single(result.TypingDiffs);
        Assert.Equal("NT", diff.Right);
        Assert.Equal("+", diff.Left);
        Assert.False(right[0].HasTypedAntigen("E"));
        Assert.NotEqual("-", diff.Right);
    }

    [Fact]
    public void CoverageGaps_ListAntigensFullyTypedOnSelectedButNotOnComparison()
    {
        var left = new[]
        {
            Cell("1", ("Fya", "+"), ("Jka", "+"), ("k", "+")),
            Cell("2", ("Fya", "-"), ("Jka", "-"), ("k", "+")),
            Cell("AC"),
        };
        var right = new[]
        {
            Cell("1", ("Fya", "+"), ("k", "+")),
            Cell("2", ("Jka", "+"), ("k", "+")),
            Cell("3", ("Fya", "-")),
        };
        var result = PanelComparer.Compare("Old", left, new[] { "Fya", "Jka", "k" }, "New", right, new[] { "Fya", "Jka", "k" });

        Assert.Equal(new[] { "Fya (2 of 3 cells)", "Jka (1 of 3 cells)", "k (2 of 3 cells)" }, result.CoverageGaps);
        Assert.Contains("Typed on every cell of the selected panel but not of the comparison panel: Fya (2 of 3 cells)", result.Explanation);
        Assert.Contains("Comparison panel cell(s) with nothing typed positive: 3", result.Explanation);
        Assert.Equal("3 antigens", result.LeftTyping!.Display);
        Assert.Contains("partial", result.RightTyping!.Display);
    }

    [Fact]
    public void CoverageGaps_IgnoreAntigensTheSelectedPanelDoesNotFullyType()
    {
        var left = new[] { Cell("1", ("Lua", "+")), Cell("2", ("k", "+")) };
        var right = new[] { Cell("1", ("k", "+")), Cell("2", ("k", "+")) };
        var result = PanelComparer.Compare("A", left, new[] { "Lua", "k" }, "B", right, new[] { "Lua", "k" });
        Assert.Empty(result.CoverageGaps);
    }

    private static PanelCell Cell(string number, params (string Ag, string Val)[] antigens)
    {
        var cell = new PanelCell { CellNumber = number };
        foreach (var (ag, val) in antigens)
            cell.SetAntigen(ag, val);
        return cell;
    }
}
