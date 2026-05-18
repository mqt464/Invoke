using System.Diagnostics;
using Invoke.Core.Dmenu;

if (args.Length > 0 && args[0].Equals("dmenu", StringComparison.OrdinalIgnoreCase))
    return await RunDmenuAsync(args[1..]);

Console.Error.WriteLine("Usage: invoke-cli dmenu [-i] [-p prompt] [-mesg text] [-window-title text] [-l lines] [-a rows] [-u rows] [-selected-row n] [-select value] [-format spec] [-sep c] [-dump] [-markup-rows] [-only-match] [-no-custom] [-filter query]");
return 1;

static async Task<int> RunDmenuAsync(string[] args)
{
    var prompt = "dmenu";
    string? message = null;
    string? windowTitle = null;
    string? filter = null;
    string? preselectedText = null;
    int? selectedRow = null;
    var format = "s";
    var separator = "\n";
    var caseSensitive = true;
    var noCustom = false;
    var lines = 10;
    var markupRows = false;
    var dump = false;
    var urgentRows = new List<int>();
    var activeRows = new List<int>();

    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "-i":
                caseSensitive = false;
                break;
            case "-p" when index + 1 < args.Length:
                prompt = args[++index];
                break;
            case "-mesg" when index + 1 < args.Length:
                message = args[++index];
                break;
            case "-window-title" when index + 1 < args.Length:
                windowTitle = args[++index];
                break;
            case "-l" when index + 1 < args.Length && int.TryParse(args[index + 1], out var parsedLines):
                lines = Math.Max(1, parsedLines);
                index++;
                break;
            case "-a" when index + 1 < args.Length:
                urgentRows.AddRange(ParseRowSpec(args[++index]));
                break;
            case "-u" when index + 1 < args.Length:
                activeRows.AddRange(ParseRowSpec(args[++index]));
                break;
            case "-selected-row" when index + 1 < args.Length && int.TryParse(args[index + 1], out var parsedSelectedRow):
                selectedRow = Math.Max(0, parsedSelectedRow);
                index++;
                break;
            case "-select" when index + 1 < args.Length:
                preselectedText = args[++index];
                break;
            case "-format" when index + 1 < args.Length:
                format = args[++index];
                break;
            case "-sep" when index + 1 < args.Length:
                separator = DecodeSeparator(args[++index]);
                break;
            case "-dump":
                dump = true;
                break;
            case "-sync":
                break;
            case "-markup-rows":
                markupRows = true;
                break;
            case "-only-match":
                noCustom = true;
                break;
            case "-no-custom":
                noCustom = true;
                break;
            case "-filter" when index + 1 < args.Length:
                filter = args[++index];
                break;
        }
    }

    var input = await Console.In.ReadToEndAsync().ConfigureAwait(false);
    var entries = SplitEntries(input, separator);
    if (entries.Length == 0)
        return 1;

    if (dump)
    {
        var filtered = DmenuSelector.FilterIndexed(entries, filter ?? string.Empty, caseSensitive)
            .Select(static entry => entry.Entry);
        foreach (var entry in filtered)
            Console.Out.WriteLine(entry);

        return 0;
    }

    var sessionDirectory = Path.Combine(Path.GetTempPath(), "invoke-dmenu");
    Directory.CreateDirectory(sessionDirectory);
    var sessionId = Guid.NewGuid().ToString("N");
    var sessionPath = Path.Combine(sessionDirectory, sessionId + ".json");
    var outputPath = Path.Combine(sessionDirectory, sessionId + ".out");

    new DmenuSession
    {
        Prompt = prompt,
        Message = message,
        WindowTitle = windowTitle,
        InitialQuery = filter ?? string.Empty,
        CaseInsensitive = !caseSensitive,
        NoCustom = noCustom,
        MarkupRows = markupRows,
        Format = string.IsNullOrWhiteSpace(format) ? "s" : format,
        Separator = separator,
        SelectedRow = selectedRow,
        PreselectedText = preselectedText,
        UrgentRows = urgentRows.Distinct().OrderBy(static value => value).ToList(),
        ActiveRows = activeRows.Distinct().OrderBy(static value => value).ToList(),
        Lines = Math.Min(Math.Max(1, lines), Math.Max(1, entries.Length)),
        Entries = [.. entries],
        OutputPath = outputPath
    }.Save(sessionPath);

    var appPath = ResolveInvokeAppPath();
    if (!File.Exists(appPath))
        return 1;

    using var process = Process.Start(new ProcessStartInfo(appPath, $"--dmenu-session \"{sessionPath}\"")
    {
        UseShellExecute = true
    });
    if (process is null)
        return 1;

    while (!process.HasExited && !File.Exists(outputPath))
        await Task.Delay(50).ConfigureAwait(false);

    if (!File.Exists(outputPath))
        return 1;

    var selectedText = (await File.ReadAllTextAsync(outputPath).ConfigureAwait(false)).TrimEnd('\r', '\n');
    if (string.IsNullOrEmpty(selectedText))
        return 1;

    Console.Out.WriteLine(selectedText);
    TryDelete(sessionPath);
    TryDelete(outputPath);
    return 0;
}

static string ResolveInvokeAppPath()
{
    var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
    var configDirectory = baseDirectory.Parent?.Name ?? "Debug";
    var targetFramework = baseDirectory.Name;
    var srcDirectory = baseDirectory.Parent?.Parent?.Parent?.Parent;
    if (srcDirectory is null)
        return string.Empty;

    return Path.Combine(srcDirectory.FullName, "Invoke.App", "bin", configDirectory, targetFramework, "Invoke.App.exe");
}

static void TryDelete(string path)
{
    try
    {
        if (File.Exists(path))
            File.Delete(path);
    }
    catch
    {
    }
}

static IEnumerable<int> ParseRowSpec(string raw)
{
    foreach (var segment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var separatorIndex = segment.IndexOfAny(['-', ':']);
        if (separatorIndex > 0 &&
            int.TryParse(segment[..separatorIndex], out var start) &&
            int.TryParse(segment[(separatorIndex + 1)..], out var end))
        {
            var low = Math.Min(start, end);
            var high = Math.Max(start, end);
            for (var index = low; index <= high; index++)
                yield return index;

            continue;
        }

        if (int.TryParse(segment, out var value))
            yield return value;
    }
}

static string[] SplitEntries(string input, string separator)
{
    if (string.IsNullOrEmpty(separator) || separator == "\n")
    {
        return input
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }

    return input
        .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string DecodeSeparator(string raw) => raw switch
{
    "\\n" => "\n",
    "\\r" => "\r",
    "\\t" => "\t",
    "\\0" => "\0",
    _ => raw
};
