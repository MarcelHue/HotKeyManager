using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>Editor for the block that runs a batch command.</summary>
public partial class BatchEditorViewModel : ActionEditorViewModelBase
{
    [ObservableProperty]
    private string command = string.Empty;

    [ObservableProperty]
    private string workingDirectory = string.Empty;

    [ObservableProperty]
    private bool runHidden = true;

    [ObservableProperty]
    private bool waitForExit;

    /// <inheritdoc />
    public override string DisplayName => "Batch-Befehl ausführen";

    /// <inheritdoc />
    public override ActionType Type => ActionType.BatchCommand;

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not BatchAction batch)
        {
            return;
        }

        this.Command = batch.Command;
        this.WorkingDirectory = batch.WorkingDirectory;
        this.RunHidden = batch.RunHidden;
        this.WaitForExit = batch.WaitForExit;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new BatchAction
    {
        Command = this.Command,
        WorkingDirectory = this.WorkingDirectory,
        RunHidden = this.RunHidden,
        WaitForExit = this.WaitForExit
    };
}
