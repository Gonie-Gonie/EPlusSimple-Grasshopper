using System.Text;
using GonieGonie.InvisibleDragon.Idd;
using GonieGonie.InvisibleDragon.Internal;

namespace GonieGonie.InvisibleDragon.Idf;

public sealed class IdfWriterOptions
{
    public string NewLine { get; set; } = "\n";

    public string Indent { get; set; } = "  ";

    public bool IncludeSchemaFieldComments { get; set; } = true;

    public bool SeparateObjectsWithBlankLine { get; set; } = true;
}

/// <summary>
/// Writes a stable IDF representation using invariant field order and line endings.
/// </summary>
public static class IdfWriter
{
    public static void WriteFile(
        string path,
        IdfDocument document,
        IdfWriterOptions? options = null,
        Encoding? encoding = null)
    {
        Guard.NotNull(path, nameof(path));
        File.WriteAllText(path, Write(document, options), encoding ?? new UTF8Encoding(false));
    }

    public static string Write(IdfDocument document, IdfWriterOptions? options = null)
    {
        Guard.NotNull(document, nameof(document));
        options ??= new IdfWriterOptions();
        ValidateOptions(options);
        var output = new StringBuilder();

        WriteComments(output, document.PreambleComments, string.Empty, options.NewLine);
        if (document.PreambleComments.Count > 0 && document.Count > 0)
        {
            output.Append(options.NewLine);
        }

        for (int objectIndex = 0; objectIndex < document.Count; objectIndex++)
        {
            IdfObject item = document[objectIndex];
            WriteComments(output, item.LeadingComments, string.Empty, options.NewLine);
            output.Append(item.ObjectType);
            output.Append(item.Fields.Count == 0 ? ';' : ',');
            AppendInline(output, item.HeaderComment);
            output.Append(options.NewLine);

            for (int fieldIndex = 0; fieldIndex < item.Fields.Count; fieldIndex++)
            {
                IdfField field = item.Fields[fieldIndex];
                WriteComments(output, field.LeadingComments, options.Indent, options.NewLine);
                output.Append(options.Indent);
                output.Append(field.Value.Trim());
                output.Append(fieldIndex == item.Fields.Count - 1 ? ';' : ',');

                string? comment = field.InlineComment;
                if (comment is null && options.IncludeSchemaFieldComments)
                {
                    IddFieldDefinition? definition = item.Definition?.ResolveField(fieldIndex);
                    comment = definition is null ? null : "- " + definition.Name;
                }

                AppendInline(output, comment);
                output.Append(options.NewLine);
            }

            if (options.SeparateObjectsWithBlankLine && objectIndex < document.Count - 1)
            {
                output.Append(options.NewLine);
            }
        }

        if (document.TrailingComments.Count > 0)
        {
            if (document.Count > 0 && options.SeparateObjectsWithBlankLine)
            {
                output.Append(options.NewLine);
            }

            WriteComments(output, document.TrailingComments, string.Empty, options.NewLine);
        }

        return output.ToString();
    }

    private static void ValidateOptions(IdfWriterOptions options)
    {
        if (options.NewLine != "\n" && options.NewLine != "\r\n")
        {
            throw new ArgumentException("IDF new lines must be LF or CRLF.", nameof(options));
        }

        if (options.Indent.Any(character => character != ' ' && character != '\t'))
        {
            throw new ArgumentException("IDF indentation may contain only spaces and tabs.", nameof(options));
        }
    }

    private static void WriteComments(StringBuilder output, IEnumerable<string> comments, string indent, string newLine)
    {
        foreach (string comment in comments)
        {
            output.Append(indent);
            output.Append('!');
            output.Append(comment);
            output.Append(newLine);
        }
    }

    private static void AppendInline(StringBuilder output, string? comment)
    {
        if (!string.IsNullOrWhiteSpace(comment))
        {
            output.Append("  !");
            output.Append(IdfField.NormalizeComment(comment));
        }
    }
}
