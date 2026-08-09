using FileCompare.Models;

namespace M_S_Converter_Output_Compare.Controls;

/// <summary>Editable list of difference-magnitude bucket boundaries, mirroring the web app's GroupingSettings panel.</summary>
public class GroupingSettingsControl : UserControl
{
    private readonly DataGridView _grid;

    public event EventHandler? ConfigChanged;

    public GroupingSettingsControl()
    {
        Dock = DockStyle.Top;
        Height = 160;

        var label = new Label
        {
            Text = "Difference-magnitude grouping boundaries (absolute-value upper bound per group; a final \"greater than last boundary\" group is added automatically)",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(600, 0),
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            Height = 100,
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Upper bound",
            Name = "UpperBound",
        });
        _grid.Rows.Add("0.5");
        _grid.CellEndEdit += (_, _) => ConfigChanged?.Invoke(this, EventArgs.Empty);
        _grid.RowsRemoved += (_, _) => ConfigChanged?.Invoke(this, EventArgs.Empty);
        _grid.UserDeletedRow += (_, _) => ConfigChanged?.Invoke(this, EventArgs.Empty);

        var gridPanel = new Panel { Dock = DockStyle.Fill };
        gridPanel.Controls.Add(_grid);

        Controls.Add(gridPanel);
        Controls.Add(label);
    }

    /// <summary>Parses the grid rows into a config; non-numeric or blank rows are ignored. Returns the default (single boundary 0.5) if nothing valid was entered.</summary>
    public DifferenceGroupingConfig GetConfig()
    {
        var boundaries = new List<GroupBoundary>();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var text = row.Cells["UpperBound"].Value?.ToString();
            if (decimal.TryParse(text, out var value))
            {
                boundaries.Add(new GroupBoundary { UpperBound = value });
            }
        }

        return boundaries.Count > 0
            ? new DifferenceGroupingConfig { Groups = boundaries }
            : DifferenceGroupingConfig.Default();
    }
}
