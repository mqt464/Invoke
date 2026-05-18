namespace Invoke.Core.Plugins.External;

public sealed record ExternalPluginRequest(
    string Raw,
    string Trigger,
    string Terms,
    string Kind,
    int MaxResults);
