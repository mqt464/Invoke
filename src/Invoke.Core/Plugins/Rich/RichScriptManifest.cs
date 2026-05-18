namespace Invoke.Core.Plugins.Rich;

public sealed class RichScriptManifest
{
    public const string ExactTriggerMode = "exact";
    public const string PrefixTokenTriggerMode = "prefix-token";

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "external";
    public string Entry { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public int Priority { get; set; } = 50;
    public bool KeepAlive { get; set; }
    public List<string> Triggers { get; set; } = [];
    public string TriggerMode { get; set; } = ExactTriggerMode;
    public bool RunForQuery { get; set; }
    public string? BackgroundEntry { get; set; }
    public string? BackgroundArguments { get; set; }
    public bool BackgroundRestartOnExit { get; set; }
}

public sealed record RichScriptDefinition(string Directory, RichScriptManifest Manifest);
