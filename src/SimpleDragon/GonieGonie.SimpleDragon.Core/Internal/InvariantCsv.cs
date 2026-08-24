using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GonieGonie.SimpleDragon.Internal;

internal sealed class CsvDataException : FormatException
{
    public CsvDataException(string source, int rowNumber, string message)
        : base(source + " row " + rowNumber.ToString(CultureInfo.InvariantCulture) + ": " + message)
    {
        SourceName = source;
        RowNumber = rowNumber;
    }

    public string SourceName { get; }

    public int RowNumber { get; }
}

internal sealed class CsvDocument
{
    private CsvDocument(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<CsvRow> Rows { get; }

    public static CsvDocument ReadEmbedded(string path, bool stripHeaderUnits = false)
    {
        using Stream stream = SimpleDragonEmbeddedData.OpenRead(path);
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);
        return Parse(reader.ReadToEnd(), path, stripHeaderUnits);
    }

    public static CsvDocument Parse(string text, string source = "<text>", bool stripHeaderUnits = false)
    {
        DomainSupport.NotNull(text, nameof(text));
        var records = ParseRecords(text, source);
        if (records.Count == 0)
        {
            throw new CsvDataException(source, 1, "The CSV has no header row.");
        }

        string[] parsedHeaders = records[0]
            .Select(value => NormalizeHeader(value, stripHeaderUnits))
            .ToArray();
        if (parsedHeaders.Length > 0)
        {
            parsedHeaders[0] = parsedHeaders[0].TrimStart('\ufeff');
        }

        int columnCount = parsedHeaders.Length;
        while (columnCount > 0 && parsedHeaders[columnCount - 1].Length == 0)
        {
            bool trailingColumnIsEmpty = records
                .Skip(1)
                .All(record => record.Length > columnCount - 1
                    && string.IsNullOrWhiteSpace(record[columnCount - 1]));
            if (!trailingColumnIsEmpty)
            {
                break;
            }

            columnCount--;
        }

        string[] headers = parsedHeaders.Take(columnCount).ToArray();

        var knownHeaders = new HashSet<string>(StringComparer.Ordinal);
        foreach (string header in headers)
        {
            if (header.Length == 0)
            {
                throw new CsvDataException(source, 1, "A column name is empty.");
            }

            if (!knownHeaders.Add(header))
            {
                throw new CsvDataException(source, 1, "Duplicate column name '" + header + "'.");
            }
        }

        var rows = new List<CsvRow>(Math.Max(0, records.Count - 1));
        for (int index = 1; index < records.Count; index++)
        {
            string[] values = records[index];
            if (values.Length == 1 && values[0].Length == 0)
            {
                continue;
            }

            bool hasOnlyIgnoredTrailingValues = values.Length >= headers.Length
                && values.Skip(headers.Length).All(string.IsNullOrWhiteSpace);
            if (values.Length < headers.Length || !hasOnlyIgnoredTrailingValues)
            {
                throw new CsvDataException(
                    source,
                    index + 1,
                    "Expected " + headers.Length.ToString(CultureInfo.InvariantCulture)
                    + " columns but found " + values.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            rows.Add(new CsvRow(source, index + 1, headers, values.Take(headers.Length).ToArray()));
        }

        return new CsvDocument(Array.AsReadOnly(headers), rows.AsReadOnly());
    }

    private static string NormalizeHeader(string value, bool stripHeaderUnits)
    {
        string header = value.Trim();
        return stripHeaderUnits
            ? Regex.Replace(header, @"\s*\[[^\]]+\]\s*$", string.Empty, RegexOptions.CultureInvariant)
            : header;
    }

    private static List<string[]> ParseRecords(string text, string source)
    {
        var records = new List<string[]>();
        var record = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool quoteClosed = false;
        int physicalLine = 1;

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (quoted)
            {
                if (current == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        quoteClosed = true;
                    }
                }
                else
                {
                    field.Append(current);
                    if (current == '\n')
                    {
                        physicalLine++;
                    }
                }

                continue;
            }

            if (quoteClosed && current != ',' && current != '\r' && current != '\n')
            {
                throw new CsvDataException(source, physicalLine, "Unexpected character after a closing quote.");
            }

            if (current == '"')
            {
                if (field.Length != 0)
                {
                    throw new CsvDataException(source, physicalLine, "A quote must begin at the start of a field.");
                }

                quoted = true;
                quoteClosed = false;
            }
            else if (current == ',')
            {
                record.Add(field.ToString());
                field.Clear();
                quoteClosed = false;
            }
            else if (current == '\r' || current == '\n')
            {
                if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                record.Add(field.ToString());
                field.Clear();
                records.Add(record.ToArray());
                record.Clear();
                quoteClosed = false;
                physicalLine++;
            }
            else
            {
                field.Append(current);
            }
        }

        if (quoted)
        {
            throw new CsvDataException(source, physicalLine, "An escaped field is not closed.");
        }

        if (field.Length > 0 || record.Count > 0 || quoteClosed)
        {
            record.Add(field.ToString());
            records.Add(record.ToArray());
        }

        return records;
    }
}

internal sealed class CsvRow
{
    private readonly Dictionary<string, string> _values;

    public CsvRow(string source, int rowNumber, IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        Source = source;
        RowNumber = rowNumber;
        var fields = new Dictionary<string, string>(headers.Count, StringComparer.Ordinal);
        for (int index = 0; index < headers.Count; index++)
        {
            fields.Add(headers[index], values[index].Trim());
        }

        _values = fields;
    }

    public string Source { get; }

    public int RowNumber { get; }

    public string Required(string column)
    {
        if (!_values.TryGetValue(column, out string? value))
        {
            throw Error("Required column '" + column + "' is missing.");
        }

        if (value.Length == 0)
        {
            throw Error("Required value in column '" + column + "' is empty.");
        }

        return value;
    }

    public string Optional(string column)
    {
        if (!_values.TryGetValue(column, out string? value))
        {
            throw Error("Required column '" + column + "' is missing.");
        }

        return value;
    }

    public int Integer(string column)
    {
        string value = Required(column);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            throw Error("Value '" + value + "' in column '" + column + "' is not an invariant integer.");
        }

        return result;
    }

    public double Number(string column)
    {
        string value = Required(column);
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            || double.IsNaN(result)
            || double.IsInfinity(result))
        {
            throw Error("Value '" + value + "' in column '" + column + "' is not a finite invariant number.");
        }

        return result;
    }

    public bool ZeroOne(string column)
    {
        int value = Integer(column);
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw Error("Value in column '" + column + "' must be 0 or 1."),
        };
    }

    public CsvDataException Error(string message)
    {
        return new CsvDataException(Source, RowNumber, message);
    }
}
