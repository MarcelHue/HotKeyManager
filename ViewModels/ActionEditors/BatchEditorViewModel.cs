using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

public partial class BatchEditorViewModel : ActionEditorViewModelBase
{
    public override string DisplayName => "Batch-Befehl ausführen";
    public override ActionType Type => ActionType.BatchCommand;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private bool _runHidden = true;

    [ObservableProperty]
    private bool _waitForExit;

    public override void LoadFrom(ActionBase action)
    {
        if (action is not BatchAction batch) return;

        Command = batch.Command;
        WorkingDirectory = batch.WorkingDirectory;
        RunHidden = batch.RunHidden;
        WaitForExit = batch.WaitForExit;
    }

    public override ActionBase BuildAction()
    {
        return new BatchAction
        {
            Command = Command,
            WorkingDirectory = WorkingDirectory,
            RunHidden = RunHidden,
            WaitForExit = WaitForExit
        };
    }
}
