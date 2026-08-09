using FileCompare.Models;
using FileCompare.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FileCompare.Components.Pages;

public partial class Home : ComponentBase
{
    private static readonly string[] DefaultKeyColumnCandidates = { "PersonalNr", "Lohnart", "TextLohnart" };
    private const string DefaultCompareColumnCandidate = "Betrag";

    [Inject] private FileParserService ParserService { get; set; } = default!;
    [Inject] private ComparisonService ComparisonService { get; set; } = default!;
    [Inject] private GroupingService GroupingService { get; set; } = default!;
    [Inject] private ExcelExportService ExcelExportService { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    private FileTypeSelection _fileTypeSelection = FileTypeSelection.Default();

    private byte[]? _converterRawBytes;
    private byte[]? _clientRawBytes;
    private ParsedFile? _converterFile;
    private ParsedFile? _clientFile;
    private string? _converterFileName;
    private string? _clientFileName;
    private string? _converterError;
    private string? _clientError;

    private readonly List<string> _headerMismatchErrors = new();
    private readonly List<string> _rowParseErrors = new();
    private bool _headersReady;

    private List<string> _availableKeyColumns = new();
    private List<string> _selectedKeyColumns = new();
    private List<string> _numericCandidateColumns = new();
    private List<string> _nonNumericExcludedColumns = new();
    private List<string> _otherColumns = new();
    private string? _compareColumnName;

    private DifferenceGroupingConfig _groupingConfig = DifferenceGroupingConfig.Default();

    private bool _isComparing;
    private ComparisonResult? _rawComparisonResult;
    private ComparisonResult? _comparisonResult;
    private List<string> _comparisonWarnings = new();

    private bool CanCompare => _selectedKeyColumns.Count > 0 && !string.IsNullOrEmpty(_compareColumnName);

    private void OnFileError(bool isConverter, string message)
    {
        if (isConverter)
        {
            _converterError = message;
        }
        else
        {
            _clientError = message;
        }
        StateHasChanged();
    }

    private async Task OnFileLoadedAsync(bool isConverter, string fileName, byte[] content)
    {
        if (isConverter)
        {
            _converterRawBytes = content;
            _converterFileName = fileName;
        }
        else
        {
            _clientRawBytes = content;
            _clientFileName = fileName;
        }

        await ParseAndStoreAsync(isConverter, content, fileName);
        RebuildRowParseErrors();
        await ValidateAndResetAsync();
    }

    private async Task OnFileTypeSelectionChangedAsync(FileTypeSelection selection)
    {
        _fileTypeSelection = selection;

        if (_converterRawBytes is not null)
        {
            await ParseAndStoreAsync(true, _converterRawBytes, _converterFileName ?? "Converter output file");
        }
        if (_clientRawBytes is not null)
        {
            await ParseAndStoreAsync(false, _clientRawBytes, _clientFileName ?? "Client expected file");
        }

        RebuildRowParseErrors();
        await ValidateAndResetAsync();
    }

    private async Task ParseAndStoreAsync(bool isConverter, byte[] bytes, string fileName)
    {
        try
        {
            var parsed = await Task.Run(() => ParseBytes(bytes));

            if (isConverter)
            {
                _converterFile = parsed;
                _converterError = null;
            }
            else
            {
                _clientFile = parsed;
                _clientError = null;
            }
        }
        catch (Exception ex)
        {
            if (isConverter)
            {
                _converterFile = null;
            }
            else
            {
                _clientFile = null;
            }
            OnFileError(isConverter, $"Failed to parse '{fileName}': {ex.Message}");
        }
    }

    private string AcceptExtensionsForCurrentFormat => _fileTypeSelection.Format switch
    {
        InputFileFormat.Csv => ".csv",
        _ => ".csv,.txt",
    };

    private ParsedFile ParseBytes(byte[] bytes) => _fileTypeSelection.Format switch
    {
        InputFileFormat.Csv => ParserService.ParseDelimitedText(bytes, ','),
        _ => ParserService.ParseDelimitedText(bytes, _fileTypeSelection.Delimiter),
    };

    private void RebuildRowParseErrors()
    {
        _rowParseErrors.Clear();
        if (_converterFile is not null)
        {
            _rowParseErrors.AddRange(_converterFile.RowErrors.Select(e => $"Converter output file — {e}"));
        }
        if (_clientFile is not null)
        {
            _rowParseErrors.AddRange(_clientFile.RowErrors.Select(e => $"Client expected file — {e}"));
        }
    }

    private Task ValidateAndResetAsync()
    {
        _headerMismatchErrors.Clear();
        _headersReady = false;
        _comparisonResult = null;
        _rawComparisonResult = null;
        _comparisonWarnings.Clear();

        if (_converterFile is null || _clientFile is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            ParserService.ValidateHeaders(_converterFile, _clientFile);
        }
        catch (HeaderMismatchException ex)
        {
            if (ex.OnlyInConverterFile.Count > 0)
            {
                _headerMismatchErrors.Add($"Only in Converter output file: {string.Join(", ", ex.OnlyInConverterFile)}");
            }
            if (ex.OnlyInClientFile.Count > 0)
            {
                _headerMismatchErrors.Add($"Only in Client expected file: {string.Join(", ", ex.OnlyInClientFile)}");
            }
            return Task.CompletedTask;
        }

        _headersReady = true;
        InitializeKeySelectionDefaults();
        RecomputeCompareColumnCandidates();
        InitializeCompareColumnDefault();
        RecomputeOtherColumns();

        return Task.CompletedTask;
    }

    private void InitializeKeySelectionDefaults()
    {
        var headers = _converterFile!.Headers;
        _selectedKeyColumns = DefaultKeyColumnCandidates.Where(headers.Contains).ToList();
        _availableKeyColumns = headers.Where(h => !_selectedKeyColumns.Contains(h)).ToList();
    }

    private void InitializeCompareColumnDefault()
    {
        if (_numericCandidateColumns.Contains(DefaultCompareColumnCandidate))
        {
            _compareColumnName = DefaultCompareColumnCandidate;
        }
    }

    private void RecomputeCompareColumnCandidates()
    {
        var headers = _converterFile!.Headers;
        var nonKeyColumns = headers.Where(h => !_selectedKeyColumns.Contains(h)).ToList();

        _numericCandidateColumns = nonKeyColumns
            .Where(c => ComparisonService.IsColumnNumeric(_converterFile!, c) && ComparisonService.IsColumnNumeric(_clientFile!, c))
            .ToList();

        _nonNumericExcludedColumns = nonKeyColumns.Except(_numericCandidateColumns).ToList();

        if (_compareColumnName is not null && !_numericCandidateColumns.Contains(_compareColumnName))
        {
            _compareColumnName = null;
        }
    }

    private void RecomputeOtherColumns()
    {
        _otherColumns = _converterFile!.Headers
            .Where(h => !_selectedKeyColumns.Contains(h) && h != _compareColumnName)
            .ToList();
    }

    private void OnSelectedKeysChanged(List<string> keys)
    {
        _selectedKeyColumns = keys;
        RecomputeCompareColumnCandidates();
        RecomputeOtherColumns();
    }

    private void OnCompareColumnChanged(string? column)
    {
        _compareColumnName = column;
        RecomputeOtherColumns();
    }

    private void OnGroupingConfigChanged(DifferenceGroupingConfig config)
    {
        _groupingConfig = config;
        if (_rawComparisonResult is not null)
        {
            _comparisonResult = GroupingService.ApplyGrouping(_rawComparisonResult, _groupingConfig);
        }
    }

    private async Task RunCompareAsync()
    {
        if (!CanCompare || _converterFile is null || _clientFile is null || _compareColumnName is null)
        {
            return;
        }

        _isComparing = true;
        StateHasChanged();

        try
        {
            var keySelection = new KeySelection { OrderedKeyColumns = _selectedKeyColumns };
            var compareSelection = new CompareColumnSelection { ColumnName = _compareColumnName };

            _rawComparisonResult = await Task.Run(() =>
                ComparisonService.Compare(_converterFile, _clientFile, keySelection, compareSelection));
            _comparisonResult = GroupingService.ApplyGrouping(_rawComparisonResult, _groupingConfig);
            _comparisonWarnings = _rawComparisonResult.Warnings;
        }
        finally
        {
            _isComparing = false;
        }
    }

    private async Task DownloadReportAsync()
    {
        if (_comparisonResult is null || _compareColumnName is null)
        {
            return;
        }

        var bytes = ExcelExportService.ExportToExcel(_comparisonResult, _selectedKeyColumns, _otherColumns, _compareColumnName, _groupingConfig);
        var base64 = Convert.ToBase64String(bytes);
        const string xlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        await JsRuntime.InvokeVoidAsync("fileCompareDownload.downloadFile", "comparison-report.xlsx", xlsxContentType, base64);
    }
}
