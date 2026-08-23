using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PersonnelPlatform.Application.Integration;

public static class SpreadsheetImportReader
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxRows = 10_000;
    public const int MaxColumns = 100;
    private const long MaxExpandedBytes = 50L * 1024 * 1024;

    public static SpreadsheetData ReadXlsx(string fileName, ReadOnlyMemory<byte> content)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Yalnız .xlsx dosyaları desteklenir.");
        if (content.Length == 0 || content.Length > MaxFileBytes)
            throw new InvalidDataException($"Excel dosyası 1 byte ile {MaxFileBytes / 1024 / 1024} MB arasında olmalıdır.");

        using var stream = new MemoryStream(content.ToArray(), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        if (archive.Entries.Count == 0) throw new InvalidDataException("XLSX arşivi boş.");
        if (archive.Entries.Sum(x => x.Length) > MaxExpandedBytes) throw new InvalidDataException("XLSX açılmış içerik boyutu izin verilen sınırı aşıyor.");

        var sharedStrings = ReadSharedStrings(archive);
        var sheet = archive.Entries
            .Where(x => x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) && x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? throw new InvalidDataException("XLSX içinde worksheet bulunamadı.");

        using var sheetStream = sheet.Open();
        var document = LoadSafe(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var xmlRows = document.Descendants(ns + "row").ToArray();
        if (xmlRows.Length == 0) throw new InvalidDataException("Excel sayfası boş.");

        List<string>? headers = null;
        var rows = new List<SpreadsheetDataRow>();
        var headerRowNumber = 0;

        foreach (var xmlRow in xmlRows)
        {
            var rowNumber = ParseRowNumber(xmlRow, headerRowNumber + rows.Count + 1);
            var cells = ReadCells(xmlRow, ns, sharedStrings);
            if (cells.Count == 0 || cells.Values.All(string.IsNullOrWhiteSpace)) continue;

            if (headers is null)
            {
                var maxIndex = cells.Keys.Max();
                if (maxIndex >= MaxColumns) throw new InvalidDataException($"Excel en fazla {MaxColumns} kolon içerebilir.");
                headers = Enumerable.Range(0, maxIndex + 1)
                    .Select(i => cells.TryGetValue(i, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : $"COLUMN_{i + 1}")
                    .ToList();
                var duplicates = headers.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
                if (duplicates.Length > 0) throw new InvalidDataException($"Tekrarlanan kolon başlığı: {string.Join(", ", duplicates)}");
                headerRowNumber = rowNumber;
                continue;
            }

            if (rows.Count >= MaxRows) throw new InvalidDataException($"Excel en fazla {MaxRows} veri satırı içerebilir.");
            if (cells.Keys.Any(x => x >= headers.Count)) throw new InvalidDataException("Veri satırında başlık satırından daha fazla kolon bulunuyor.");
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++) values[headers[i]] = cells.TryGetValue(i, out var value) ? value.Trim() : string.Empty;
            if (values.Values.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(new SpreadsheetDataRow(rowNumber, values));
        }

        if (headers is null || headers.Count == 0) throw new InvalidDataException("Excel başlık satırı bulunamadı.");
        if (rows.Count == 0) throw new InvalidDataException("Excel içinde veri satırı bulunamadı.");
        return new SpreadsheetData(headers, rows);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return Array.Empty<string>();
        using var stream = entry.Open();
        var document = LoadSafe(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value)))
            .ToArray();
    }

    private static Dictionary<int, string> ReadCells(XElement row, XNamespace ns, IReadOnlyList<string> sharedStrings)
    {
        var result = new Dictionary<int, string>();
        var sequentialIndex = 0;
        foreach (var cell in row.Elements(ns + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            var index = string.IsNullOrWhiteSpace(reference) ? sequentialIndex : ColumnIndex(reference);
            if (index < 0 || index >= MaxColumns) throw new InvalidDataException($"Excel en fazla {MaxColumns} kolon içerebilir.");
            sequentialIndex = index + 1;
            var type = cell.Attribute("t")?.Value;
            string value;
            if (type == "inlineStr") value = string.Concat(cell.Descendants(ns + "t").Select(x => x.Value));
            else
            {
                var raw = cell.Element(ns + "v")?.Value ?? string.Empty;
                if (type == "s" && int.TryParse(raw, out var sharedIndex))
                    value = sharedIndex >= 0 && sharedIndex < sharedStrings.Count ? sharedStrings[sharedIndex] : throw new InvalidDataException("XLSX shared string index geçersiz.");
                else if (type == "b") value = raw == "1" ? "TRUE" : "FALSE";
                else value = raw;
            }
            result[index] = value;
        }
        return result;
    }

    private static int ColumnIndex(string cellReference)
    {
        var value = 0;
        var found = false;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch)) break;
            found = true;
            value = checked(value * 26 + (char.ToUpperInvariant(ch) - 'A' + 1));
        }
        return found ? value - 1 : -1;
    }

    private static int ParseRowNumber(XElement row, int fallback) =>
        int.TryParse(row.Attribute("r")?.Value, out var value) && value > 0 ? value : Math.Max(1, fallback);

    private static XDocument LoadSafe(Stream stream)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxExpandedBytes };
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }
}
