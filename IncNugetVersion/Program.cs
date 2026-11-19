using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace IncNugetVersion;

internal class Program
{
    private static int Main(string[] args)
    {
        var progname = Path.GetFileNameWithoutExtension(Environment.ProcessPath);

        if (args.Length == 0)
        {
            Console.Error.WriteLine($"Usage: {progname} <file.nuspec> [<file.nuspec>...]");
            return 2;
        }

        var failed = false;
        foreach (var filename in args)
        {
            try
            {
                var (oldVersion, newVersion) = IncrementVersion(filename);
                Console.WriteLine($"{filename}: {oldVersion} -> {newVersion}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{progname} Error: {filename}: {ex.Message}");
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    /// <summary>
    /// Increments the last numeric portion of package/metadata/version in a nuspec file
    /// and writes the file back. A prerelease or build suffix is kept, so 1.2.3-beta
    /// becomes 1.2.4-beta.
    /// </summary>
    private static (string OldVersion, string NewVersion) IncrementVersion(string filename)
    {
        var document = XDocument.Load(filename);

        // Match by local name: nuspec files usually put everything in a schema namespace.
        var versionElement = document.Root?
            .Elements().FirstOrDefault(e => e.Name.LocalName == "metadata")?
            .Elements().FirstOrDefault(e => e.Name.LocalName == "version")
            ?? throw new InvalidDataException("no <package>/<metadata>/<version> element found.");

        var oldVersion = versionElement.Value.Trim();
        versionElement.Value = IncrementLastPart(oldVersion);
        SaveDocument(filename, document);
        return (oldVersion, versionElement.Value);
    }

    private static string IncrementLastPart(string version)
    {
        var suffixStart = version.IndexOfAny(['-', '+']);
        var numbers = suffixStart < 0 ? version : version[..suffixStart];
        var suffix = suffixStart < 0 ? "" : version[suffixStart..];

        var lastDot = numbers.LastIndexOf('.');
        var last = numbers[(lastDot + 1)..];
        if (!int.TryParse(last, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value == int.MaxValue)
        {
            throw new InvalidDataException($"cannot increment version '{version}'.");
        }

        return $"{numbers[..(lastDot + 1)]}{value + 1}{suffix}";
    }

    private static void SaveDocument(string filename, XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "  ",
            NewLineChars = Environment.NewLine
        };

        using var writer = new StreamWriter(filename, false, settings.Encoding);

        using (var xmlWriter = XmlWriter.Create(writer, settings))
            document.Save(xmlWriter);

        writer.Write(Environment.NewLine);
    }
}