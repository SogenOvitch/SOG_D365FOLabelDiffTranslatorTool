using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LabelDiffTool.App.ViewModels;

namespace LabelDiffTool.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is MainViewModel vm && !vm.ConfirmClose())
            e.Cancel = true;
    }

    // DataGrid.SelectedItems isn't bindable, so push the multi-selection to the view model here.
    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && DataContext is MainViewModel vm)
            vm.SetSelectedRows(grid.SelectedItems.OfType<RowViewModel>());
    }
}
