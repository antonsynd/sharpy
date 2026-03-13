using Sharpy.Compiler.Discovery;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

public class XmlDocReaderTests : IDisposable
{
    private readonly string _tempDir;

    public XmlDocReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"XmlDocReaderTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteTempXml(string content)
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, content);
        return path;
    }

    private const string SampleXml = """
        <?xml version="1.0"?>
        <doc>
            <assembly>
                <name>Sharpy.Core</name>
            </assembly>
            <members>
                <member name="M:Sharpy.Builtins.Print(System.Object[])">
                    <summary>Prints items to standard output.</summary>
                    <param name="items">The items to print.</param>
                    <param name="sep">Separator between items.</param>
                    <returns>Nothing.</returns>
                    <example>print("hello", "world")</example>
                </member>
                <member name="T:Sharpy.List`1">
                    <summary>A generic list type.</summary>
                </member>
                <member name="P:Sharpy.List`1.Count">
                    <summary>Gets the number of elements.</summary>
                </member>
                <member name="M:Sharpy.Builtins.Len(Sharpy.ISized)">
                    <summary>Returns the length of an object.</summary>
                    <param name="obj">The object to measure.</param>
                </member>
                <member name="M:Sharpy.Builtins.NoDoc">
                </member>
                <member name="M:Sharpy.Builtins.XmlEntities">
                    <summary>Returns a &lt;list&gt; of &amp;items&gt;.</summary>
                </member>
                <member name="M:Sharpy.Builtins.InlineTag">
                    <summary>See <see cref="M:Sharpy.Builtins.Print(System.Object[])"/> for details.</summary>
                </member>
                <member name="M:Sharpy.Builtins.MultiLine">
                    <summary>
                        This is a
                        multi-line summary
                        with extra   spaces.
                    </summary>
                </member>
            </members>
        </doc>
        """;

    [Fact]
    public void TryCreate_MissingFile_ReturnsNull()
    {
        var reader = XmlDocReader.TryCreate("/nonexistent/path/doc.xml");
        Assert.Null(reader);
    }

    [Fact]
    public void TryCreate_ValidFile_ReturnsReader()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path);
        Assert.NotNull(reader);
    }

    [Fact]
    public void TryCreate_MalformedXml_ReturnsNull()
    {
        var path = WriteTempXml("<doc><members><broken");
        var reader = XmlDocReader.TryCreate(path);
        Assert.Null(reader);
    }

    [Fact]
    public void GetMemberDoc_ExtractsSummary()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.Print(System.Object[])");
        Assert.NotNull(doc);
        Assert.Equal("Prints items to standard output.", doc.Summary);
    }

    [Fact]
    public void GetMemberDoc_ExtractsParameters()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.Print(System.Object[])")!;
        Assert.Equal(2, doc.Parameters.Count);
        Assert.Equal("The items to print.", doc.Parameters["items"]);
        Assert.Equal("Separator between items.", doc.Parameters["sep"]);
    }

    [Fact]
    public void GetMemberDoc_ExtractsReturns()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.Print(System.Object[])")!;
        Assert.Equal("Nothing.", doc.Returns);
    }

    [Fact]
    public void GetMemberDoc_ExtractsExample()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.Print(System.Object[])")!;
        Assert.Equal("print(\"hello\", \"world\")", doc.Example);
    }

    [Fact]
    public void GetMemberDoc_GenericType_BacktickNotation()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("T:Sharpy.List`1");
        Assert.NotNull(doc);
        Assert.Equal("A generic list type.", doc.Summary);
    }

    [Fact]
    public void GetMemberDoc_Property()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("P:Sharpy.List`1.Count");
        Assert.NotNull(doc);
        Assert.Equal("Gets the number of elements.", doc.Summary);
    }

    [Fact]
    public void GetMemberDoc_NotFound_ReturnsNull()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.DoesNotExist");
        Assert.Null(doc);
    }

    [Fact]
    public void GetMemberDoc_EmptyMember_ReturnsNull()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.NoDoc");
        Assert.Null(doc);
    }

    [Fact]
    public void GetMemberDoc_XmlEntities_HandledCorrectly()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.XmlEntities")!;
        Assert.Equal("Returns a <list> of &items>.", doc.Summary);
    }

    [Fact]
    public void GetMemberDoc_InlineTags_StrippedToText()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.InlineTag")!;
        Assert.Contains("for details.", doc.Summary!);
    }

    [Fact]
    public void GetMemberDoc_MultiLineWhitespace_Normalized()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.MultiLine")!;
        Assert.Equal("This is a multi-line summary with extra spaces.", doc.Summary);
    }

    [Fact]
    public void GetMemberDoc_ParameterOnly()
    {
        var path = WriteTempXml(SampleXml);
        var reader = XmlDocReader.TryCreate(path)!;

        var doc = reader.GetMemberDoc("M:Sharpy.Builtins.Len(Sharpy.ISized)")!;
        Assert.Equal("Returns the length of an object.", doc.Summary);
        Assert.Single(doc.Parameters);
        Assert.Equal("The object to measure.", doc.Parameters["obj"]);
        Assert.Null(doc.Returns);
        Assert.Null(doc.Example);
    }
}
