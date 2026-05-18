using System.Globalization;
using System.Text;

namespace Invoke.Core.Rasi;

public static class RasiParser
{
    public static RasiDocument Parse(string text)
    {
        var parser = new Parser(text);
        return parser.ParseDocument();
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text)
        {
            _text = text ?? string.Empty;
        }

        public RasiDocument ParseDocument()
        {
            var document = new RasiDocument();
            while (!IsEnd())
            {
                SkipTrivia();
                if (IsEnd())
                    break;

                if (MatchKeyword("@import"))
                {
                    SkipTrivia();
                    document.Imports.Add(ParseStringLike());
                    ConsumeOptional(';');
                    continue;
                }

                if (MatchKeyword("@theme"))
                {
                    SkipTrivia();
                    document.ThemeReference = ParseStringLike();
                    ConsumeOptional(';');
                    continue;
                }

                var selectors = ParseSelectors();
                SkipTrivia();
                Expect('{');
                var properties = ParsePropertyBlock();
                foreach (var selector in selectors)
                {
                    var target = document.GetOrAddSection(selector);
                    foreach (var property in properties)
                        target[property.Key] = property.Value;
                }
            }

            return document;
        }

        private Dictionary<string, RasiValue> ParsePropertyBlock()
        {
            var properties = new Dictionary<string, RasiValue>(StringComparer.OrdinalIgnoreCase);
            while (!IsEnd())
            {
                SkipTrivia();
                if (Peek() == '}')
                {
                    _index++;
                    break;
                }

                var name = ParsePropertyName();
                SkipTrivia();
                Expect(':');
                SkipTrivia();
                properties[name] = ParseValueUntil(';', '}');
                SkipTrivia();
                ConsumeOptional(';');
            }

            return properties;
        }

        private List<string> ParseSelectors()
        {
            var selectors = new List<string>();
            var builder = new StringBuilder();
            while (!IsEnd())
            {
                SkipTrivia();
                var current = ParseSelectorAtom();
                builder.Append(current);
                SkipTrivia();
                if (Peek() == ',')
                {
                    selectors.Add(NormalizeSelector(builder.ToString()));
                    builder.Clear();
                    _index++;
                    continue;
                }

                if (Peek() == '{')
                {
                    selectors.Add(NormalizeSelector(builder.ToString()));
                    break;
                }

                builder.Append(' ');
            }

            return selectors.Count == 0 ? ["configuration"] : selectors;
        }

        private string ParseSelectorAtom()
        {
            SkipTrivia();
            if (Peek() == '*')
            {
                _index++;
                return "*";
            }

            return ParseBareWord(allowDashes: true, allowDots: true, allowHashes: true, allowAt: false);
        }

        private string ParsePropertyName()
        {
            SkipTrivia();
            var start = _index;
            while (!IsEnd())
            {
                var current = Peek();
                if (IsBareWordCharacter(current, allowDashes: true, allowDots: true, allowHashes: false, allowAt: false, allowColons: false))
                {
                    _index++;
                    continue;
                }

                if (current == ':' &&
                    _index > start &&
                    IsBareWordCharacter(_text[_index - 1], allowDashes: true, allowDots: true, allowHashes: false, allowAt: false, allowColons: false) &&
                    IsBareWordCharacter(Peek(1), allowDashes: true, allowDots: true, allowHashes: false, allowAt: false, allowColons: false))
                {
                    _index++;
                    continue;
                }

                break;
            }

            return _text[start.._index];
        }

        private RasiValue ParseValueUntil(params char[] terminators)
        {
            SkipTrivia();
            var values = new List<RasiValue>();
            var rawBuilder = new StringBuilder();
            while (!IsEnd())
            {
                SkipTrivia();
                var next = Peek();
                if (next == '\0' || terminators.Contains(next))
                    break;

                var value = ParseSingleValue();
                values.Add(value);
                if (rawBuilder.Length > 0)
                    rawBuilder.Append(' ');
                rawBuilder.Append(value.Raw);
                SkipTrivia();
                if (Peek() == ',')
                {
                    rawBuilder.Append(',');
                    _index++;
                }
            }

            if (values.Count == 0)
                return RasiValue.Null();

            if (values.Count == 1)
                return values[0];

            return RasiValue.List(values, rawBuilder.ToString());
        }

