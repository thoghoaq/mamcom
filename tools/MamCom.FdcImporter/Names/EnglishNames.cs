using System.Text;

namespace MamCom.FdcImporter.Names;

/// <summary>
/// <c>data/plan/ingredient-names-en.csv</c>: the English text to search FDC with for each ingredient id. These are
/// search words, not data: a person picks the result in Studio. Columns <c>id</c> and <c>english</c>; a field may be
/// quoted with <c>"</c> (doubled inside) when it contains a comma.
/// </summary>
public static class EnglishNames
{
    public const string RelativePath = "plan/ingredient-names-en.csv";

    /// <exception cref="FormatException">The header is not <c>id,english</c>, a row has the wrong number of fields, a field is empty, or an id repeats. The message names the line.</exception>
    public static IReadOnlyDictionary<string, string> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var lineNumber = 0;
        var sawHeader = false;

        foreach (var rawLine in csv.TrimStart((char)0xFEFF).Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var fields = SplitFields(line, lineNumber);
            if (!sawHeader)
            {
                if (fields.Count != 2 || fields[0] != "id" || fields[1] != "english")
                {
                    throw new FormatException($"line {lineNumber}: the header must be \"id,english\".");
                }

                sawHeader = true;
                continue;
            }

            if (fields.Count != 2)
            {
                throw new FormatException($"line {lineNumber}: expected 2 fields (id, english), found {fields.Count}.");
            }

            var id = fields[0].Trim();
            var english = fields[1].Trim();
            if (id.Length == 0 || english.Length == 0)
            {
                throw new FormatException($"line {lineNumber}: id and english must not be empty.");
            }

            if (!names.TryAdd(id, english))
            {
                throw new FormatException($"line {lineNumber}: the id '{id}' appears twice.");
            }
        }

        if (!sawHeader)
        {
            throw new FormatException("the file is empty; it needs the header \"id,english\".");
        }

        return names;
    }

    private static List<string> SplitFields(string line, int lineNumber)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (inQuotes)
        {
            throw new FormatException($"line {lineNumber}: a quoted field is not closed.");
        }

        fields.Add(field.ToString());
        return fields;
    }
}
