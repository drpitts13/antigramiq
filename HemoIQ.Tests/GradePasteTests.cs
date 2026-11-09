using HemoIQ.Services;

namespace HemoIQ.Tests;

public class GradePasteTests
{
    [Fact]
    public void ColumnFromSpreadsheet_NormalizesGrades_BlankInsideIsNt_TrailingBlanksDropped()
    {
        var r = GradePaste.ParseBlock("0\r\n2\r\nw\r\n\r\nmf\r\n4+\r\nH\r\n\r\n", 10, 1);
        Assert.True(r.Ok);
        Assert.Equal(new[] { "0", "2+", "w+", "NT", "MF", "4+", "H" }, r.Rows.Select(x => x[0]));
    }

    [Fact]
    public void UnrecognizedValue_RejectsWholePaste()
    {
        var r = GradePaste.ParseBlock("0\n2=\n1+", 5, 1);
        Assert.False(r.Ok);
        Assert.Empty(r.Rows);
        Assert.Contains("Line 2: \"2=\" is not a recognized grade.", r.Errors);
        Assert.StartsWith("Nothing was pasted.", GradePaste.ErrorMessage(r));
    }

    [Fact]
    public void NullClipboard_AndBlockWiderThanOnePhase_AreRejected()
    {
        Assert.False(GradePaste.ParseBlock(null, 5, 4).Ok);
        Assert.Contains(GradePaste.ParseBlock("0\t1+\n0\t0", 5, 1).Errors, e => e.Contains("2 columns were copied but only 1 phase(s) remain"));
    }

    [Fact]
    public void Block_TwoPhases_ParsesRowsAndColumns_PadsShortLinesWithNt()
    {
        var r = GradePaste.ParseBlock("0\t2\r\nw\t\r\n4+\r\n", 5, 3);
        Assert.True(r.Ok);
        Assert.Equal(2, r.Columns);
        Assert.Equal(new[] { "0", "2+" }, r.Rows[0]);
        Assert.Equal(new[] { "w+", "NT" }, r.Rows[1]);
        Assert.Equal(new[] { "4+", "NT" }, r.Rows[2]);
    }

    [Fact]
    public void Block_SingleColumn_BehavesLikeColumnPaste()
    {
        var r = GradePaste.ParseBlock("0\n2\nmf", 3, 1);
        Assert.True(r.Ok);
        Assert.Equal(1, r.Columns);
        Assert.Equal(new[] { "0", "2+", "MF" }, r.Rows.Select(x => x[0]));
    }

    [Fact]
    public void Block_RejectsBadCellByLineAndColumn_TooManyColumnsOrRows_AndEmpty()
    {
        var bad = GradePaste.ParseBlock("0\t2\n0\tx+", 5, 4);
        Assert.False(bad.Ok);
        Assert.Empty(bad.Rows);
        Assert.Contains("Line 2, column 2: \"x+\" is not a recognized grade.", bad.Errors);

        Assert.Contains(GradePaste.ParseBlock("0\t0\t0", 5, 2).Errors, e => e.Contains("3 columns were copied but only 2 phase(s) remain"));
        Assert.Contains(GradePaste.ParseBlock("0\n0\n0", 2, 4).Errors, e => e.Contains("3 rows were copied but only 2 cell(s) remain"));
        Assert.False(GradePaste.ParseBlock("\r\n", 5, 4).Ok);
        Assert.StartsWith("Nothing was pasted.", GradePaste.ErrorMessage(bad));
    }

    [Fact]
    public void Overwrites_OnlyDifferentEnteredGrades()
    {
        var current = new[] { "NT", "0", "2+", "", "1+" };
        var pasted = new[] { "1+", "0", "3+", "0", "1+" };
        Assert.Equal(new[] { 2 }, GradePaste.Overwrites(current, pasted));
    }
}