        private RasiValue ParseSingleValue()
        {
            SkipTrivia();
            var next = Peek();
            if (next == '[')
                return ParseBracketList();
            if (next == '"' || next == '\'')
            {
                var value = ParseQuotedString();
                return RasiValue.String(value);
            }

            var atom = ParseBareWord(allowDashes: true, allowDots: true, allowHashes: true, allowAt: true, allowColons: true);
            if (string.IsNullOrEmpty(atom))
                throw new FormatException($"Unexpected token '{Peek()}' at index {_index}.");
            SkipTrivia();
            if (Peek() == '(')
                return ParseFunction(atom);

            if (bool.TryParse(atom, out var booleanValue))
                return RasiValue.Boolean(booleanValue);

            if (double.TryParse(TrimNumericSuffix(atom), NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
                return RasiValue.Number(numberValue, atom);

            return RasiValue.Identifier(atom);
        }

        private RasiValue ParseFunction(string functionName)
        {
            var rawBuilder = new StringBuilder(functionName);
            Expect('(');
            rawBuilder.Append('(');
            var arguments = new List<RasiValue>();
            while (!IsEnd())
            {
                SkipTrivia();
                if (Peek() == ')')
                {
                    _index++;
                    rawBuilder.Append(')');
                    break;
                }

                var argument = ParseSingleValue();
                arguments.Add(argument);
                rawBuilder.Append(argument.Raw);
                SkipTrivia();
                if (Peek() == ',')
                {
                    _index++;
                    rawBuilder.Append(',');
                }
            }

            return RasiValue.Function(functionName, arguments, rawBuilder.ToString());
        }

        private RasiValue ParseBracketList()
        {
            var rawBuilder = new StringBuilder();
            var values = new List<RasiValue>();
            Expect('[');
            rawBuilder.Append('[');
            while (!IsEnd())
            {
                SkipTrivia();
                if (Peek() == ']')
                {
                    _index++;
                    rawBuilder.Append(']');
                    break;
                }

                var value = ParseSingleValue();
                values.Add(value);
                rawBuilder.Append(value.Raw);
                SkipTrivia();
                if (Peek() == ',')
                {
                    _index++;
                    rawBuilder.Append(',');
                }
            }

            return RasiValue.List(values, rawBuilder.ToString());
        }

        private string ParseStringLike()
        {
            SkipTrivia();
            if (Peek() == '"' || Peek() == '\'')
                return ParseQuotedString();

            return ParseBareWord(allowDashes: true, allowDots: true, allowHashes: true, allowAt: true, allowColons: true);
        }

        private string ParseQuotedString()
        {
            var quote = Peek();
            Expect(quote);
            var builder = new StringBuilder();
            while (!IsEnd())
            {
                var current = _text[_index++];
                if (current == '\\' && !IsEnd())
                {
                    var escaped = _text[_index++];
                    builder.Append(escaped switch
                    {
                        '\\' => '\\',
                        '"' => '"',
                        '\'' => '\'',
                        _ => new string(['\\', escaped])
                    });
                    continue;
                }

                if (current == quote)
                    break;

                builder.Append(current);
            }

            return builder.ToString();
        }

        private string ParseBareWord(bool allowDashes, bool allowDots, bool allowHashes, bool allowAt, bool allowColons = false)
        {
            var start = _index;
            while (!IsEnd())
            {
                var current = Peek();
                if (IsBareWordCharacter(current, allowDashes, allowDots, allowHashes, allowAt, allowColons))
                {
                    _index++;
                    continue;
                }

                break;
            }

            return _text[start.._index];
        }

        private static bool IsBareWordCharacter(char current, bool allowDashes, bool allowDots, bool allowHashes, bool allowAt, bool allowColons)
        {
            return char.IsLetterOrDigit(current) ||
                   current == '_' ||
                   (allowDashes && current == '-') ||
                   (allowDots && current == '.') ||
                   (allowHashes && current == '#') ||
                   (allowAt && current == '@') ||
                   (allowColons && current == ':') ||
                   current == '%' ||
                   current == '/' ||
                   current == '+' ||
                   current == '*';
        }

        private void SkipTrivia()
        {
            while (!IsEnd())
            {
                if (char.IsWhiteSpace(Peek()))
                {
                    _index++;
                    continue;
                }

                if (Peek() == '/' && Peek(1) == '/')
                {
                    _index += 2;
                    while (!IsEnd() && Peek() != '\n')
                        _index++;
                    continue;
                }

                if (Peek() == '/' && Peek(1) == '*')
                {
                    _index += 2;
                    while (!IsEnd() && !(Peek() == '*' && Peek(1) == '/'))
                        _index++;
                    if (!IsEnd())
                        _index += 2;
                    continue;
                }

                break;
            }
        }

        private bool MatchKeyword(string keyword)
        {
            SkipTrivia();
            if (!_text.AsSpan(_index).StartsWith(keyword, StringComparison.Ordinal))
                return false;

            _index += keyword.Length;
            return true;
        }

        private void Expect(char expected)
        {
            if (Peek() != expected)
                throw new FormatException($"Expected '{expected}' at index {_index}.");

            _index++;
        }

        private void ConsumeOptional(char value)
        {
            SkipTrivia();
            if (Peek() == value)
                _index++;
        }

        private char Peek(int offset = 0)
        {
            var nextIndex = _index + offset;
            return nextIndex >= _text.Length ? '\0' : _text[nextIndex];
        }

        private bool IsEnd() => _index >= _text.Length;

        private static string NormalizeSelector(string value) =>
            string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        private static string TrimNumericSuffix(string value)
        {
            var index = 0;
            while (index < value.Length &&
                   (char.IsDigit(value[index]) || value[index] is '.' or '-' or '+'))
            {
                index++;
            }

            return index == 0 ? value : value[..index];
        }
    }
}
