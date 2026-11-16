using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TKS.LeadPilot.Core.Enums;
using TKS.LeadPilot.Core.Interfaces;
using TKS.LeadPilot.Core.Models;

namespace TKS.LeadPilot.Core.Services;

public class CsvImporterService : ICsvImporterService
{
    private readonly IBusinessRepository _repository;
    private readonly IDeduplicationService _dedupService;

    public CsvImporterService(IBusinessRepository repository, IDeduplicationService dedupService)
    {
        _repository = repository;
        _dedupService = dedupService;
    }

    public CsvColumnMapping AutoDetectMapping(IReadOnlyList<string> headers)
    {
        var mapping = new CsvColumnMapping();

        foreach (var header in headers)
        {
            var h = header.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");

            if (string.IsNullOrEmpty(mapping.NameColumn) && IsMatch(h, "name", "business name", "company", "company name", "organization", "firm", "title"))
                mapping.NameColumn = header;
            else if (string.IsNullOrEmpty(mapping.CategoryColumn) && IsMatch(h, "category", "industry", "type", "sector", "niche"))
                mapping.CategoryColumn = header;
            else if (string.IsNullOrEmpty(mapping.SubcategoryColumn) && IsMatch(h, "subcategory", "sub category", "sub industry"))
                mapping.SubcategoryColumn = header;
            else if (string.IsNullOrEmpty(mapping.WebsiteColumn) && IsMatch(h, "website", "url", "web", "site", "domain", "link", "homepage"))
                mapping.WebsiteColumn = header;
            else if (string.IsNullOrEmpty(mapping.PhoneColumn) && IsMatch(h, "phone", "telephone", "tel", "mobile", "contact number", "cell", "whatsapp"))
                mapping.PhoneColumn = header;
            else if (string.IsNullOrEmpty(mapping.EmailColumn) && IsMatch(h, "email", "e mail", "mail", "contact email"))
                mapping.EmailColumn = header;
            else if (string.IsNullOrEmpty(mapping.AddressColumn) && IsMatch(h, "address", "street", "street address", "location", "address line 1"))
                mapping.AddressColumn = header;
            else if (string.IsNullOrEmpty(mapping.CityColumn) && IsMatch(h, "city", "town", "district"))
                mapping.CityColumn = header;
            else if (string.IsNullOrEmpty(mapping.StateColumn) && IsMatch(h, "state", "province", "region"))
                mapping.StateColumn = header;
            else if (string.IsNullOrEmpty(mapping.PostalCodeColumn) && IsMatch(h, "postal code", "zip", "zip code", "postal"))
                mapping.PostalCodeColumn = header;
            else if (string.IsNullOrEmpty(mapping.CountryColumn) && IsMatch(h, "country", "nation"))
                mapping.CountryColumn = header;
            else if (string.IsNullOrEmpty(mapping.TagsColumn) && IsMatch(h, "tags", "tag", "labels", "keywords"))
                mapping.TagsColumn = header;
        }

        return mapping;
    }

    public async Task<ImportPreviewResult> PreviewCsvAsync(Stream stream, DeduplicationOptions? dedupOptions = null, CancellationToken ct = default)
    {
        var rows = ParseCsvStream(stream);
        if (rows.Count == 0)
        {
            return new ImportPreviewResult();
        }

        var headers = rows[0];
        var mapping = AutoDetectMapping(headers);
        var existingBusinesses = await _repository.GetAllAsync(ct: ct);

        var preview = new ImportPreviewResult
        {
            TotalRows = rows.Count - 1,
            DetectedHeaders = headers,
            ColumnMapping = mapping
        };

        var headerMap = headers.Select((h, idx) => (Header: h, Index: idx))
                               .ToDictionary(x => x.Header, x => x.Index, StringComparer.OrdinalIgnoreCase);

        int valid = 0;
        int invalid = 0;
        int duplicates = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var row = rows[i];
            var previewRow = new ImportPreviewRow { RowIndex = i };

            try
            {
                var business = MapRowToBusiness(row, headerMap, mapping);
                previewRow.Business = business;

                if (string.IsNullOrWhiteSpace(business.Name))
                {
                    previewRow.IsValid = false;
                    previewRow.ValidationError = "Business Name is required.";
                    invalid++;
                }
                else
                {
                    valid++;
                    var dedupMatch = _dedupService.CheckDuplicate(business, existingBusinesses, dedupOptions);
                    if (dedupMatch.IsDuplicate)
                    {
                        previewRow.DuplicateMatch = dedupMatch;
                        duplicates++;
                    }
                }
            }
            catch (Exception ex)
            {
                previewRow.IsValid = false;
                previewRow.ValidationError = ex.Message;
                invalid++;
            }

            if (preview.PreviewRows.Count < 50)
            {
                preview.PreviewRows.Add(previewRow);
            }
        }

