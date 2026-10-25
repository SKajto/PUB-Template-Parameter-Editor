using System.IO.Compression;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Infrastructure;

/// <summary>
/// Reads template files from disk strictly read-only: detects format from bytes,
/// extracts the template XML (container adapter when needed), parses declarations
/// and records baseline fingerprints. No read path ever writes a template.
/// </summary>
public sealed class TemplateReaderService
{
    private static readonly string[] SupportedExtensions = [".dta", ".xml"];

    public static bool IsTemplateFile(string path) =>
        SupportedExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    public TemplateRecord Load(string path, CancellationToken token)
    {
        var record = new TemplateRecord { FilePath = path };

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            record.BaselineFingerprint = HashService.Sha256Hex(bytes);
            record.Format = TemplateFormatDetector.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 64)));

            switch (record.Format)
            {
                case TemplateFormat.DtaZipContainer:
                    using (var ms = new MemoryStream(bytes, writable: false))
                    using (var container = new DtaContainer(ms, ZipArchiveMode.Read, leaveOpen: false))
                    {
                        var xml = container.ReadTemplateXml();
                        if (xml is null)
                        {
                            record.ReadState = TemplateReadState.Error;
                            record.ReadError = "The .dta container does not contain a 'template.xml' entry; unsupported container layout.";
                            return record;
                        }

                        record.XmlContent = xml;
                    }

                    break;

                case TemplateFormat.PlainXmlTemplate:
                    var source = SourceText.FromBytes(bytes);
                    record.XmlContent = new TemplateXmlContent
                    {
                        EntryName = string.Empty,
                        EntryBytes = bytes,
                        Source = source,
                        EntryFingerprint = record.BaselineFingerprint,
                    };
                    break;

                default:
                    record.ReadState = TemplateReadState.Error;
                    record.ReadError = "Unrecognized file bytes; the file is not a supported template and stays read-only.";
                    return record;
            }

            token.ThrowIfCancellationRequested();

            var parsed = XmlDeclarationParser.Parse(record.XmlContent.Source);
            if (!parsed.Success)
            {
                record.ReadState = TemplateReadState.Error;
                record.ReadError = string.Join(" ", parsed.Errors);
                return record;
            }

            record.Declarations = parsed.Declarations;
            record.Warnings = parsed.Warnings;
            record.ReadState = TemplateReadState.Loaded;
            return record;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            record.ReadState = TemplateReadState.Error;
            record.ReadError = ex.Message;
            return record;
        }
    }
}
