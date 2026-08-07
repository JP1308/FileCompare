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

    private byte[]? _convertorRawBytes;
    private byte[]? _clientRawBytes;
    private ParsedFile? _convertorFile;
    private ParsedFile? _clientFile;
    private string? _convertorFileName;
    private string? _clientFileName;
    private string? _convertorError;
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

    private void OnFileError(bool isConvertor, string message)
    {
        if (isConvertor)
        {
            _convertorError = message;
        }
        else
        {
            _clientError = message;
        }
        StateHasChanged();
    }

    private async Task OnFileLoadedAsync(bool isConvertor, string fileName, byte[] content)
    {
        if (isConvertor)
        {
            _convertorRawBytes = content;
            _convertorFileName = fileName;
        }
        else
        {
            _clientRawBytes = content;
            _clientFileName = fileName;
        }

        await ParseAndStoreAsync(isConvertor, content, fileName);
        RebuildRowParseErrors();
        await ValidateAndResetAsync();
    }

    private async Task OnFileTypeSelectionChangedAsync(FileTypeSelection selection)
    {
        _fileTypeSelection = selection;

        if (_convertorRawBytes is not null)
        {
            await ParseAndStoreAsync(true, _convertorRawBytes, _convertorFileName ?? "Convertor output file");
        }
        if (_clientRawBytes is not null)
        {
            await ParseAndStoreAsync(false, _clientRawBytes, _clientFileName ?? "Client expected file");
        }

        RebuildRowParseErrors();
        await ValidateAndResetAsync();
    }

    private async Task ParseAndStoreAsync(bool isConvertor, byte[] bytes, string fileName)
    {
        try
        {
            var parsed = await Task.Run(() => ParseBytes(bytes));

            if (isConvertor)
            {
                _convertorFile = parsed;
                _convertorError = null;
            }
            else
            {
                _clientFile = parsed;
                _clientError = null;
            }
        }
        catch (Exception ex)
        {
            if (isConvertor)
            {
                _convertorFile = null;
            }
            else
            {
                _clientFile = null;
            }
            OnFileError(isConvertor, $"Failed to parse '{fileName}': {ex.Message}");
        }
    }

    private ParsedFile ParseBytes(byte[] bytes) => _fileTypeSelection.Format switch
    {
        InputFileFormat.Excel => ParserService.ParseExcel(bytes),
        _ => ParserService.ParseDelimitedText(bytes, _fileTypeSelection.Delimiter),
    };

    private void RebuildRowParseErrors()
    {
        _rowParseErrors.Clear();
        if (_convertorFile is not null)
        {
            _rowParseErrors.AddRange(_convertorFile.RowErrors.Select(e => $"Convertor output file — {e}"));
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

        if (_convertorFile is null || _clientFile is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            ParserService.ValidateHeaders(_convertorFile, _clientFile);
        }
        catch (HeaderMismatchException ex)
        {
            if (ex.OnlyInConvertorFile.Count > 0)
            {
                _headerMismatchErrors.Add($"Only in Convertor output file: {string.Join(", ", ex.OnlyInConvertorFile)}");
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
        var headers = _convertorFile!.Headers;
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
        var headers = _convertorFile!.Headers;
        var nonKeyColumns = headers.Where(h => !_selectedKeyColumns.Contains(h)).ToList();

        _numericCandidateColumns = nonKeyColumns
            .Where(c => ComparisonService.IsColumnNumeric(_convertorFile!, c) && ComparisonService.IsColumnNumeric(_clientFile!, c))
            .ToList();

        _nonNumericExcludedColumns = nonKeyColumns.Except(_numericCandidateColumns).ToList();

        if (_compareColumnName is not null && !_numericCandidateColumns.Contains(_compareColumnName))
        {
            _compareColumnName = null;
        }
    }

    private void RecomputeOtherColumns()
    {
        _otherColumns = _convertorFile!.Headers
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
        if (!CanCompare || _convertorFile is null || _clientFile is null || _compareColumnName is null)
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
                ComparisonService.Compare(_convertorFile, _clientFile, keySelection, compareSelection));
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
