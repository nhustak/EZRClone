using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EZRClone.Helpers;
using EZRClone.Models;
using EZRClone.ViewModels;

namespace EZRClone.Views;

public partial class BrowseView : UserControl
{
    private readonly ListViewSortHelper _sortHelper = new();

    public BrowseView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            await vm.EnsureInitializedAsync();
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem { Content: RemoteItem { IsDirectory: true } item }
            && DataContext is BrowseViewModel vm)
        {
            vm.NavigateToCommand.Execute(item);
        }
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        _sortHelper.OnColumnHeaderClick(sender, e);
    }

    private List<RemoteItem> GetSelectedItems() =>
        FileListView.SelectedItems.Cast<RemoteItem>().ToList();

    private void OnDownloadClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            _ = vm.DownloadItemsAsync(GetSelectedItems());
    }

    private void OnDownloadToClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            _ = vm.DownloadItemsToAsync(GetSelectedItems());
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is BrowseViewModel vm)
            _ = vm.DeleteItemsAsync(GetSelectedItems());
    }
}
