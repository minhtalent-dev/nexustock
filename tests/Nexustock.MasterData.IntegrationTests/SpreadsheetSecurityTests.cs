using System.Collections.Generic;
using System.IO;
using Nexustock.Modules.MasterData.Services;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class SpreadsheetSecurityTests
{
    [Theory]
    [InlineData("=SUM(1,2)", "'=SUM(1,2)")]
    [InlineData("+12345", "'+12345")]
    [InlineData("-cmd|' /C calc'!A0", "'-cmd|' /C calc'!A0")]
    [InlineData("@SUM(A1:A10)", "'@SUM(A1:A10)")]
    [InlineData("\ttab_started", "'\ttab_started")]
    [InlineData("\rcr_started", "'\rcr_started")]
    [InlineData("NORMAL_TEXT", "NORMAL_TEXT")]
    public void SanitizeFormula_NeutralizesDangerousPrefixes(string input, string expected)
    {
        var result = SpreadsheetReader.SanitizeFormula(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RowsToCsv_NeutralizesDangerousFormula_InCsvOutput()
    {
        var rows = new List<string[]>
        {
            new[] { "Header1", "Header2" },
            new[] { "=1+1", "NormalValue" }
        };

        var csv = SpreadsheetReader.RowsToCsv(rows);
        Assert.Contains("'=1+1", csv);
    }

    [Fact]
    public void WriteXlsx_NeutralizesDangerousFormula_WhenWorkbookIsReopened()
    {
        var bytes = SpreadsheetReader.WriteXlsx(new List<string[]>
        {
            new[] { "Header" },
            new[] { "=1+1" }
        });

        using var stream = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var cell = workbook.Worksheet(1).Cell(2, 1);

        Assert.Equal("=1+1", cell.GetString());
        Assert.Equal(ClosedXML.Excel.XLDataType.Text, cell.DataType);
        Assert.True(cell.Style.IncludeQuotePrefix);
        Assert.False(cell.HasFormula);
    }
}
