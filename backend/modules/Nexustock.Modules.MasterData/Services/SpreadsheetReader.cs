using ClosedXML.Excel;
using System.Text;

namespace Nexustock.Modules.MasterData.Services;

public static class SpreadsheetReader
{
    public const int MaxDataRows = 5000;

    public static List<string[]> ReadSheetRows(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("IMPORT_PARSE_FAILED");
        var range = ws.RangeUsed();
        if (range == null)
            throw new InvalidOperationException("IMPORT_PARSE_FAILED");

        var rows = new List<string[]>();
        foreach (var row in range.Rows())
        {
            var cells = row.Cells(1, range.ColumnCount())
                .Select(c =>
                {
                    try { return c.GetFormattedString()?.Trim() ?? ""; }
                    catch { return c.GetString()?.Trim() ?? ""; }
                })
                .ToArray();
            rows.Add(cells);
        }
        return rows;
    }

    public static string RowsToCsv(IReadOnlyList<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    public static byte[] WriteXlsx(IReadOnlyList<string[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 1, c + 1).Value = rows[r][c];
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
