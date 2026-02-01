using System.Windows;
using System.Windows.Controls;
using EZRClone.ViewModels;

namespace EZRClone.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ConfigViewModel vm)
        {
            vm.LoadRemotesCommand.Execute(null);
        }
    }
}
