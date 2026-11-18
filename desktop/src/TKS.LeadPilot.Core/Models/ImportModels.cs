using System.Collections.Generic;

namespace TKS.LeadPilot.Core.Models;

public class CsvColumnMapping
{
    public string NameColumn { get; set; } = string.Empty;
    public string? CategoryColumn { get; set; }
    public string? SubcategoryColumn { get; set; }
    public string? WebsiteColumn { get; set; }
    public string? PhoneColumn { get; set; }
    public string? EmailColumn { get; set; }
    public string? AddressColumn { get; set; }
    public string? CityColumn { get; set; }
    public string? StateColumn { get; set; }
    public string? PostalCodeColumn { get; set; }
    public string? CountryColumn { get; set; }
    public string? TagsColumn { get; set; }
    public string? SourceColumn { get; set; }
}

public class ImportPreviewRow
{
    public int RowIndex { get; set; }
    public Business Business { get; set; } = null!;
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
    public DuplicateMatchResult? DuplicateMatch { get; set; }
}

public class ImportPreviewResult
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public int DuplicateRows { get; set; }
    public List<string> DetectedHeaders { get; set; } = [];
    public List<ImportPreviewRow> PreviewRows { get; set; } = [];
    public CsvColumnMapping ColumnMapping { get; set; } = new();
}

public class ImportExecutionResult
{
    public int TotalProcessed { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool IsSuccess => FailedCount == 0;
}
