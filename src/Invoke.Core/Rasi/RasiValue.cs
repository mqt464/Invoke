using System.Globalization;

namespace Invoke.Core.Rasi;

public enum RasiValueKind
{
    Null,
    String,
    Number,
    Boolean,
    List,
    Function,
    Identifier
}

public sealed record RasiValue(
    RasiValueKind Kind,
    string Raw,
    string? StringValue = null,
    double? NumberValue = null,
    bool? BooleanValue = null,
    IReadOnlyList<RasiValue>? ListValue = null,
    string? FunctionName = null,
    IReadOnlyList<RasiValue>? FunctionArguments = null)
{
    public static RasiValue Null() => new(RasiValueKind.Null, string.Empty);
    public static RasiValue String(string value) => new(RasiValueKind.String, value, StringValue: value);
    public static RasiValue Number(double value, string? raw = null) =>
        new(RasiValueKind.Number, raw ?? value.ToString(CultureInfo.InvariantCulture), NumberValue: value);
    public static RasiValue Boolean(bool value) => new(RasiValueKind.Boolean, value ? "true" : "false", BooleanValue: value);
    public static RasiValue Identifier(string value) => new(RasiValueKind.Identifier, value, StringValue: value);
    public static RasiValue List(IReadOnlyList<RasiValue> values, string raw) => new(RasiValueKind.List, raw, ListValue: values);
    public static RasiValue Function(string name, IReadOnlyList<RasiValue> arguments, string raw) =>
        new(RasiValueKind.Function, raw, FunctionName: name, FunctionArguments: arguments);

    public string AsString(string fallback = "") => Kind switch
    {
        RasiValueKind.String or RasiValueKind.Identifier => StringValue ?? fallback,
        RasiValueKind.Number => Raw.Length > 0 ? Raw : NumberValue?.ToString(CultureInfo.InvariantCulture) ?? fallback,
        RasiValueKind.Boolean => BooleanValue is true ? "true" : "false",
        RasiValueKind.List => string.Join(" ", ListValue?.Select(static value => value.AsString()) ?? []),
        _ => fallback
    };

    public bool AsBoolean(bool fallback = false) => Kind switch
    {
        RasiValueKind.Boolean => BooleanValue ?? fallback,
        RasiValueKind.Identifier or RasiValueKind.String => bool.TryParse(StringValue, out var value) ? value : fallback,
        _ => fallback
    };

    public double AsNumber(double fallback = 0) => Kind switch
    {
        RasiValueKind.Number => NumberValue ?? fallback,
        RasiValueKind.Identifier or RasiValueKind.String => double.TryParse(StringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback,
        _ => fallback
    };
}
