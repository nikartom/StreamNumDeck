using System.Reflection;
using System.Text.RegularExpressions;
using StreamNumDeck.App.Localization;
using StreamNumDeck.Core.Actions;
using StreamNumDeck.Core.Execution;

namespace StreamNumDeck.Wpf.Services;

internal static class UserErrorFormatter
{
    private static readonly Regex InlineParameterPattern = new(
        @"\s*\(Parameter\s+'[^']+'\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ParameterLinePattern = new(
        @"\r?\n\s*(Parameter name|Имя параметра)\s*:.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Format(string summary, Exception exception)
    {
        AppLogger.Error(summary, exception);
        var detail = GetSafeDetail(exception);
        if (string.IsNullOrWhiteSpace(detail)
            || string.Equals(summary.TrimEnd('.', '!', ' '), detail.TrimEnd('.', '!', ' '), StringComparison.OrdinalIgnoreCase))
        {
            return summary;
        }

        return $"{summary.TrimEnd()}\n\n{detail}";
    }

    public static string GetDetail(Exception exception)
    {
        var current = Unwrap(exception);
        var message = current.Message?.Trim() ?? string.Empty;
        message = InlineParameterPattern.Replace(message, string.Empty);
        message = ParameterLinePattern.Replace(message, string.Empty);
        return message.Trim();
    }

    public static string GetSafeDetail(Exception exception)
    {
        var current = Unwrap(exception);
        if (current is UserActionException actionException)
        {
            return FormatActionError(actionException);
        }

        if (current is KeyboardMacroFormatException macroException)
        {
            return FormatMacroError(macroException);
        }

        return current is ArgumentException
            or FormatException
            or FileNotFoundException
            or DirectoryNotFoundException
            or UnauthorizedAccessException
            ? GetDetail(current)
            : string.Empty;
    }

    private static string FormatActionError(UserActionException exception) => exception.Error switch
    {
        UserActionError.TargetUnavailable => AppStrings.Format(
            "ActionError_TargetUnavailable",
            exception.Subject ?? string.Empty),
        UserActionError.AudioSessionUnavailable => AppStrings.Format(
            "ActionError_AudioSessionUnavailable",
            exception.Subject ?? AppStrings.Get("Editor_Application.Header", "application")),
        UserActionError.ObsUnavailable => AppStrings.Get(
            "ActionError_ObsUnavailable",
            "OBS is not connected. Start OBS and check the WebSocket settings."),
        UserActionError.ObsActionRejected => AppStrings.Get(
            "ActionError_ObsActionRejected",
            "OBS rejected the configured action. Check its scene, source, or input."),
        UserActionError.AudioFileUnsupported => AppStrings.Format(
            "ActionError_AudioFileUnsupported",
            exception.Subject ?? AppStrings.Get("Editor_AudioFile.Header", "audio file")),
        _ => string.Empty,
    };

    private static string FormatMacroError(KeyboardMacroFormatException exception) => exception.Error switch
    {
        KeyboardMacroFormatError.Empty => AppStrings.Get(
            "ActionError_MacroEmpty",
            "Enter at least one keyboard shortcut."),
        KeyboardMacroFormatError.InvalidLine => AppStrings.Format(
            "ActionError_MacroInvalidLine",
            exception.LineNumber ?? 0),
        KeyboardMacroFormatError.TooManySteps => AppStrings.Format(
            "ActionError_MacroTooManySteps",
            KeyboardMacroActionDefinition.MaximumSteps),
        _ => string.Empty,
    };

    private static Exception Unwrap(Exception exception)
    {
        while (true)
        {
            if (exception is AggregateException aggregate)
            {
                exception = aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? exception;
                continue;
            }

            if (exception is TargetInvocationException && exception.InnerException is not null)
            {
                exception = exception.InnerException;
                continue;
            }

            return exception;
        }
    }
}
