using System.Text.Json;

namespace Invoke.Core.Dmenu;

public sealed class DmenuSession
{
    public string Prompt { get; set; } = "dmenu";
    public string? Message { get; set; }
    public string? WindowTitle { get; set; }
    public string? InitialQuery { get; set; }
    public bool CaseInsensitive { get; set; }
    public bool NoCustom { get; set; }
    public bool MarkupRows { get; set; }
    public string Format { get; set; } = "s";
    public string Separator { get; set; } = "\n";
    public int? SelectedRow { get; set; }
    public string? PreselectedText { get; set; }
    public List<int> UrgentRows { get; set; } = [];
    public List<int> ActiveRows { get; set; } = [];
    public int Lines { get; set; } = 10;
    public List<string> Entries { get; set; } = [];
    public string OutputPath { get; set; } = string.Empty;

    public static DmenuSession Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<DmenuSession>(json) ?? new DmenuSession();
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(this);
        File.WriteAllText(path, json);
    }
}
