using System.Windows.Controls;
using System.Windows.Input;
using EZRClone.Models;
using EZRClone.ViewModels;

namespace EZRClone.Views;

public partial class BrowseView : UserControl
{
    public BrowseView()
    {
        InitializeComponent();
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem { Content: RemoteItem { IsDirectory: true } item }
            && DataContext is BrowseViewModel vm)
        {
            vm.NavigateToCommand.Execute(item);
        }
    }
}
