using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

public partial class BatchEditorViewModel : ActionEditorViewModelBase
{
    public override string DisplayName => "Batch-Befehl ausführen";
    public override ActionType Type => ActionType.BatchCommand;
    public override string ValidationMessage => "Bitte gib einen Befehl ein.";

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

    public override ActionBase? BuildAction()
    {
        if (string.IsNullOrWhiteSpace(Command))
            return null;

        return new BatchAction
        {
            Command = Command,
            WorkingDirectory = WorkingDirectory,
            RunHidden = RunHidden,
            WaitForExit = WaitForExit
        };
    }
}