        preview.ValidRows = valid;
        preview.InvalidRows = invalid;
        preview.DuplicateRows = duplicates;

        return preview;
    }

    public async Task<ImportExecutionResult> ExecuteImportAsync(
        Stream stream,
        CsvColumnMapping? mapping = null,
        DeduplicationStrategy strategy = DeduplicationStrategy.Skip,
        DeduplicationOptions? dedupOptions = null,
        CancellationToken ct = default)
    {
        var rows = ParseCsvStream(stream);
        var result = new ImportExecutionResult();

        if (rows.Count <= 1) return result;

        var headers = rows[0];
        mapping ??= AutoDetectMapping(headers);

        var headerMap = headers.Select((h, idx) => (Header: h, Index: idx))
                               .ToDictionary(x => x.Header, x => x.Index, StringComparer.OrdinalIgnoreCase);

        var existingBusinesses = await _repository.GetAllAsync(ct: ct);
        var existingMap = existingBusinesses.ToDictionary(b => b.Id);

        var toInsert = new List<Business>();
        var toUpdate = new List<Business>();

        for (int i = 1; i < rows.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            result.TotalProcessed++;

            var row = rows[i];
            try
            {
                var incoming = MapRowToBusiness(row, headerMap, mapping);
                if (string.IsNullOrWhiteSpace(incoming.Name))
                {
                    result.FailedCount++;
                    result.Errors.Add($"Row {i}: Missing required Business Name");
                    continue;
                }

                var dedupMatch = _dedupService.CheckDuplicate(incoming, existingMap.Values, dedupOptions);

                if (dedupMatch.IsDuplicate && dedupMatch.ExistingBusiness != null)
                {
                    var existing = dedupMatch.ExistingBusiness;
                    switch (strategy)
                    {
                        case DeduplicationStrategy.Skip:
                            result.SkippedCount++;
                            break;

                        case DeduplicationStrategy.Merge:
                            _dedupService.MergeBusinesses(existing, incoming);
                            if (!toUpdate.Any(b => b.Id == existing.Id)) toUpdate.Add(existing);
                            result.UpdatedCount++;
                            break;

                        case DeduplicationStrategy.Overwrite:
                            existing.Name = incoming.Name;
                            existing.Category = incoming.Category;
                            existing.City = incoming.City;
                            existing.PhonePrimary = incoming.PhonePrimary;
                            existing.EmailPrimary = incoming.EmailPrimary;
                            existing.WebsiteUrl = incoming.WebsiteUrl;
                            existing.WebsiteStatus = incoming.WebsiteStatus;
                            existing.AddressLine1 = incoming.AddressLine1;
                            existing.Tags = incoming.Tags;
                            existing.UpdatedAtUtc = DateTime.UtcNow;
                            existing.Version += 1;
                            if (!toUpdate.Any(b => b.Id == existing.Id)) toUpdate.Add(existing);
                            result.UpdatedCount++;
                            break;
                    }
                }
                else
                {
                    toInsert.Add(incoming);
                    existingMap[incoming.Id] = incoming;
                    result.CreatedCount++;
                }
            }
            catch (Exception ex)
            {
                result.FailedCount++;
                result.Errors.Add($"Row {i}: {ex.Message}");
            }
        }

        if (toInsert.Count > 0)
        {
            await _repository.AddBatchAsync(toInsert, ct);
        }

        if (toUpdate.Count > 0)
        {
            await _repository.UpdateBatchAsync(toUpdate, ct);
        }

        return result;
    }

    public Task<byte[]> ExportToCsvAsync(IEnumerable<Business> businesses, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        // UTF-8 BOM for Microsoft Excel compatibility
        var preamble = Encoding.UTF8.GetPreamble();

        // Header
        sb.AppendLine("Id,Name,Category,Subcategory,City,StateProvince,Country,PhonePrimary,EmailPrimary,WebsiteUrl,WebsiteStatus,LeadScore,PipelineStage,Tags,CreatedAtUtc");

        foreach (var b in businesses)
        {
            ct.ThrowIfCancellationRequested();
            var line = string.Join(",",
                EscapeCsv(b.Id),
                EscapeCsv(b.Name),
                EscapeCsv(b.Category),
                EscapeCsv(b.Subcategory),
                EscapeCsv(b.City),
                EscapeCsv(b.StateProvince),
                EscapeCsv(b.Country),
                EscapeCsv(b.PhonePrimary),
                EscapeCsv(b.EmailPrimary),
                EscapeCsv(b.WebsiteUrl),
                EscapeCsv(b.WebsiteStatus.ToString()),
                b.LeadScore.ToString(),
                EscapeCsv(b.PipelineStage?.Name ?? b.PipelineStageId),
                EscapeCsv(b.Tags),
                b.CreatedAtUtc.ToString("o")
            );
            sb.AppendLine(line);
        }

        var contentBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fullBytes = new byte[preamble.Length + contentBytes.Length];
        Buffer.BlockCopy(preamble, 0, fullBytes, 0, preamble.Length);
        Buffer.BlockCopy(contentBytes, 0, fullBytes, preamble.Length, contentBytes.Length);

        return Task.FromResult(fullBytes);
    }

    private static Business MapRowToBusiness(List<string> row, Dictionary<string, int> headerMap, CsvColumnMapping mapping)
    {
        string GetVal(string? colName)
        {
            if (string.IsNullOrWhiteSpace(colName)) return string.Empty;
            if (headerMap.TryGetValue(colName, out var idx) && idx < row.Count)
            {
                return row[idx].Trim();
            }
            return string.Empty;
        }

        var name = GetVal(mapping.NameColumn);
        var category = GetVal(mapping.CategoryColumn);
        var subcategory = GetVal(mapping.SubcategoryColumn);
        var website = GetVal(mapping.WebsiteColumn);
        var phone = GetVal(mapping.PhoneColumn);
        var email = GetVal(mapping.EmailColumn);
        var address = GetVal(mapping.AddressColumn);
        var city = GetVal(mapping.CityColumn);
        var state = GetVal(mapping.StateColumn);
        var postalCode = GetVal(mapping.PostalCodeColumn);
        var country = GetVal(mapping.CountryColumn);
        var tags = GetVal(mapping.TagsColumn);

        var business = new Business
        {
            Name = name,
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
            Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory,
            WebsiteUrl = string.IsNullOrWhiteSpace(website) ? null : website,
            WebsiteStatus = !string.IsNullOrWhiteSpace(website) ? WebsiteStatus.ActiveWebsite : WebsiteStatus.NoWebsite,
            PhonePrimary = string.IsNullOrWhiteSpace(phone) ? null : phone,
            EmailPrimary = string.IsNullOrWhiteSpace(email) ? null : email,
            AddressLine1 = string.IsNullOrWhiteSpace(address) ? null : address,
            City = string.IsNullOrWhiteSpace(city) ? "Unspecified" : city,
            StateProvince = string.IsNullOrWhiteSpace(state) ? null : state,
            PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode,
            Country = string.IsNullOrWhiteSpace(country) ? "Pakistan" : country,
            Tags = string.IsNullOrWhiteSpace(tags) ? null : tags,
            SourceProvider = "CSV_IMPORT"
        };

        return business;
    }

    public static List<List<string>> ParseCsvStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<List<string>>();
        var currentField = new StringBuilder();
        var currentRow = new List<string>();
        bool inQuotes = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++; // skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',' || c == '\t' || c == ';')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (!inQuotes)
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                if (currentRow.Any(field => !string.IsNullOrWhiteSpace(field)))
                {
                    rows.Add(currentRow);
                }
                currentRow = new List<string>();
            }
            else
            {
                currentField.Append('\n');
            }
        }

        if (currentRow.Count > 0 || currentField.Length > 0)
        {
            currentRow.Add(currentField.ToString());
            if (currentRow.Any(field => !string.IsNullOrWhiteSpace(field)))
            {
                rows.Add(currentRow);
            }
        }

        return rows;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static bool IsMatch(string header, params string[] candidates)
    {
        return candidates.Any(c => string.Equals(header, c, StringComparison.OrdinalIgnoreCase));
    }
}
