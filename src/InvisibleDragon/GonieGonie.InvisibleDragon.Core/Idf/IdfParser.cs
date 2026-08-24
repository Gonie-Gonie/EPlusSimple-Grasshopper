using System.Text;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Idf;

/// <summary>
/// Parses IDF delimiters, quoted values, comments, and arbitrary object order.
/// </summary>
public static class IdfParser
{
    public static IdfDocument ParseFile(string path, IddSchema? schema = null, Encoding? encoding = null)
    {
        Guard.NotNull(path, nameof(path));
        return Parse(File.ReadAllText(path, encoding ?? new UTF8Encoding(false)), schema);
    }

    public static IdfDocument Parse(string text, IddSchema? schema = null)
    {
        Guard.NotNull(text, nameof(text));
        var objects = new List<IdfObject>();
        var preamble = new List<string>();
        var pendingComments = new List<string>();
        var tokens = new List<Token>();
        var value = new StringBuilder();
        IdfObject? lastCompletedObject = null;
        IdfField? lastCommittedField = null;
        Token? lastCommittedToken = null;
        bool inQuotes = false;

        using var reader = new StringReader(text);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            bool delimiterSeenOnLine = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    value.Append(character);
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        value.Append(line[++index]);
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (!inQuotes && character == '!')
                {
                    string comment = line.Substring(index + 1).Trim();
                    if (delimiterSeenOnLine && string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        if (lastCommittedField is not null)
                        {
                            lastCommittedField.InlineComment = comment;
                        }
                        else if (lastCompletedObject is not null)
                        {
                            lastCompletedObject.HeaderComment = comment;
                        }
                        else if (lastCommittedToken is not null)
                        {
                            lastCommittedToken.InlineComment = comment;
                        }
                    }
                    else
                    {
                        pendingComments.Add(comment);
                    }

                    break;
                }

                if (!inQuotes && (character == ',' || character == ';'))
                {
                    var token = new Token(value.ToString().Trim(), pendingComments);
                    value.Clear();
                    pendingComments.Clear();
                    tokens.Add(token);
                    lastCommittedToken = token;
                    delimiterSeenOnLine = true;

                    if (character == ';')
                    {
                        lastCompletedObject = BuildObject(tokens, schema, objects.Count == 0 ? preamble : null);
                        objects.Add(lastCompletedObject);
                        lastCommittedField = lastCompletedObject.Fields.Count == 0
                            ? null
                            : lastCompletedObject.Fields[lastCompletedObject.Fields.Count - 1];
                        tokens.Clear();
                    }
                    else
                    {
                        lastCommittedField = null;
                        lastCompletedObject = null;
                    }

                    continue;
                }

                value.Append(character);
            }

            if (inQuotes)
            {
                value.Append('\n');
            }
            else if (!string.IsNullOrWhiteSpace(value.ToString()))
            {
                value.Append(' ');
            }
        }

        if (inQuotes)
        {
            throw new FormatException($"Unterminated quoted IDF value at line {lineNumber}.");
        }

        if (tokens.Count > 0 || !string.IsNullOrWhiteSpace(value.ToString()))
        {
            throw new FormatException("The IDF text ends before an object semicolon.");
        }

        return new IdfDocument(schema, objects, preamble, pendingComments);
    }

    private static IdfObject BuildObject(List<Token> tokens, IddSchema? schema, List<string>? preamble)
    {
        if (tokens.Count == 0 || string.IsNullOrWhiteSpace(tokens[0].Value))
        {
            throw new FormatException("An IDF object has no object type.");
        }

        Token header = tokens[0];
        IReadOnlyList<string> leading;
        if (preamble is not null)
        {
            preamble.AddRange(header.LeadingComments);
            leading = Array.AsReadOnly(Array.Empty<string>());
        }
        else
        {
            leading = header.LeadingComments;
        }

        IddObjectDefinition? definition = null;
        schema?.TryGetObject(header.Value, out definition);
        var fields = tokens.Skip(1)
            .Select(token => new IdfField(token.Value, token.LeadingComments, token.InlineComment))
            .ToArray();
        return new IdfObject(header.Value, fields, definition, leading, header.InlineComment);
    }

    private sealed class Token
    {
        public Token(string value, IEnumerable<string> leadingComments)
        {
            Value = value;
            LeadingComments = leadingComments.ToArray();
        }

        public string Value { get; }

        public IReadOnlyList<string> LeadingComments { get; }

        public string? InlineComment { get; set; }
    }
}
