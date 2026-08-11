using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

public partial class ProcessEditorViewModel : ActionEditorViewModelBase
{
    public override string DisplayName => "Programm starten";
    public override ActionType Type => ActionType.StartProcess;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private bool _runAsAdmin;

    [ObservableProperty]
    private bool _hidden;

    public override void LoadFrom(ActionBase action)
    {
        if (action is not ProcessAction process) return;

        FilePath = process.FilePath;
        Arguments = process.Arguments;
        WorkingDirectory = process.WorkingDirectory;
        RunAsAdmin = process.RunAsAdmin;
        Hidden = process.Hidden;
    }

    public override ActionBase BuildAction()
    {
        return new ProcessAction
        {
            FilePath = FilePath?.Trim() ?? string.Empty,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            RunAsAdmin = RunAsAdmin,
            Hidden = Hidden
        };
    }
}
