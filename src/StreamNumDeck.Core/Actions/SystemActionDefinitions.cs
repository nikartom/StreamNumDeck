namespace StreamNumDeck.Core.Actions;

public sealed record LaunchProcessActionDefinition : ActionDefinition
{
    public LaunchProcessActionDefinition(string executablePath, string? arguments = null, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("An executable path is required.", nameof(executablePath));
        }

        ExecutablePath = executablePath!.Trim();
        Arguments = arguments?.Trim();
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory!.Trim();
    }

    public override ActionGroup Group => ActionGroup.System;

    public string ExecutablePath { get; }

    public string? Arguments { get; }

    public string? WorkingDirectory { get; }
}

public sealed record OpenPathActionDefinition : ActionDefinition
{
    public OpenPathActionDefinition(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file or directory path is required.", nameof(path));
        }

        Path = path.Trim();
    }

    public override ActionGroup Group => ActionGroup.System;

    public string Path { get; }
}

public sealed record OpenUriActionDefinition : ActionDefinition
{
    public OpenUriActionDefinition(string uri)
    {
        if (!System.Uri.TryCreate(uri, System.UriKind.Absolute, out var parsedUri) ||
            parsedUri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS addresses are supported.", nameof(uri));
        }

        Uri = parsedUri.AbsoluteUri;
    }

    public override ActionGroup Group => ActionGroup.System;

    public string Uri { get; }
}
