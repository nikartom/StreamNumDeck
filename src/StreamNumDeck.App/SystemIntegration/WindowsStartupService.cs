using Windows.ApplicationModel;
using StreamNumDeck.App.Localization;

namespace StreamNumDeck.App.SystemIntegration;

public sealed class WindowsStartupService
{
    public const string TaskId = "StreamNumDeckStartup";

    public async Task<bool> IsEnabledAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = await StartupTask.GetAsync(TaskId);

        if (!enabled)
        {
            if (task.State is StartupTaskState.EnabledByPolicy)
            {
                throw new InvalidOperationException(AppStrings.Get("Error_AutostartPolicyEnabled"));
            }

            if (task.State is StartupTaskState.Enabled)
            {
                task.Disable();
            }

            return;
        }

        if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
        {
            return;
        }

        var state = await task.RequestEnableAsync();
        if (state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
        {
            return;
        }

        var message = state switch
        {
            StartupTaskState.DisabledByUser =>
                AppStrings.Get("Error_AutostartDisabledByUser"),
            StartupTaskState.DisabledByPolicy =>
                AppStrings.Get("Error_AutostartPolicyDisabled"),
            _ => AppStrings.Get("Error_AutostartEnable"),
        };
        throw new InvalidOperationException(message);
    }
}
