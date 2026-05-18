using System.Diagnostics;

namespace Invoke.Core.Plugins.Rich;

public static class RichScriptProcessResolver
{
    public static ProcessStartInfo CreateStartInfo(RichScriptDefinition definition, string? suffixArguments = null)
    {
        var resolved = Resolve(definition, suffixArguments);
        return new ProcessStartInfo(resolved.FileName, resolved.Arguments)
        {
            WorkingDirectory = definition.Directory
        };
    }

    public static (string FileName, string Arguments) Resolve(RichScriptDefinition definition, string? suffixArguments = null)
    {
        var entryPath = Path.Combine(definition.Directory, definition.Manifest.Entry);
        return Resolve(entryPath, definition.Manifest.Arguments, suffixArguments);
    }

    public static (string FileName, string Arguments) ResolveBackground(RichScriptDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Manifest.BackgroundEntry))
            throw new InvalidOperationException($"Script {definition.Manifest.Id} does not define a background entry.");

        var entryPath = Path.Combine(definition.Directory, definition.Manifest.BackgroundEntry);
        return Resolve(entryPath, definition.Manifest.BackgroundArguments, null);
    }

    public static ProcessStartInfo CreateBackgroundStartInfo(RichScriptDefinition definition)
    {
        var resolved = ResolveBackground(definition);
        return new ProcessStartInfo(resolved.FileName, resolved.Arguments)
        {
            WorkingDirectory = definition.Directory
        };
    }

    private static (string FileName, string Arguments) Resolve(string entryPath, string? baseArguments, string? suffixArguments)
    {
        var extension = Path.GetExtension(entryPath);
        var arguments = baseArguments ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(suffixArguments))
            arguments = string.Join(' ', new[] { arguments, suffixArguments }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return extension.ToLowerInvariant() switch
        {
            ".ps1" => ("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File {Quote(entryPath)} {arguments}".Trim()),
            ".cmd" or ".bat" => ("cmd.exe", $"/c {Quote(entryPath)} {arguments}".Trim()),
            _ => (entryPath, arguments)
        };
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
