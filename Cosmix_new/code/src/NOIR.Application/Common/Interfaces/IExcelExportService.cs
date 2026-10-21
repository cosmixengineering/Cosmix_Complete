namespace NOIR.Application.Common.Interfaces;

/// <summary>
/// Service for creating Excel files from tabular data.
/// </summary>
public interface IExcelExportService
{
    byte[] CreateExcelFile(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows);
}
