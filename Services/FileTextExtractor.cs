using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using System.Text;

public class FileTextExtractor
{
    public string ExtractText(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();

        return extension switch
        {
            ".pdf" => ExtractFromPdf(filePath),
            ".docx" => ExtractFromDocx(filePath),
            _ => ""
        };
    }

    private string ExtractFromPdf(string path)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(path);
        foreach (var page in document.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private string ExtractFromDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        return doc.MainDocumentPart?.Document.Body?.InnerText ?? "";
    }
}
