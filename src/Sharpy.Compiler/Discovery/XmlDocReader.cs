using System.Xml.Linq;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Parsed documentation for a single XML doc member.
/// </summary>
internal sealed record XmlMemberDoc(
    string? Summary,
    Dictionary<string, string> Parameters,
    string? Returns,
    string? Example);

/// <summary>
/// Reads .NET XML documentation files and extracts member documentation.
/// </summary>
internal sealed class XmlDocReader
{
    private readonly XDocument _document;

    private XmlDocReader(XDocument document)
    {
        _document = document;
    }

    /// <summary>
    /// Try to create a reader from an XML documentation file path.
    /// Returns null if the file doesn't exist or is malformed.
    /// </summary>
    public static XmlDocReader? TryCreate(string xmlFilePath)
    {
        if (!File.Exists(xmlFilePath))
            return null;

        try
        {
            var document = XDocument.Load(xmlFilePath);
            return new XmlDocReader(document);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Look up documentation for a member by its XML doc member ID.
    /// Member ID format examples:
    ///   M:Sharpy.Builtins.Print(System.Object[]) for methods
    ///   T:Sharpy.List`1 for types
    ///   P:Sharpy.List`1.Count for properties
    /// Returns null if the member is not found.
    /// </summary>
    public XmlMemberDoc? GetMemberDoc(string memberName)
    {
        var members = _document.Root?.Element("members");
        if (members is null)
            return null;

        var memberElement = members
            .Elements("member")
            .FirstOrDefault(e => e.Attribute("name")?.Value == memberName);

        if (memberElement is null)
            return null;

        var summary = GetInnerText(memberElement.Element("summary"));

        var parameters = new Dictionary<string, string>();
        foreach (var param in memberElement.Elements("param"))
        {
            var name = param.Attribute("name")?.Value;
            if (name is not null)
            {
                var text = GetInnerText(param);
                if (text is not null)
                    parameters[name] = text;
            }
        }

        var returns = GetInnerText(memberElement.Element("returns"));
        var example = GetInnerText(memberElement.Element("example"));

        // Return null if there's no meaningful documentation
        if (summary is null && parameters.Count == 0 && returns is null && example is null)
            return null;

        return new XmlMemberDoc(summary, parameters, returns, example);
    }

    /// <summary>
    /// Extract the inner text of an XML element, stripping all nested XML tags.
    /// Returns null if the element is null or contains only whitespace.
    /// </summary>
    private static string? GetInnerText(XElement? element)
    {
        if (element is null)
            return null;

        // Get concatenated text content, stripping XML tags
        var text = string.Concat(element.Nodes().Select(n => n switch
        {
            XText t => t.Value,
            XElement e => e.Value,
            _ => string.Empty
        }));

        // Normalize whitespace: collapse runs of whitespace into single spaces, then trim
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
