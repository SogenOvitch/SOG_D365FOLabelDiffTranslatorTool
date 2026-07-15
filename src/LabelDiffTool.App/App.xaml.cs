using System.Windows;
using LabelDiffTool.App.Services;
using LabelDiffTool.App.ViewModels;
using LabelDiffTool.Core.Translation;

namespace LabelDiffTool.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Simple hand-wired composition. Swap GTranslateService here for a
        // LibreTranslate-backed ITranslationService without touching the UI.
        var dialogs = new WpfFileDialogService();
        var translator = new GTranslateService();
        var viewModel = new MainViewModel(dialogs, translator);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }
}
