using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
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

    // Commit an inline text edit (double-click a Source/Target cell) back to the underlying label.
    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not RowViewModel row) return;
        if (e.EditingElement is not TextBox tb) return;
        if (DataContext is not MainViewModel vm) return;

        var path = (e.Column as DataGridBoundColumn)?.Binding is Binding b ? b.Path.Path : null;
        if (path != nameof(RowViewModel.SourceText) && path != nameof(RowViewModel.TargetText))
            return;

        var editingSource = path == nameof(RowViewModel.SourceText);
        vm.ApplyCellEdit(row, editingSource, tb.Text);

        // Defer the grid rebuild until after this commit finishes, to avoid re-entrancy.
        Dispatcher.BeginInvoke(new Action(vm.RefreshComparison), DispatcherPriority.Background);
    }
}
