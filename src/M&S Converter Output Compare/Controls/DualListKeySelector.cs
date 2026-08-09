namespace M_S_Converter_Output_Compare.Controls;

/// <summary>Two-list key column picker with move/reorder controls, mirroring the web app's DualListKeySelector.</summary>
public class DualListKeySelector : UserControl
{
    private readonly ListBox _availableList;
    private readonly ListBox _selectedList;

    public event EventHandler? SelectionChanged;

    public List<string> SelectedKeyColumns => _selectedList.Items.Cast<string>().ToList();

    public DualListKeySelector()
    {
        Dock = DockStyle.Top;
        Height = 220;

        var availableLabel = new Label { Text = "Available columns", Dock = DockStyle.Top, AutoSize = true };
        _availableList = new ListBox { Dock = DockStyle.Fill };

        var availablePanel = new Panel { Dock = DockStyle.Left, Width = 220 };
        availablePanel.Controls.Add(_availableList);
        availablePanel.Controls.Add(availableLabel);

        var selectedLabel = new Label { Text = "Selected key columns (in order)", Dock = DockStyle.Top, AutoSize = true };
        _selectedList = new ListBox { Dock = DockStyle.Fill };

        var selectedPanel = new Panel { Dock = DockStyle.Fill };
        selectedPanel.Controls.Add(_selectedList);
        selectedPanel.Controls.Add(selectedLabel);

        _availableList.DoubleClick += (_, _) => MoveSelectedItems(_availableList, _selectedList, appendToEnd: true);
        _selectedList.DoubleClick += (_, _) => MoveSelectedItems(_selectedList, _availableList, appendToEnd: false);

        var addButton = new Button { Text = "Add >", Width = 90, Margin = new Padding(4) };
        addButton.Click += (_, _) => MoveSelectedItems(_availableList, _selectedList, appendToEnd: true);

        var removeButton = new Button { Text = "< Remove", Width = 90, Margin = new Padding(4) };
        removeButton.Click += (_, _) => MoveSelectedItems(_selectedList, _availableList, appendToEnd: false);

        var upButton = new Button { Text = "Move Up", Width = 90, Margin = new Padding(4) };
        upButton.Click += (_, _) => MoveWithinSelected(-1);

        var downButton = new Button { Text = "Move Down", Width = 90, Margin = new Padding(4) };
        downButton.Click += (_, _) => MoveWithinSelected(1);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            Width = 100,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4, 40, 4, 0),
        };
        buttonsPanel.Controls.Add(addButton);
        buttonsPanel.Controls.Add(removeButton);
        buttonsPanel.Controls.Add(upButton);
        buttonsPanel.Controls.Add(downButton);

        Controls.Add(selectedPanel);
        Controls.Add(buttonsPanel);
        Controls.Add(availablePanel);
    }

    private void MoveSelectedItems(ListBox from, ListBox to, bool appendToEnd)
    {
        var items = from.SelectedItems.Cast<string>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        foreach (var item in items)
        {
            from.Items.Remove(item);
            if (appendToEnd)
            {
                to.Items.Add(item);
            }
            else
            {
                to.Items.Insert(0, item);
            }
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MoveWithinSelected(int direction)
    {
        if (_selectedList.SelectedIndex < 0)
        {
            return;
        }

        var index = _selectedList.SelectedIndex;
        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= _selectedList.Items.Count)
        {
            return;
        }

        var item = _selectedList.Items[index];
        _selectedList.Items.RemoveAt(index);
        _selectedList.Items.Insert(newIndex, item);
        _selectedList.SelectedIndex = newIndex;

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Repopulates both lists from a fresh header set, pre-selecting any of <paramref name="defaultKeyColumns"/> found (in that order).</summary>
    public void SetAvailableColumns(List<string> headers, IReadOnlyList<string> defaultKeyColumns)
    {
        _availableList.Items.Clear();
        _selectedList.Items.Clear();

        var selected = defaultKeyColumns.Where(headers.Contains).ToList();
        foreach (var column in selected)
        {
            _selectedList.Items.Add(column);
        }
        foreach (var column in headers.Where(h => !selected.Contains(h)))
        {
            _availableList.Items.Add(column);
        }

        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
