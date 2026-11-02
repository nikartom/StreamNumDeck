using System.Diagnostics;
using System.ComponentModel;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Infrastructure.Execution;

public sealed class SystemActionExecutor : IActionExecutor
{
    private readonly Func<ProcessStartInfo, Process?> startProcess;

    public SystemActionExecutor()
        : this(Process.Start)
    {
    }

    internal SystemActionExecutor(Func<ProcessStartInfo, Process?> startProcess)
    {
        this.startProcess = startProcess;
    }

    public bool CanExecute(ActionDefinition action) => action is
        LaunchProcessActionDefinition or
        OpenPathActionDefinition or
        OpenUriActionDefinition;

    public Task ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateTarget(action);

        var startInfo = action switch
        {
            LaunchProcessActionDefinition process => new ProcessStartInfo
            {
                FileName = process.ExecutablePath,
                Arguments = process.Arguments ?? string.Empty,
                WorkingDirectory = process.WorkingDirectory ?? string.Empty,
                UseShellExecute = true,
            },
            OpenPathActionDefinition path => new ProcessStartInfo
            {
                FileName = path.Path,
                UseShellExecute = true,
            },
            OpenUriActionDefinition uri => new ProcessStartInfo
            {
                FileName = uri.Uri,
                UseShellExecute = true,
            },
            _ => throw new NotSupportedException($"{action.GetType().Name} is not a system action."),
        };

        try
        {
            // No process is not a failure: the shell can hand the target over to
            // an application that is already running.
            startProcess(startInfo)?.Dispose();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            throw new UserActionException(UserActionError.TargetUnavailable, startInfo.FileName, exception);
        }

        return Task.CompletedTask;
    }

    private static void ValidateTarget(ActionDefinition action)
    {
        if (action is OpenPathActionDefinition path
            && !File.Exists(path.Path)
            && !Directory.Exists(path.Path))
        {
            throw new UserActionException(UserActionError.TargetUnavailable, path.Path);
        }

        if (action is not LaunchProcessActionDefinition process)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(process.WorkingDirectory)
            && !Directory.Exists(process.WorkingDirectory))
        {
            throw new UserActionException(UserActionError.TargetUnavailable, process.WorkingDirectory);
        }

        var hasDirectory = Path.IsPathRooted(process.ExecutablePath)
                           || !string.IsNullOrEmpty(Path.GetDirectoryName(process.ExecutablePath));
        if (hasDirectory && !File.Exists(process.ExecutablePath))
        {
            throw new UserActionException(UserActionError.TargetUnavailable, process.ExecutablePath);
        }
    }
}
