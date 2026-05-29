using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class UrllibTests
{
    // ==================== urlparse ====================

    [Fact]
    public void Urlparse_FullUrl_ParsesAllComponents()
    {
        var result = Sharpy.UrllibModule.Urlparse("https://example.com:8080/path?q=1#frag");
        result.Scheme.Should().Be("https");
        result.Netloc.Should().Be("example.com:8080");
        result.Path.Should().Be("/path");
        result.Params.Should().Be("");
        result.Query.Should().Be("q=1");
        result.Fragment.Should().Be("frag");
    }

    [Fact]
    public void Urlparse_WithParams_ExtractsParams()
    {
        var result = Sharpy.UrllibModule.Urlparse("http://example.com/path;type=a?q=1");
        result.Path.Should().Be("/path");
        result.Params.Should().Be("type=a");
        result.Query.Should().Be("q=1");
    }

    [Fact]
    public void Urlparse_NoScheme_UsesDefault()
    {
        var result = Sharpy.UrllibModule.Urlparse("//example.com/path", "https");
        result.Scheme.Should().Be("https");
        result.Netloc.Should().Be("example.com");
        result.Path.Should().Be("/path");
    }

    [Fact]
    public void Urlparse_EmptyUrl_ReturnsEmptyComponents()
    {
        var result = Sharpy.UrllibModule.Urlparse("");
        result.Scheme.Should().Be("");
        result.Netloc.Should().Be("");
        result.Path.Should().Be("");
        result.Query.Should().Be("");
        result.Fragment.Should().Be("");
    }

    [Fact]
    public void Urlparse_Hostname_ReturnsLowercased()
    {
        var result = Sharpy.UrllibModule.Urlparse("https://Example.COM:8080/path");
        result.Hostname.Should().Be("example.com");
        result.Port.Should().Be(8080);
    }

    [Fact]
    public void Urlparse_WithUserInfo_ExtractsHostname()
    {
        var result = Sharpy.UrllibModule.Urlparse("https://user" + "@" + "example.com/path");
        result.Hostname.Should().Be("example.com");
        result.Port.Should().BeNull();
    }

    // ==================== urlsplit ====================

    [Fact]
    public void Urlsplit_FullUrl_ParsesFiveComponents()
    {
        var result = Sharpy.UrllibModule.Urlsplit("https://example.com:8080/path?q=1#frag");
        result.Scheme.Should().Be("https");
        result.Netloc.Should().Be("example.com:8080");
        result.Path.Should().Be("/path");
        result.Query.Should().Be("q=1");
        result.Fragment.Should().Be("frag");
    }

    [Fact]
    public void Urlsplit_NoFragment_WhenDisallowed()
    {
        var result = Sharpy.UrllibModule.Urlsplit("http://example.com/path#frag", allowFragments: false);
        result.Path.Should().Be("/path#frag");
        result.Fragment.Should().Be("");
    }

    // ==================== urlunsplit / urlunparse roundtrip ====================

    [Fact]
    public void Urlsplit_Urlunsplit_Roundtrip()
    {
        string url = "https://example.com:8080/path?q=1#frag";
        var split = Sharpy.UrllibModule.Urlsplit(url);
        var reconstructed = Sharpy.UrllibModule.Urlunsplit(split);
        reconstructed.Should().Be(url);
    }

    [Fact]
    public void Urlparse_Geturl_Roundtrip()
    {
        string url = "https://example.com/path?q=1#frag";
        var parsed = Sharpy.UrllibModule.Urlparse(url);
        parsed.Geturl().Should().Be(url);
    }

    // ==================== urljoin ====================

    [Fact]
    public void Urljoin_RelativePath_ResolvesCorrectly()
    {
        var result = Sharpy.UrllibModule.Urljoin("https://example.com/a/b", "c/d");
        result.Should().Be("https://example.com/a/c/d");
    }

    [Fact]
    public void Urljoin_AbsolutePath_ReplacesPath()
    {
        var result = Sharpy.UrllibModule.Urljoin("https://example.com/a/b", "/c/d");
        result.Should().Be("https://example.com/c/d");
    }

    [Fact]
    public void Urljoin_EmptyRelative_ReturnsBase()
    {
        var result = Sharpy.UrllibModule.Urljoin("https://example.com/a/b", "");
        result.Should().Be("https://example.com/a/b");
    }

    [Fact]
    public void Urljoin_DotSegments_Resolved()
    {
        var result = Sharpy.UrllibModule.Urljoin("https://example.com/a/b/c", "../d");
        result.Should().Be("https://example.com/a/d");
    }

    [Fact]
    public void Urljoin_WithNetloc_InheritsScheme()
    {
        var result = Sharpy.UrllibModule.Urljoin("https://example.com/a", "//other.com/b");
        result.Should().Be("https://other.com/b");
    }

    // ==================== parse_qs / parse_qsl ====================

    [Fact]
    public void ParseQs_BasicQueryString()
    {
        var result = Sharpy.UrllibModule.ParseQs("a=1&b=2&b=3");
        result["a"][0].Should().Be("1");
        result["b"][0].Should().Be("2");
        result["b"][1].Should().Be("3");
    }

    [Fact]
    public void ParseQsl_BasicQueryString()
    {
        var result = Sharpy.UrllibModule.ParseQsl("a=1&b=2");
        result[0].Should().Be(("a", "1"));
        result[1].Should().Be(("b", "2"));
    }

    [Fact]
    public void ParseQs_EncodedValues()
    {
        var result = Sharpy.UrllibModule.ParseQs("key=hello+world&other=%2Fpath");
        result["key"][0].Should().Be("hello world");
        result["other"][0].Should().Be("/path");
    }

    [Fact]
    public void ParseQs_EmptyString_ReturnsEmptyDict()
    {
        var result = Sharpy.UrllibModule.ParseQs("");
        result.Count.Should().Be(0);
    }

    // ==================== urlencode ====================

    [Fact]
    public void Urlencode_DictInput()
    {
        var d = new Sharpy.Dict<string, object?>();
        d["a"] = "1";
        d["b"] = "2";
        var result = Sharpy.UrllibModule.Urlencode(d);
        result.Should().Contain("a=1");
        result.Should().Contain("b=2");
    }

    [Fact]
    public void Urlencode_EncodesSpecialChars()
    {
        var d = new Sharpy.Dict<string, object?>();
        d["key"] = "hello world";
        var result = Sharpy.UrllibModule.Urlencode(d);
        result.Should().Be("key=hello+world");
    }

    [Fact]
    public void Urlencode_ListInput()
    {
        var pairs = new Sharpy.List<(string, string)>();
        pairs.Add(("a", "1"));
        pairs.Add(("b", "2"));
        var result = Sharpy.UrllibModule.Urlencode(pairs);
        result.Should().Be("a=1&b=2");
    }

    // ==================== quote / unquote ====================

    [Fact]
    public void Quote_EncodesSpecialChars()
    {
        var result = Sharpy.UrllibModule.Quote("/path with spaces");
        result.Should().Be("/path%20with%20spaces");
    }

    [Fact]
    public void Quote_SafeSlash_PreservesSlash()
    {
        var result = Sharpy.UrllibModule.Quote("/a/b c");
        result.Should().Be("/a/b%20c");
    }

    [Fact]
    public void Quote_NoSafe_EncodesSlash()
    {
        var result = Sharpy.UrllibModule.Quote("/a/b", "");
        result.Should().Be("%2Fa%2Fb");
    }

    [Fact]
    public void Unquote_DecodesPercent()
    {
        var result = Sharpy.UrllibModule.Unquote("%2Fpath%20with%20spaces");
        result.Should().Be("/path with spaces");
    }

    [Fact]
    public void Unquote_NoPercentEncoding_ReturnsOriginal()
    {
        var result = Sharpy.UrllibModule.Unquote("hello");
        result.Should().Be("hello");
    }

    // ==================== quote_plus / unquote_plus ====================

    [Fact]
    public void QuotePlus_SpaceBecomesPlus()
    {
        var result = Sharpy.UrllibModule.QuotePlus("a b+c");
        result.Should().Be("a+b%2Bc");
    }

    [Fact]
    public void UnquotePlus_PlusBecomesSpace()
    {
        var result = Sharpy.UrllibModule.UnquotePlus("a+b%2Bc");
        result.Should().Be("a b+c");
    }

    // ==================== Unicode handling ====================

    [Fact]
    public void Quote_UnicodeChars_Encoded()
    {
        var result = Sharpy.UrllibModule.Quote("café", "");
        result.Should().Be("caf%C3%A9");
    }

    [Fact]
    public void Unquote_UnicodeChars_Decoded()
    {
        var result = Sharpy.UrllibModule.Unquote("caf%C3%A9");
        result.Should().Be("café");
    }

    // ==================== Edge cases ====================

    [Fact]
    public void Urlparse_UnusualScheme()
    {
        var result = Sharpy.UrllibModule.Urlparse("ftp://files.example.com/pub/");
        result.Scheme.Should().Be("ftp");
        result.Netloc.Should().Be("files.example.com");
        result.Path.Should().Be("/pub/");
    }

    [Fact]
    public void Urlparse_FileScheme()
    {
        var result = Sharpy.UrllibModule.Urlparse("file:///tmp/test.txt");
        result.Scheme.Should().Be("file");
        result.Netloc.Should().Be("");
        result.Path.Should().Be("/tmp/test.txt");
    }

    [Fact]
    public void Urlparse_NullUrl_ThrowsTypeError()
    {
        var act = () => Sharpy.UrllibModule.Urlparse(null!);
        act.Should().Throw<Sharpy.TypeError>();
    }

    [Fact]
    public void ParseResult_Equality()
    {
        var a = new Sharpy.ParseResult("https", "example.com", "/path", "", "q=1", "frag");
        var b = new Sharpy.ParseResult("https", "example.com", "/path", "", "q=1", "frag");
        a.Should().Be(b);
    }

    [Fact]
    public void SplitResult_Equality()
    {
        var a = new Sharpy.SplitResult("https", "example.com", "/path", "q=1", "frag");
        var b = new Sharpy.SplitResult("https", "example.com", "/path", "q=1", "frag");
        a.Should().Be(b);
    }
}
