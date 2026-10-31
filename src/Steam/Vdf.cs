using System.Text;

namespace SteamWorkshopUnsub.Steam;

internal sealed record VdfEntry(string Key, string? Value, VdfObject? Children)
{
    public bool IsObject => Children is not null;
}

internal sealed class VdfObject
{
    public List<VdfEntry> Entries { get; } = [];

    public VdfEntry? Find(string key) =>
        Entries.FirstOrDefault(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public VdfObject? FindObject(string key) => Find(key)?.Children;

    public string? FindValue(string key) => Find(key)?.Value;

    public bool Remove(string key) =>
        Entries.RemoveAll(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) > 0;

    public IEnumerable<VdfEntry> Descendants(string key)
    {
        foreach (var entry in Entries)
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                yield return entry;
            }

            if (entry.Children is not null)
            {
                foreach (var descendant in entry.Children.Descendants(key))
                {
                    yield return descendant;
                }
            }
        }
    }
}

internal static class VdfFile
{
    public static VdfObject Load(string path) => Parse(File.ReadAllText(path));

    public static VdfObject Parse(string content) => new Parser(content).Parse();

    public static void SaveAtomic(string path, VdfObject document)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            // Write beside the destination so the final move stays on the same
            // filesystem and cannot leave a partially written Steam config.
            File.WriteAllText(temporaryPath, Serialize(document), new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string Serialize(VdfObject document)
    {
        var output = new StringBuilder();
        WriteObject(output, document, 0);
        return output.ToString();
    }

    private static void WriteObject(StringBuilder output, VdfObject value, int depth)
    {
        string indentation = new('\t', depth);
        foreach (var entry in value.Entries)
        {
            output.Append(indentation).Append('"').Append(Escape(entry.Key)).Append('"');
            if (entry.Children is null)
            {
                output.Append("\t\t\"").Append(Escape(entry.Value ?? string.Empty)).AppendLine("\"");
                continue;
            }

            output.AppendLine();
            output.Append(indentation).AppendLine("{");
            WriteObject(output, entry.Children, depth + 1);
            output.Append(indentation).AppendLine("}");
        }
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private enum TokenKind
    {
        Value,
        OpenBrace,
        CloseBrace,
        End
    }

    private readonly record struct Token(TokenKind Kind, string Value = "");

    private sealed class Parser(string content)
    {
        private int _position;
        private Token? _lookahead;

        public VdfObject Parse()
        {
            var result = ParseObject(false);
            if (Peek().Kind != TokenKind.End)
            {
                throw Error("Unexpected content after the root object");
            }

            return result;
        }

        private VdfObject ParseObject(bool expectClosingBrace)
        {
            var result = new VdfObject();
            while (true)
            {
                // A VDF object is a sequence of key/value pairs. A value that
                // starts with "{" recursively becomes another VdfObject.
                Token key = Read();
                if (key.Kind == TokenKind.CloseBrace)
                {
                    if (!expectClosingBrace)
                    {
                        throw Error("Unexpected closing brace");
                    }

                    return result;
                }

                if (key.Kind == TokenKind.End)
                {
                    if (expectClosingBrace)
                    {
                        throw Error("Missing closing brace");
                    }

                    return result;
                }

                if (key.Kind != TokenKind.Value)
                {
                    throw Error("Expected a key");
                }

                Token value = Read();
                if (value.Kind == TokenKind.OpenBrace)
                {
                    result.Entries.Add(new VdfEntry(key.Value, null, ParseObject(true)));
                }
                else if (value.Kind == TokenKind.Value)
                {
                    result.Entries.Add(new VdfEntry(key.Value, value.Value, null));
                }
                else
                {
                    throw Error($"Expected a value or object after '{key.Value}'");
                }
            }
        }

        private Token Peek() => _lookahead ??= NextToken();

        private Token Read()
        {
            Token token = Peek();
            _lookahead = null;
            return token;
        }

        private Token NextToken()
        {
            SkipTrivia();
            if (_position >= content.Length)
            {
                return new Token(TokenKind.End);
            }

            char current = content[_position++];
            if (current == '{') return new Token(TokenKind.OpenBrace);
            if (current == '}') return new Token(TokenKind.CloseBrace);
            if (current == '"') return new Token(TokenKind.Value, ReadQuotedValue());

            int start = _position - 1;
            while (_position < content.Length && !char.IsWhiteSpace(content[_position]) && content[_position] is not '{' and not '}')
            {
                _position++;
            }

            return new Token(TokenKind.Value, content[start.._position]);
        }

        private string ReadQuotedValue()
        {
            var value = new StringBuilder();
            while (_position < content.Length)
            {
                char current = content[_position++];
                if (current == '"')
                {
                    return value.ToString();
                }

                if (current == '\\' && _position < content.Length)
                {
                    char escaped = content[_position++];
                    if (escaped is 'n' or 'r' or 't' or '\\' or '"')
                    {
                        value.Append(escaped switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            _ => escaped
                        });
                    }
                    else
                    {
                        // Preserve unknown escapes instead of silently dropping
                        // a backslash from paths or application-defined values.
                        value.Append('\\').Append(escaped);
                    }
                }
                else
                {
                    value.Append(current);
                }
            }

            throw Error("Unterminated quoted value");
        }

        private void SkipTrivia()
        {
            while (_position < content.Length)
            {
                if (char.IsWhiteSpace(content[_position]))
                {
                    _position++;
                    continue;
                }

                if (_position + 1 < content.Length && content[_position] == '/' && content[_position + 1] == '/')
                {
                    _position += 2;
                    while (_position < content.Length && content[_position] != '\n')
                    {
                        _position++;
                    }
                    continue;
                }

                break;
            }
        }

        private FormatException Error(string message) => new($"Invalid VDF data at offset {_position}: {message}.");
    }
}
