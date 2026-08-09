using FileCompare.Models;
using FileCompare.Services;
using M_S_Converter_Output_Compare.Controls;

namespace M_S_Converter_Output_Compare;

public class MainForm : Form
{
    private static readonly string[] DefaultKeyColumnCandidates = { "PersonalNr", "Lohnart", "TextLohnart" };
    private const string DefaultCompareColumnCandidate = "Betrag";

    private readonly FileParserService _parserService = new();
    private readonly ComparisonService _comparisonService = new();
    private readonly GroupingService _groupingService = new();
    private readonly ExcelExportService _excelExportService = new();

    // --- UI controls ---
    private readonly RadioButton _csvRadio;
    private readonly RadioButton _delimitedRadio;
    private readonly ComboBox _delimiterCombo;
    private readonly FileUploadPanel _converterUpload;
    private readonly FileUploadPanel _clientUpload;
    private readonly Label _headerMismatchLabel;
    private readonly Label _rowParseErrorsLabel;
    private readonly DualListKeySelector _keySelector;
    private readonly ComboBox _compareColumnCombo;
    private readonly GroupingSettingsControl _groupingControl;
    private readonly Button _compareButton;
    private readonly Label _summaryLabel;
    private readonly Label _warningsLabel;
    private readonly TreeView _resultsTree;
    private readonly DataGridView _resultsGrid;
    private readonly Button _downloadButton;

    // --- State (ported from the Blazor app's Home.razor.cs) ---
    private FileTypeSelection _fileTypeSelection = FileTypeSelection.Default();

    private byte[]? _converterRawBytes;
    private byte[]? _clientRawBytes;
    private ParsedFile? _converterFile;
    private ParsedFile? _clientFile;
    private string? _converterFileName;
    private string? _clientFileName;

    private bool _headersReady;
    private List<string> _numericCandidateColumns = new();
    private List<string> _otherColumns = new();
    private string? _compareColumnName;

    private DifferenceGroupingConfig _groupingConfig = DifferenceGroupingConfig.Default();

    private ComparisonResult? _rawComparisonResult;
    private ComparisonResult? _comparisonResult;

