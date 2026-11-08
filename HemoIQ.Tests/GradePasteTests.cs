using HemoIQ.Services;

namespace HemoIQ.Tests;

public class GradePasteTests
{
    [Fact]
    public void ColumnFromSpreadsheet_NormalizesGrades_BlankInsideIsNt_TrailingBlanksDropped()
    {
        var r = GradePaste.Parse("0\r\n2\r\nw\r\n\r\nmf\r\n4+\r\nH\r\n\r\n", 10);
        Assert.True(r.Ok);
        Assert.Equal(new[] { "0", "2+", "w+", "NT", "MF", "4+", "H" }, r.Grades);
    }

    [Fact]
    public void UnrecognizedValue_RejectsWholePaste()
    {
        var r = GradePaste.Parse("0\n2=\n1+", 5);
        Assert.False(r.Ok);
        Assert.Empty(r.Grades);
        Assert.Contains("Line 2: \"2=\" is not a recognized grade.", r.Errors);
        Assert.StartsWith("Nothing was pasted.", GradePaste.ErrorMessage(r));
    }

    [Fact]
    public void TooManyValues_MultiColumn_AndEmptyClipboard_AreRejected()
    {
        Assert.Contains(GradePaste.Parse("0\n0\n0", 2).Errors, e => e.Contains("3 values were copied but only 2 cell(s) remain"));
        Assert.Contains(GradePaste.Parse("0\t1+\n0\t0", 5).Errors, e => e.Contains("one column at a time"));
        Assert.False(GradePaste.Parse("\r\n\r\n", 5).Ok);
        Assert.False(GradePaste.Parse(null, 5).Ok);
    }

    [Fact]
    public void Overwrites_OnlyDifferentEnteredGrades()
    {
        var current = new[] { "NT", "0", "2+", "", "1+" };
        var pasted = new[] { "1+", "0", "3+", "0", "1+" };
        Assert.Equal(new[] { 2 }, GradePaste.Overwrites(current, pasted));
    }
}
