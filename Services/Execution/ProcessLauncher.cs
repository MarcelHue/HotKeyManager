using System.Diagnostics;
using HotKeyManager.Models;

namespace HotKeyManager.Services.Execution;

/// <summary>Starts programs and cmd.exe batch commands.</summary>
internal static class ProcessLauncher
{
    public static void Start(ProcessAction action)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = action.FilePath,
            Arguments = action.Arguments,
            UseShellExecute = true,
            CreateNoWindow = action.Hidden,
            WindowStyle = action.Hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
        };

        if (!string.IsNullOrEmpty(action.WorkingDirectory))
        {
            startInfo.WorkingDirectory = action.WorkingDirectory;
        }

        if (action.RunAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        Process.Start(startInfo);
    }

    public static void RunBatch(BatchAction action)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {action.Command}",
            UseShellExecute = false,
            CreateNoWindow = action.RunHidden,
            WindowStyle = action.RunHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
        };

        if (!string.IsNullOrEmpty(action.WorkingDirectory))
        {
            startInfo.WorkingDirectory = action.WorkingDirectory;
        }

        using var process = Process.Start(startInfo);
        if (action.WaitForExit)
        {
            process?.WaitForExit();
        }
    }
}
