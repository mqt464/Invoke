namespace Invoke.Core.Rasi;

public sealed class RasiDocument
{
    public Dictionary<string, Dictionary<string, RasiValue>> Sections { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Imports { get; } = [];
    public string? ThemeReference { get; set; }

    public Dictionary<string, RasiValue> GetOrAddSection(string name)
    {
        if (!Sections.TryGetValue(name, out var section))
        {
            section = new Dictionary<string, RasiValue>(StringComparer.OrdinalIgnoreCase);
            Sections[name] = section;
        }

        return section;
    }

    public void Merge(RasiDocument other)
    {
        foreach (var import in other.Imports)
            Imports.Add(import);

        if (!string.IsNullOrWhiteSpace(other.ThemeReference))
            ThemeReference = other.ThemeReference;

        foreach (var section in other.Sections)
        {
            var target = GetOrAddSection(section.Key);
            foreach (var property in section.Value)
                target[property.Key] = property.Value;
        }
    }
}
