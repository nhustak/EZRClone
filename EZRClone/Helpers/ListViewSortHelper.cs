using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EZRClone.Helpers;

public class ListViewSortHelper
{
    private GridViewColumnHeader? _lastHeaderClicked;
    private ListSortDirection _lastDirection = ListSortDirection.Ascending;

    private static readonly Dictionary<string, string> HeaderToProperty = new()
    {
        ["Name"] = "Name",
        ["Path"] = "Path",
        ["Size"] = "Size",
        ["Files (all)"] = "FileCount",
        ["Modified"] = "ModTime"
    };

    public void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader headerClicked) return;
        if (headerClicked.Role == GridViewColumnHeaderRole.Padding) return;

        var header = headerClicked.Column?.Header?.ToString();
        if (string.IsNullOrEmpty(header) || !HeaderToProperty.ContainsKey(header)) return;

        var sortProperty = HeaderToProperty[header];
        ListSortDirection direction;

        if (headerClicked != _lastHeaderClicked)
        {
            direction = ListSortDirection.Ascending;
        }
        else
        {
            direction = _lastDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        if (sender is not ListView listView) return;
        var view = CollectionViewSource.GetDefaultView(listView.ItemsSource);
        if (view == null) return;

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(sortProperty, direction));

        // Update header text with arrow indicator
        if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
        {
            var oldHeader = _lastHeaderClicked.Column?.Header?.ToString()?.TrimEnd(' ', '▲', '▼');
            if (_lastHeaderClicked.Column != null && oldHeader != null)
                _lastHeaderClicked.Column.Header = oldHeader;
        }

        var arrow = direction == ListSortDirection.Ascending ? " ▲" : " ▼";
        if (headerClicked.Column != null)
            headerClicked.Column.Header = header + arrow;

        _lastHeaderClicked = headerClicked;
        _lastDirection = direction;
    }
}
