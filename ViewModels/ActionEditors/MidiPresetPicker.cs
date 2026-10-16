using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;
using HotKeyManager.Services;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Picks a documented command from the MIDI presets and maintains the preset files
/// (save, update, import, open in the default editor).
/// </summary>
public partial class MidiPresetPicker : ObservableObject
{
    private readonly MidiPresetService presetService = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPreset))]
    [NotifyPropertyChangedFor(nameof(HasSelectedCommand))]
    private int presetIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandHint))]
    [NotifyPropertyChangedFor(nameof(HasCommandHint))]
    [NotifyPropertyChangedFor(nameof(HasSelectedCommand))]
    private int commandIndex = -1;

    [ObservableProperty]
    private string presetNotes = string.Empty;

    [ObservableProperty]
    private bool hasPresetNotes;

    /// <summary>Loads all presets (bundled and user).</summary>
    public MidiPresetPicker()
    {
        this.Reload();
    }

    /// <summary>Raised when the user picks a command; the editor copies its values.</summary>
    public event Action<MidiPresetCommand>? CommandChosen;

    public ObservableCollection<MidiPreset> Presets { get; } = new();

    /// <summary>Commands of the selected preset (mirrors its Commands list).</summary>
    public ObservableCollection<MidiPresetCommand> PresetCommands { get; } = new();

    public bool HasSelectedPreset => this.PresetIndex >= 0 && this.PresetIndex < this.Presets.Count;

    public bool HasSelectedCommand =>
        this.HasSelectedPreset && this.CommandIndex >= 0 && this.CommandIndex < this.PresetCommands.Count;

    public string CommandHint => this.HasSelectedCommand ? this.SelectedCommand!.Hint ?? string.Empty : string.Empty;

    public bool HasCommandHint => !string.IsNullOrEmpty(this.CommandHint);

    public MidiPreset? SelectedPreset => this.HasSelectedPreset ? this.Presets[this.PresetIndex] : null;

    public MidiPresetCommand? SelectedCommand =>
        this.HasSelectedCommand ? this.PresetCommands[this.CommandIndex] : null;

    /// <summary>Adds a command to the selected preset, or to a new "Eigene Befehle" preset, and selects it.</summary>
    public bool AddCommand(MidiPresetCommand command)
    {
        var preset = this.SelectedPreset ?? new MidiPreset
        {
            Name = "Eigene Befehle",
            Description = "Selbst getestete MIDI-Befehle.",
        };
        preset.Commands.Add(command);
        if (!this.presetService.Save(preset))
        {
            return false;
        }

        this.Reload(preset);
        this.CommandIndex = this.PresetCommands.Count - 1;
        return true;
    }

    /// <summary>Replaces the selected command and keeps it selected.</summary>
    public bool ReplaceSelectedCommand(MidiPresetCommand command)
    {
        // PresetCommands mirrors preset.Commands, so the index is the position in the preset.
        var index = this.CommandIndex;
        if (this.SelectedPreset is not { } preset || index < 0 || index >= preset.Commands.Count)
        {
            return false;
        }

        preset.Commands[index] = command;
        if (!this.presetService.Save(preset))
        {
            return false;
        }

        this.Reload(preset);
        this.CommandIndex = index;
        return true;
    }

    /// <summary>Imports a preset file and selects it; false for an invalid file.</summary>
    public bool Import(string path)
    {
        if (this.presetService.Import(path) is not { } imported)
        {
            return false;
        }

        this.Reload(imported);
        return true;
    }

    /// <summary>
    /// Opens the JSON file of the selected preset in the default program. Bundled presets are
    /// copied to the user folder first, so edits survive app updates.
    /// </summary>
    public bool OpenSelectedFile()
    {
        if (this.SelectedPreset is not { } preset)
        {
            return false;
        }

        var userFolder = MidiPresetService.UserFolder;
        var isUserCopy = preset.FilePath?.StartsWith(userFolder, StringComparison.OrdinalIgnoreCase) == true;
        if (!isUserCopy && !this.presetService.Save(preset))
        {
            return false;
        }

        if (string.IsNullOrEmpty(preset.FilePath) || !File.Exists(preset.FilePath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = preset.FilePath, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Preset-Datei konnte nicht geöffnet werden", ex);
            return false;
        }
    }

    /// <summary>Reads the presets again (after editing a file outside the app).</summary>
    public void ReloadFromDisk() => this.Reload(this.SelectedPreset);

    private void Reload(MidiPreset? select = null)
    {
        this.Presets.Clear();
        foreach (var preset in this.presetService.LoadAll())
        {
            this.Presets.Add(preset);
        }

        if (select is null)
        {
            return;
        }

        for (var i = 0; i < this.Presets.Count; i++)
        {
            if (string.Equals(this.Presets[i].Name, select.Name, StringComparison.OrdinalIgnoreCase))
            {
                this.PresetIndex = i;
                return;
            }
        }
    }

    partial void OnPresetIndexChanged(int value)
    {
        this.PresetCommands.Clear();
        this.CommandIndex = -1;

        if (!this.HasSelectedPreset)
        {
            this.PresetNotes = string.Empty;
            this.HasPresetNotes = false;
            return;
        }

        var preset = this.Presets[value];
        foreach (var command in preset.Commands)
        {
            this.PresetCommands.Add(command);
        }

        this.PresetNotes = preset.Notes ?? string.Empty;
        this.HasPresetNotes = !string.IsNullOrEmpty(this.PresetNotes);
    }

    partial void OnCommandIndexChanged(int value)
    {
        if (this.SelectedCommand is { } command)
        {
            this.CommandChosen?.Invoke(command);
        }
    }
}
