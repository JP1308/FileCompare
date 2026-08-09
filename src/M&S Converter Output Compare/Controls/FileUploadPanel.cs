namespace M_S_Converter_Output_Compare.Controls;

/// <summary>A labeled drop zone that accepts a file via drag-and-drop or a Browse button, mirroring the web app's FileDropUpload.</summary>
public class FileUploadPanel : UserControl
{
    private readonly Label _titleLabel;
    private readonly Panel _dropZone;
    private readonly Label _dropZoneLabel;
    private readonly Button _browseButton;
    private readonly Label _errorLabel;

    private static readonly Color DropZoneBorder = Color.FromArgb(180, 180, 180);
    private static readonly Color DropZoneHoverBorder = Color.FromArgb(0, 120, 215);
    private static readonly Color DropZoneHoverFill = Color.FromArgb(235, 244, 255);

    public string Title { get; }
    public string? SelectedFileName { get; private set; }
    public string AcceptExtensions { get; set; } = ".csv,.txt";

    public event EventHandler<(string FileName, byte[] Content)>? FileSelected;

    public FileUploadPanel(string title)
    {
        Title = title;
        Size = new Size(380, 170);
        Margin = new Padding(0, 0, 24, 12);

        _titleLabel = new Label
        {
            Text = title,
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Top,
        };

        _dropZone = new Panel
        {
            Height = 90,
            Dock = DockStyle.Top,
            BorderStyle = BorderStyle.None,
            AllowDrop = true,
            BackColor = Color.FromArgb(250, 250, 250),
            Margin = new Padding(0, 4, 0, 4),
        };
        _dropZone.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, _dropZone.ClientRectangle,
            _dropZoneCurrentBorder, ButtonBorderStyle.Dashed);
        _dropZone.DragEnter += DropZone_DragEnter;
        _dropZone.DragLeave += (_, _) => SetHover(false);
        _dropZone.DragDrop += DropZone_DragDrop;
        _dropZone.Click += (_, _) => BrowseForFile();

        _dropZoneLabel = new Label
        {
            Text = "Drag && drop a file here, or click to browse",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
        };
        _dropZoneLabel.Click += (_, _) => BrowseForFile();
        _dropZone.Controls.Add(_dropZoneLabel);

        _browseButton = new Button
        {
            Text = "Browse…",
            AutoSize = true,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 4),
        };
        _browseButton.Click += (_, _) => BrowseForFile();

        _errorLabel = new Label
        {
            ForeColor = Color.Firebrick,
            AutoSize = true,
            Dock = DockStyle.Top,
            Visible = false,
        };

        // Dock order: last-added docks closest to the edge, so add bottom-up for Top docking.
        Controls.Add(_errorLabel);
        Controls.Add(_dropZone);
        Controls.Add(_browseButton);
        Controls.Add(_titleLabel);
    }

    private Color _dropZoneCurrentBorder = DropZoneBorder;

    private void SetHover(bool hovering)
    {
        _dropZoneCurrentBorder = hovering ? DropZoneHoverBorder : DropZoneBorder;
        _dropZone.BackColor = hovering ? DropZoneHoverFill : Color.FromArgb(250, 250, 250);
        _dropZone.Invalidate();
    }

    private void DropZone_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            SetHover(true);
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void DropZone_DragDrop(object? sender, DragEventArgs e)
    {
        SetHover(false);
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            LoadFile(files[0]);
        }
    }

    private void BrowseForFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = $"Select {Title}",
            Filter = BuildFilter(),
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadFile(dialog.FileName);
        }
    }

    private string BuildFilter()
    {
        var extensions = AcceptExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var patterns = string.Join(";", extensions.Select(e => "*" + e));
        return $"Accepted files ({patterns})|{patterns}|All files (*.*)|*.*";
    }

    private void LoadFile(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var fileName = Path.GetFileName(path);
            SelectedFileName = fileName;
            _dropZoneLabel.Text = $"{fileName}\n(click to replace)";
            ClearError();
            FileSelected?.Invoke(this, (fileName, bytes));
        }
        catch (Exception ex)
        {
            ShowError($"Could not read file: {ex.Message}");
        }
    }

    public void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }

    public void ClearError()
    {
        _errorLabel.Visible = false;
        _errorLabel.Text = string.Empty;
    }
}