    public MainForm()
    {
        Text = "M&S Converter Output Compare";
        MinimumSize = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;

        // ---- Top: configuration (scrollable) ----
        var configPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

        var fileTypeGroup = new GroupBox { Text = "1. File type", Dock = DockStyle.Top, Height = 80 };
        _csvRadio = new RadioButton { Text = "CSV (.csv)", Left = 12, Top = 24, AutoSize = true };
        _delimitedRadio = new RadioButton { Text = "Delimited text", Left = 12, Top = 48, AutoSize = true, Checked = true };
        _delimiterCombo = new ComboBox { Left = 150, Top = 46, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
        _delimiterCombo.Items.AddRange(new object[] { "Semicolon ( ; )", "Tab", "Pipe ( | )" });
        _delimiterCombo.SelectedIndex = 0;
        _csvRadio.CheckedChanged += (_, _) => OnFileTypeChanged();
        _delimitedRadio.CheckedChanged += (_, _) => OnFileTypeChanged();
        _delimiterCombo.SelectedIndexChanged += (_, _) => OnFileTypeChanged();
        fileTypeGroup.Controls.Add(_csvRadio);
        fileTypeGroup.Controls.Add(_delimitedRadio);
        fileTypeGroup.Controls.Add(_delimiterCombo);

        var uploadGroup = new GroupBox { Text = "2. Upload files", Dock = DockStyle.Top, Height = 210, Padding = new Padding(12) };
        _converterUpload = new FileUploadPanel("Converter output file");
        _clientUpload = new FileUploadPanel("Client expected file");
        _converterUpload.FileSelected += (_, f) => OnFileSelected(isConverter: true, f.FileName, f.Content);
        _clientUpload.FileSelected += (_, f) => OnFileSelected(isConverter: false, f.FileName, f.Content);
        var uploadFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
        uploadFlow.Controls.Add(_converterUpload);
        uploadFlow.Controls.Add(_clientUpload);
        uploadGroup.Controls.Add(uploadFlow);

        _headerMismatchLabel = new Label
        {
            ForeColor = Color.Firebrick,
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Visible = false,
            Padding = new Padding(0, 4, 0, 4),
        };

        _rowParseErrorsLabel = new Label
        {
            ForeColor = Color.DarkOrange,
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Visible = false,
            Padding = new Padding(0, 4, 0, 4),
        };

        var keyGroup = new GroupBox { Text = "3. Key column(s) selection (ordered)", Dock = DockStyle.Top, Height = 260, Padding = new Padding(12) };
        _keySelector = new DualListKeySelector { Dock = DockStyle.Fill };
        _keySelector.SelectionChanged += (_, _) => OnKeySelectionChanged();
        keyGroup.Controls.Add(_keySelector);

        var compareGroup = new GroupBox { Text = "4. Compare column", Dock = DockStyle.Top, Height = 70, Padding = new Padding(12) };
        _compareColumnCombo = new ComboBox { Left = 12, Top = 28, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList };
        _compareColumnCombo.SelectedIndexChanged += (_, _) => OnCompareColumnChanged();
        compareGroup.Controls.Add(_compareColumnCombo);

        var groupingGroup = new GroupBox { Text = "5. Difference-magnitude grouping", Dock = DockStyle.Top, Height = 190, Padding = new Padding(12) };
        _groupingControl = new GroupingSettingsControl { Dock = DockStyle.Fill };
        _groupingControl.ConfigChanged += (_, _) => OnGroupingConfigChanged();
        groupingGroup.Controls.Add(_groupingControl);

        _compareButton = new Button { Text = "Compare", Dock = DockStyle.Top, Height = 36, Enabled = false };
        _compareButton.Click += async (_, _) => await RunCompareAsync();

        // Docked panels stack in reverse of add order (last added is topmost), so add bottom-to-top.
        configPanel.Controls.Add(_compareButton);
        configPanel.Controls.Add(groupingGroup);
        configPanel.Controls.Add(compareGroup);
        configPanel.Controls.Add(keyGroup);
        configPanel.Controls.Add(_rowParseErrorsLabel);
        configPanel.Controls.Add(_headerMismatchLabel);
        configPanel.Controls.Add(uploadGroup);
        configPanel.Controls.Add(fileTypeGroup);

        // ---- Bottom: results ----
        var resultsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var summaryFlow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight };
        _summaryLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        summaryFlow.Controls.Add(_summaryLabel);

        _warningsLabel = new Label
        {
            ForeColor = Color.DarkOrange,
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(900, 0),
            Visible = false,
        };

        _downloadButton = new Button { Text = "Download report…", Dock = DockStyle.Top, Height = 32, Enabled = false };
        _downloadButton.Click += (_, _) => DownloadReport();

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 350 };
        _resultsTree = new TreeView { Dock = DockStyle.Fill };
        _resultsTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is LeafData data)
            {
                PopulateGrid(data.Rows, data.ShowAbsoluteDifference);
            }
        };
        _resultsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
        };
        split.Panel1.Controls.Add(_resultsTree);
        split.Panel2.Controls.Add(_resultsGrid);

        resultsPanel.Controls.Add(split);
        resultsPanel.Controls.Add(_downloadButton);
        resultsPanel.Controls.Add(_warningsLabel);
        resultsPanel.Controls.Add(summaryFlow);

        var mainSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 560 };
        mainSplit.Panel1.Controls.Add(configPanel);
        mainSplit.Panel2.Controls.Add(resultsPanel);

        Controls.Add(mainSplit);

        UpdateAcceptExtensions();

        // Without an explicit initial focus, the runtime auto-focuses a lower control (e.g. inside the
        // grouping grid) and the scrollable config panel auto-scrolls down to keep it visible.
        ActiveControl = _delimitedRadio;
        Shown += (_, _) => configPanel.AutoScrollPosition = new Point(0, 0);
    }

    private FileTypeSelection CurrentFileTypeSelection()
    {
        var format = _csvRadio.Checked ? InputFileFormat.Csv : InputFileFormat.DelimitedText;
        var delimiter = _delimiterCombo.SelectedIndex switch
        {
            1 => '\t',
            2 => '|',
            _ => ';',
        };
        return new FileTypeSelection { Format = format, Delimiter = delimiter };
    }

    private void UpdateAcceptExtensions()
    {
        var extensions = _fileTypeSelection.Format == InputFileFormat.Csv ? ".csv" : ".csv,.txt";
        _converterUpload.AcceptExtensions = extensions;
        _clientUpload.AcceptExtensions = extensions;
        _delimiterCombo.Visible = _fileTypeSelection.Format == InputFileFormat.DelimitedText;
    }

    private async void OnFileTypeChanged()
    {
        _fileTypeSelection = CurrentFileTypeSelection();
        UpdateAcceptExtensions();

        if (_converterRawBytes is not null)
        {
            ParseAndStore(isConverter: true, _converterRawBytes, _converterFileName ?? "Converter output file");
        }
        if (_clientRawBytes is not null)
        {
            ParseAndStore(isConverter: false, _clientRawBytes, _clientFileName ?? "Client expected file");
        }

        RebuildRowParseErrorsLabel();
        await ValidateAndResetAsync();
    }

    private async void OnFileSelected(bool isConverter, string fileName, byte[] content)
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

        ParseAndStore(isConverter, content, fileName);
        RebuildRowParseErrorsLabel();
        await ValidateAndResetAsync();
    }

    private void ParseAndStore(bool isConverter, byte[] bytes, string fileName)
    {
        var panel = isConverter ? _converterUpload : _clientUpload;
        try
        {
            var parsed = ParseBytes(bytes);
            if (isConverter)
            {
                _converterFile = parsed;
            }
            else
            {
                _clientFile = parsed;
            }
            panel.ClearError();
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
            panel.ShowError($"Failed to parse '{fileName}': {ex.Message}");
        }
    }

    private ParsedFile ParseBytes(byte[] bytes) => _fileTypeSelection.Format switch
    {
        InputFileFormat.Csv => _parserService.ParseDelimitedText(bytes, ','),
        _ => _parserService.ParseDelimitedText(bytes, _fileTypeSelection.Delimiter),
    };

    private void RebuildRowParseErrorsLabel()
    {
        var errors = new List<string>();
        if (_converterFile is not null)
        {
            errors.AddRange(_converterFile.RowErrors.Select(e => $"Converter output file — {e}"));
        }
        if (_clientFile is not null)
        {
            errors.AddRange(_clientFile.RowErrors.Select(e => $"Client expected file — {e}"));
        }

        _rowParseErrorsLabel.Visible = errors.Count > 0;
        _rowParseErrorsLabel.Text = string.Join(Environment.NewLine, errors);
    }

    private Task ValidateAndResetAsync()
    {
        _headerMismatchLabel.Visible = false;
        _headersReady = false;
        _comparisonResult = null;
        _rawComparisonResult = null;
        _resultsTree.Nodes.Clear();
        _resultsGrid.Columns.Clear();
        _resultsGrid.Rows.Clear();
        _summaryLabel.Text = string.Empty;
        _warningsLabel.Visible = false;
        _downloadButton.Enabled = false;

        if (_converterFile is null || _clientFile is null)
        {
            UpdateCompareButtonState();
            return Task.CompletedTask;
        }

        try
        {
            _parserService.ValidateHeaders(_converterFile, _clientFile);
        }
        catch (HeaderMismatchException ex)
        {
            var parts = new List<string>();
            if (ex.OnlyInConverterFile.Count > 0)
            {
                parts.Add($"Only in Converter output file: {string.Join(", ", ex.OnlyInConverterFile)}");
            }
            if (ex.OnlyInClientFile.Count > 0)
            {
                parts.Add($"Only in Client expected file: {string.Join(", ", ex.OnlyInClientFile)}");
            }
            _headerMismatchLabel.Text = string.Join(Environment.NewLine, parts);
            _headerMismatchLabel.Visible = true;
            UpdateCompareButtonState();
            return Task.CompletedTask;
        }

        _headersReady = true;
        _keySelector.SetAvailableColumns(_converterFile.Headers, DefaultKeyColumnCandidates);
        RecomputeCompareColumnCandidates();
        InitializeCompareColumnDefault();
        RecomputeOtherColumns();
        UpdateCompareButtonState();

        return Task.CompletedTask;
    }

    private void OnKeySelectionChanged()
    {
        RecomputeCompareColumnCandidates();
        RecomputeOtherColumns();
        UpdateCompareButtonState();
    }

    private void OnCompareColumnChanged()
    {
        _compareColumnName = _compareColumnCombo.SelectedItem as string;
        RecomputeOtherColumns();
        UpdateCompareButtonState();
    }

    private void RecomputeCompareColumnCandidates()
    {
        if (_converterFile is null || _clientFile is null)
        {
            return;
        }

        var selectedKeys = _keySelector.SelectedKeyColumns;
        var nonKeyColumns = _converterFile.Headers.Where(h => !selectedKeys.Contains(h)).ToList();

        _numericCandidateColumns = nonKeyColumns
            .Where(c => _comparisonService.IsColumnNumeric(_converterFile, c) && _comparisonService.IsColumnNumeric(_clientFile, c))
            .ToList();

        var previousSelection = _compareColumnCombo.SelectedItem as string;
        _compareColumnCombo.SelectedIndexChanged -= CompareColumnCombo_SelectedIndexChanged;
        _compareColumnCombo.Items.Clear();
        _compareColumnCombo.Items.AddRange(_numericCandidateColumns.Cast<object>().ToArray());
        if (previousSelection is not null && _numericCandidateColumns.Contains(previousSelection))
        {
            _compareColumnCombo.SelectedItem = previousSelection;
        }
        else
        {
            _compareColumnName = null;
        }
        _compareColumnCombo.SelectedIndexChanged += CompareColumnCombo_SelectedIndexChanged;
    }

    private void CompareColumnCombo_SelectedIndexChanged(object? sender, EventArgs e) => OnCompareColumnChanged();

    private void InitializeCompareColumnDefault()
    {
        if (_numericCandidateColumns.Contains(DefaultCompareColumnCandidate))
        {
            _compareColumnCombo.SelectedIndexChanged -= CompareColumnCombo_SelectedIndexChanged;
            _compareColumnCombo.SelectedItem = DefaultCompareColumnCandidate;
            _compareColumnCombo.SelectedIndexChanged += CompareColumnCombo_SelectedIndexChanged;
            _compareColumnName = DefaultCompareColumnCandidate;
        }
    }

    private void RecomputeOtherColumns()
    {
        if (_converterFile is null)
        {
            _otherColumns = new List<string>();
            return;
        }

        var selectedKeys = _keySelector.SelectedKeyColumns;
        _otherColumns = _converterFile.Headers
            .Where(h => !selectedKeys.Contains(h) && h != _compareColumnName)
            .ToList();
    }

    private void UpdateCompareButtonState()
    {
        _compareButton.Enabled = _headersReady && _keySelector.SelectedKeyColumns.Count > 0 && !string.IsNullOrEmpty(_compareColumnName);
    }

    private async Task RunCompareAsync()
    {
        if (_converterFile is null || _clientFile is null || _compareColumnName is null)
        {
            return;
        }

        _compareButton.Enabled = false;
        _compareButton.Text = "Comparing…";
        Cursor = Cursors.WaitCursor;

        try
        {
            var keySelection = new KeySelection { OrderedKeyColumns = _keySelector.SelectedKeyColumns };
            var compareSelection = new CompareColumnSelection { ColumnName = _compareColumnName };
            var converterFile = _converterFile;
            var clientFile = _clientFile;

            _rawComparisonResult = await Task.Run(() =>
                _comparisonService.Compare(converterFile, clientFile, keySelection, compareSelection));
            _comparisonResult = _groupingService.ApplyGrouping(_rawComparisonResult, _groupingConfig);

            BuildResultsTree();
            UpdateSummary();
            _downloadButton.Enabled = true;
        }
        finally
        {
            _compareButton.Text = "Compare";
            Cursor = Cursors.Default;
            UpdateCompareButtonState();
        }
    }

    private void OnGroupingConfigChanged()
    {
        _groupingConfig = _groupingControl.GetConfig();
        if (_rawComparisonResult is not null)
        {
            _comparisonResult = _groupingService.ApplyGrouping(_rawComparisonResult, _groupingConfig);
            BuildResultsTree();
            UpdateSummary();
        }
    }

    private void UpdateSummary()
    {
        if (_comparisonResult is null)
        {
            _summaryLabel.Text = string.Empty;
            _warningsLabel.Visible = false;
            return;
        }

        _summaryLabel.Text =
            $"Matching: {_comparisonResult.MatchingCount}    " +
            $"Added: {_comparisonResult.AddedCount}    " +
            $"Deleted: {_comparisonResult.DeletedCount}    " +
            $"Different: {_comparisonResult.DifferentCount}";

        if (_comparisonResult.Warnings.Count > 0)
        {
            _warningsLabel.Text = string.Join(Environment.NewLine, _comparisonResult.Warnings);
            _warningsLabel.Visible = true;
        }
        else
        {
            _warningsLabel.Visible = false;
        }
    }

    private void BuildResultsTree()
    {
        _resultsTree.Nodes.Clear();
        _resultsGrid.Columns.Clear();
        _resultsGrid.Rows.Clear();

        if (_comparisonResult is null)
        {
            return;
        }

        var keyColumns = _keySelector.SelectedKeyColumns;

        _resultsTree.Nodes.Add(BuildCategoryNode("Matching entries", RowStatus.Matching, keyColumns));
        _resultsTree.Nodes.Add(BuildCategoryNode("Added entries", RowStatus.Added, keyColumns));
        _resultsTree.Nodes.Add(BuildCategoryNode("Deleted entries", RowStatus.Deleted, keyColumns));
        _resultsTree.Nodes.Add(BuildCategoryNode("Different entries", RowStatus.Different, keyColumns));

        _resultsTree.ExpandAll();
        _resultsTree.Nodes[0].EnsureVisible();
    }

    private TreeNode BuildCategoryNode(string label, RowStatus status, List<string> keyColumns)
    {
        var rows = _comparisonResult!.Rows.Where(r => r.Status == status).ToList();
        var isDifferent = status == RowStatus.Different;

        var selectors = new List<Func<RowResult, string>>();
        if (isDifferent)
        {
            selectors.Add(r => r.DifferenceGroup ?? "(ungrouped)");
        }
        for (var i = 0; i < keyColumns.Count; i++)
        {
            var index = i;
            selectors.Add(r => r.KeyValues[index]);
        }

        var node = new TreeNode($"{label} ({rows.Count})") { Tag = new LeafData(rows, isDifferent) };
        AddLevelNodes(node, rows, selectors, 0, isDifferent);
        return node;
    }

    private static void AddLevelNodes(TreeNode parent, List<RowResult> rows, List<Func<RowResult, string>> selectors, int level, bool isDifferent)
    {
        if (level >= selectors.Count)
        {
            return;
        }

        foreach (var group in rows.GroupBy(selectors[level]))
        {
            var groupRows = group.ToList();
            var child = new TreeNode($"{group.Key} ({groupRows.Count})") { Tag = new LeafData(groupRows, isDifferent) };
            parent.Nodes.Add(child);
            AddLevelNodes(child, groupRows, selectors, level + 1, isDifferent);
        }
    }

    private void PopulateGrid(List<RowResult> rows, bool showAbsoluteDifference)
    {
        _resultsGrid.Columns.Clear();
        _resultsGrid.Rows.Clear();

        var keyColumns = _keySelector.SelectedKeyColumns;
        foreach (var key in keyColumns)
        {
            _resultsGrid.Columns.Add(key, key);
        }
        _resultsGrid.Columns.Add("Converter", $"{_compareColumnName} (Converter)");
        _resultsGrid.Columns.Add("Client", $"{_compareColumnName} (Client)");
        if (showAbsoluteDifference)
        {
            _resultsGrid.Columns.Add("AbsoluteDifference", "AbsoluteDifference");
        }
        _resultsGrid.Columns.Add("Other", OtherColumnsFormatter.Header(_otherColumns));

        foreach (var row in rows)
        {
            var values = new List<object>(row.KeyValues)
            {
                row.ConverterCompareValue?.ToString() ?? "—",
                row.ClientCompareValue?.ToString() ?? "—",
            };
            if (showAbsoluteDifference)
            {
                values.Add(row.AbsoluteDifference?.ToString() ?? "—");
            }
            values.Add(OtherColumnsFormatter.Combine(row, _otherColumns));

            _resultsGrid.Rows.Add(values.ToArray());
        }
    }

    private void DownloadReport()
    {
        if (_comparisonResult is null || _compareColumnName is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            FileName = "comparison-report.xlsx",
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var bytes = _excelExportService.ExportToExcel(
            _comparisonResult, _keySelector.SelectedKeyColumns, _otherColumns, _compareColumnName, _groupingConfig);
        File.WriteAllBytes(dialog.FileName, bytes);
    }

    private sealed record LeafData(List<RowResult> Rows, bool ShowAbsoluteDifference);
}
