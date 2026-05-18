namespace Invoke.Core.Plugins.External;

public sealed class ExternalPluginResult
{
    public string? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? Kind { get; set; }
    public double? Score { get; set; }
    public string? Icon { get; set; }
    public ExternalPluginAction? Action { get; set; }
}

public sealed class ExternalPluginResponse
{
    public string? Prompt { get; set; }
    public string? Message { get; set; }
    public string? DisplayPrefix { get; set; }
    public string? RawPrefix { get; set; }
    public List<ExternalPluginResult> Results { get; set; } = [];
}

public sealed class ExternalPluginAction
{
    public string? Title { get; set; }
    public string? Kind { get; set; }
    public string? Command { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? Url { get; set; }
    public string? Path { get; set; }
    public string? Text { get; set; }
    public string? Query { get; set; }
    public bool RunAsAdministrator { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string? ConfirmationText { get; set; }
}
