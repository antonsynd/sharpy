using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HtmlModuleTests
{
    // ---- Escape tests ----

    [Fact]
    public void Escape_ScriptTag()
    {
        Html.Escape("<script>alert(1)</script>")
            .Should().Be("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public void Escape_DoubleQuotes_Default()
    {
        Html.Escape("\"hello\"")
            .Should().Be("&quot;hello&quot;");
    }

    [Fact]
    public void Escape_DoubleQuotes_QuoteFalse()
    {
        Html.Escape("\"hello\"", quote: false)
            .Should().Be("\"hello\"");
    }

    [Fact]
    public void Escape_SingleQuotes_Default()
    {
        Html.Escape("it's")
            .Should().Be("it&#x27;s");
    }

    [Fact]
    public void Escape_SingleQuotes_QuoteFalse()
    {
        Html.Escape("it's", quote: false)
            .Should().Be("it's");
    }

    [Fact]
    public void Escape_Ampersand()
    {
        Html.Escape("a & b")
            .Should().Be("a &amp; b");
    }

    [Fact]
    public void Escape_EmptyString()
    {
        Html.Escape("").Should().Be("");
    }

    [Fact]
    public void Escape_NoSpecialChars()
    {
        Html.Escape("hello world")
            .Should().Be("hello world");
    }

    [Fact]
    public void Escape_AllSpecialChars()
    {
        Html.Escape("<>&\"'")
            .Should().Be("&lt;&gt;&amp;&quot;&#x27;");
    }

    // ---- Unescape tests ----

    [Fact]
    public void Unescape_NamedEntities()
    {
        Html.Unescape("&lt;b&gt;")
            .Should().Be("<b>");
    }

    [Fact]
    public void Unescape_DecimalCharRef()
    {
        Html.Unescape("&#60;")
            .Should().Be("<");
    }

    [Fact]
    public void Unescape_HexCharRef()
    {
        Html.Unescape("&#x3c;")
            .Should().Be("<");
    }

    [Fact]
    public void Unescape_AmpersandEntity()
    {
        Html.Unescape("&amp;")
            .Should().Be("&");
    }

    [Fact]
    public void Unescape_EmptyString()
    {
        Html.Unescape("").Should().Be("");
    }

    [Fact]
    public void Unescape_NoEntities()
    {
        Html.Unescape("hello world")
            .Should().Be("hello world");
    }

    [Fact]
    public void Roundtrip_EscapeUnescape()
    {
        var original = "<div class=\"test\">it's & cool</div>";
        var escaped = Html.Escape(original);
        var unescaped = Html.Unescape(escaped);
        unescaped.Should().Be(original);
    }

    // ---- HTMLParser tests ----

    private class TestParser : HTMLParser
    {
        public System.Collections.Generic.List<string> Events { get; } = new System.Collections.Generic.List<string>();

        public TestParser(bool convertCharrefs = true) : base(convertCharrefs)
        {
        }

        public override void HandleStarttag(string tag, Sharpy.List<(string, string?)> attrs)
        {
            var attrStr = string.Join(", ", ToStringList(attrs));
            Events.Add($"starttag:{tag} [{attrStr}]");
        }

        public override void HandleEndtag(string tag)
        {
            Events.Add($"endtag:{tag}");
        }

        public override void HandleStartendtag(string tag, Sharpy.List<(string, string?)> attrs)
        {
            var attrStr = string.Join(", ", ToStringList(attrs));
            Events.Add($"startendtag:{tag} [{attrStr}]");
        }

        public override void HandleData(string data)
        {
            Events.Add($"data:{data}");
        }

        public override void HandleComment(string data)
        {
            Events.Add($"comment:{data}");
        }

        public override void HandleEntityref(string name)
        {
            Events.Add($"entityref:{name}");
        }

        public override void HandleCharref(string name)
        {
            Events.Add($"charref:{name}");
        }

        public override void HandleDecl(string decl)
        {
            Events.Add($"decl:{decl}");
        }

        public override void HandlePi(string data)
        {
            Events.Add($"pi:{data}");
        }

        private static System.Collections.Generic.List<string> ToStringList(Sharpy.List<(string, string?)> attrs)
        {
            var result = new System.Collections.Generic.List<string>();
            foreach (var (name, value) in attrs)
            {
                if (value == null)
                    result.Add($"({name}, null)");
                else
                    result.Add($"({name}, {value})");
            }
            return result;
        }
    }

    // Use the default HandleStartendtag implementation (calls HandleStarttag + HandleEndtag)
    private class DefaultHandlerParser : HTMLParser
    {
        public System.Collections.Generic.List<string> Events { get; } = new System.Collections.Generic.List<string>();

        public DefaultHandlerParser(bool convertCharrefs = true) : base(convertCharrefs)
        {
        }

        public override void HandleStarttag(string tag, Sharpy.List<(string, string?)> attrs)
        {
            Events.Add($"starttag:{tag}");
        }

        public override void HandleEndtag(string tag)
        {
            Events.Add($"endtag:{tag}");
        }

        public override void HandleData(string data)
        {
            Events.Add($"data:{data}");
        }
    }

    [Fact]
    public void Parser_StartTag_Simple()
    {
        var p = new TestParser();
        p.Feed("<div>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("starttag:div []");
    }

    [Fact]
    public void Parser_StartTag_WithAttributes()
    {
        var p = new TestParser();
        p.Feed("<p class=\"main\">");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("starttag:p [(class, main)]");
    }

    [Fact]
    public void Parser_StartTag_MultipleAttributes()
    {
        var p = new TestParser();
        p.Feed("<input disabled type=text name=\"foo\">");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("starttag:input [(disabled, null), (type, text), (name, foo)]");
    }

    [Fact]
    public void Parser_StartTag_SingleQuotedAttr()
    {
        var p = new TestParser();
        p.Feed("<div class='main'>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("starttag:div [(class, main)]");
    }

    [Fact]
    public void Parser_EndTag()
    {
        var p = new TestParser();
        p.Feed("</div>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("endtag:div");
    }

    [Fact]
    public void Parser_SelfClosing_Slash()
    {
        var p = new TestParser();
        p.Feed("<br/>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("startendtag:br []");
    }

    [Fact]
    public void Parser_SelfClosing_SpaceSlash()
    {
        var p = new TestParser();
        p.Feed("<br />");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("startendtag:br []");
    }

    [Fact]
    public void Parser_BareBr_NotSelfClosing()
    {
        var p = new TestParser();
        p.Feed("<br>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("starttag:br []");
    }

    [Fact]
    public void Parser_DefaultStartendtag_CallsStartAndEnd()
    {
        // Use parser with default HandleStartendtag
        var p = new DefaultHandlerParser();
        p.Feed("<br/>");
        p.Events.Should().HaveCount(2);
        p.Events[0].Should().Be("starttag:br");
        p.Events[1].Should().Be("endtag:br");
    }

    [Fact]
    public void Parser_Data_SimpleText()
    {
        var p = new TestParser();
        p.Feed("hello world");
        p.Close();
        p.Events.Should().ContainSingle()
            .Which.Should().Be("data:hello world");
    }

    [Fact]
    public void Parser_Comment()
    {
        var p = new TestParser();
        p.Feed("<!-- comment -->");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("comment: comment ");
    }

    [Fact]
    public void Parser_Doctype()
    {
        var p = new TestParser();
        p.Feed("<!DOCTYPE html>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("decl:DOCTYPE html");
    }

    [Fact]
    public void Parser_ProcessingInstruction()
    {
        var p = new TestParser();
        p.Feed("<?xml version=\"1.0\"?>");
        p.Events.Should().ContainSingle()
            .Which.Should().Be("pi:xml version=\"1.0\"");
    }

    [Fact]
    public void Parser_EntityRef_ConvertCharrefsOff()
    {
        var p = new TestParser(convertCharrefs: false);
        p.Feed("&amp;");
        p.Close();
        p.Events.Should().ContainSingle()
            .Which.Should().Be("entityref:amp");
    }

    [Fact]
    public void Parser_CharRef_Decimal_ConvertCharrefsOff()
    {
        var p = new TestParser(convertCharrefs: false);
        p.Feed("&#60;");
        p.Close();
        p.Events.Should().ContainSingle()
            .Which.Should().Be("charref:60");
    }

    [Fact]
    public void Parser_CharRef_Hex_ConvertCharrefsOff()
    {
        var p = new TestParser(convertCharrefs: false);
        p.Feed("&#x3c;");
        p.Close();
        p.Events.Should().ContainSingle()
            .Which.Should().Be("charref:x3c");
    }

    [Fact]
    public void Parser_ConvertCharrefs_Default()
    {
        var p = new TestParser();
        p.Feed("&amp; &#60; &#x3c;");
        p.Close();
        // All entities should be converted and delivered as data
        string combined = string.Join("", p.Events.FindAll(e => e.StartsWith("data:"))
            .ConvertAll(e => e.Substring(5)));
        combined.Should().Be("& < <");
    }

    [Fact]
    public void Parser_EntityRef_MixedWithText_ConvertCharrefsOff()
    {
        var p = new TestParser(convertCharrefs: false);
        p.Feed("&amp; &#60; &#x3c;");
        p.Close();
        p.Events.Should().HaveCount(5);
        p.Events[0].Should().Be("entityref:amp");
        p.Events[1].Should().Be("data: ");
        p.Events[2].Should().Be("charref:60");
        p.Events[3].Should().Be("data: ");
        p.Events[4].Should().Be("charref:x3c");
    }

    [Fact]
    public void Parser_ScriptContent()
    {
        var p = new TestParser();
        p.Feed("<script>var x = 1 < 2;</script>");
        p.Events.Should().HaveCount(3);
        p.Events[0].Should().Be("starttag:script []");
        p.Events[1].Should().Be("data:var x = 1 < 2;");
        p.Events[2].Should().Be("endtag:script");
    }

    [Fact]
    public void Parser_StyleContent()
    {
        var p = new TestParser();
        p.Feed("<style>h1 > h2 { color: red; }</style>");
        p.Events.Should().HaveCount(3);
        p.Events[0].Should().Be("starttag:style []");
        p.Events[1].Should().Be("data:h1 > h2 { color: red; }");
        p.Events[2].Should().Be("endtag:style");
    }

    [Fact]
    public void Parser_TagsAreLowercased()
    {
        var p = new TestParser();
        p.Feed("<DIV CLASS=\"test\"></DIV>");
        p.Events.Should().HaveCount(2);
        p.Events[0].Should().Be("starttag:div [(class, test)]");
        p.Events[1].Should().Be("endtag:div");
    }

    [Fact]
    public void Parser_MultipleFeedsChunked()
    {
        var p = new TestParser();
        p.Feed("<di");
        p.Feed("v>");
        p.Feed("hello");
        p.Feed("</div>");
        p.Close();
        p.Events.Should().HaveCount(3);
        p.Events[0].Should().Be("starttag:div []");
        p.Events[1].Should().Be("data:hello");
        p.Events[2].Should().Be("endtag:div");
    }

    [Fact]
    public void Parser_CompleteDocument()
    {
        var p = new TestParser();
        p.Feed("<!DOCTYPE html><html><head><title>Test</title></head><body><p>Hello</p></body></html>");
        p.Events.Should().Contain("decl:DOCTYPE html");
        p.Events.Should().Contain("starttag:html []");
        p.Events.Should().Contain("starttag:head []");
        p.Events.Should().Contain("starttag:title []");
        p.Events.Should().Contain("data:Test");
        p.Events.Should().Contain("endtag:title");
        p.Events.Should().Contain("endtag:head");
        p.Events.Should().Contain("starttag:body []");
        p.Events.Should().Contain("starttag:p []");
        p.Events.Should().Contain("data:Hello");
        p.Events.Should().Contain("endtag:p");
        p.Events.Should().Contain("endtag:body");
        p.Events.Should().Contain("endtag:html");
    }

    [Fact]
    public void Parser_GetposTracking()
    {
        var p = new TestParser();
        p.Feed("<div>\nline2</div>");
        var pos = p.Getpos();
        // After parsing the whole string, position should advance
        pos.Item1.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Parser_Reset()
    {
        var p = new TestParser();
        p.Feed("<div>");
        p.Events.Should().HaveCount(1);
        p.Reset();
        // After reset, position is back to (1, 0)
        var pos = p.Getpos();
        pos.Item1.Should().Be(1);
        pos.Item2.Should().Be(0);
        // Can still parse after reset
        p.Feed("<span>");
        p.Events.Should().HaveCount(2);
    }

    [Fact]
    public void Parser_MalformedBareAngleBracket()
    {
        var p = new TestParser();
        p.Feed("a < b");
        p.Close();
        // The bare '<' should be delivered as data somehow
        string combined = string.Join("", p.Events.FindAll(e => e.StartsWith("data:"))
            .ConvertAll(e => e.Substring(5)));
        combined.Should().Contain("a");
        combined.Should().Contain("b");
    }

    [Fact]
    public void Parser_GetStarttagText_ReturnsLastStartTag()
    {
        var p = new TestParser();
        p.Feed("<div class=\"main\">");
        p.GetStarttagText().Should().Be("<div class=\"main\">");
    }

    [Fact]
    public void Parser_GetStarttagText_NullBeforeAnyTag()
    {
        var p = new TestParser();
        p.GetStarttagText().Should().BeNull();
    }
}
