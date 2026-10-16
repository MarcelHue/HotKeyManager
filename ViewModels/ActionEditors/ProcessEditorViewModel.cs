using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>Editor for the block that starts a program.</summary>
public partial class ProcessEditorViewModel : ActionEditorViewModelBase
{
    [ObservableProperty]
    private string filePath = string.Empty;

    [ObservableProperty]
    private string arguments = string.Empty;

    [ObservableProperty]
    private string workingDirectory = string.Empty;

    [ObservableProperty]
    private bool runAsAdmin;

    [ObservableProperty]
    private bool hidden;

    /// <inheritdoc />
    public override string DisplayName => "Programm starten";

    /// <inheritdoc />
    public override ActionType Type => ActionType.StartProcess;

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not ProcessAction process)
        {
            return;
        }

        this.FilePath = process.FilePath;
        this.Arguments = process.Arguments;
        this.WorkingDirectory = process.WorkingDirectory;
        this.RunAsAdmin = process.RunAsAdmin;
        this.Hidden = process.Hidden;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new ProcessAction
    {
        FilePath = this.FilePath?.Trim() ?? string.Empty,
        Arguments = this.Arguments,
        WorkingDirectory = this.WorkingDirectory,
        RunAsAdmin = this.RunAsAdmin,
        Hidden = this.Hidden
    };
}
