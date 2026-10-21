namespace NOIR.Infrastructure.Services;

/// <summary>
/// Creates Excel files using ClosedXML.
/// Registers via IScopedService for auto-DI.
/// </summary>
public class ExcelExportService : IExcelExportService, IScopedService
{
    public byte[] CreateExcelFile(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var workbook = new ClosedXML.Excel.XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Header row
        for (var col = 0; col < headers.Count; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
        }

        // Data rows
        for (var row = 0; row < rows.Count; row++)
        {
            var rowData = rows[row];
            for (var col = 0; col < rowData.Count; col++)
            {
                var cell = worksheet.Cell(row + 2, col + 1);
                var value = rowData[col];

                switch (value)
                {
                    case null:
                        break;
                    case string s:
                        cell.Value = s;
                        break;
                    case decimal d:
                        cell.Value = d;
                        break;
                    case int i:
                        cell.Value = i;
                        break;
                    case long l:
                        cell.Value = l;
                        break;
                    case double dbl:
                        cell.Value = dbl;
                        break;
                    case DateTimeOffset dto:
                        cell.Value = dto.ToString("yyyy-MM-dd HH:mm:ss");
                        break;
                    case DateTime dt:
                        cell.Value = dt.ToString("yyyy-MM-dd HH:mm:ss");
                        break;
                    case bool b:
                        cell.Value = b ? "Yes" : "No";
                        break;
                    default:
                        cell.Value = value.ToString();
                        break;
                }
            }
        }

        // Freeze header row
        worksheet.SheetView.FreezeRows(1);

        // Auto-filter on header row
        if (headers.Count > 0)
        {
            worksheet.RangeUsed()?.SetAutoFilter();
        }

        // Auto-fit columns with max width
        worksheet.Columns().AdjustToContents();
        foreach (var column in worksheet.ColumnsUsed())
        {
            if (column.Width > 50)
                column.Width = 50;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
