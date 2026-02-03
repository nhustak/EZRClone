using System.Windows;
using System.Windows.Controls;
using EZRClone.Helpers;
using EZRClone.Models;
using EZRClone.ViewModels;

namespace EZRClone.Views;

public partial class SearchView : UserControl
{
    private readonly ListViewSortHelper _sortHelper = new();

    public SearchView()
    {
        InitializeComponent();
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        _sortHelper.OnColumnHeaderClick(sender, e);
    }

    private List<RemoteItem> GetSelectedItems() =>
        ResultsListView.SelectedItems.Cast<RemoteItem>().ToList();

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            _ = vm.DownloadItemsAsync(GetSelectedItems());
    }

    private void OnDownloadToClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            _ = vm.DownloadItemsToAsync(GetSelectedItems());
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SearchViewModel vm)
            _ = vm.DeleteItemsAsync(GetSelectedItems());
    }
}
