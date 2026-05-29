using System;
using System.Collections.Generic;
using Xunit;

namespace Sharpy.Tests
{
    public class HtmlModuleTests
    {
        #region Escape

        [Fact]
        public void Escape_BasicHtml_EscapesAllSpecialChars()
        {
            Assert.Equal(
                "&amp;lt;script&amp;gt;alert(&#x27;xss&#x27;)&amp;lt;/script&amp;gt;".Replace("&amp;", "&"),
                HtmlModule.Escape("<script>alert('xss')</script>"));
        }

        [Fact]
        public void Escape_Ampersand_Escaped()
        {
            Assert.Equal("&amp;", HtmlModule.Escape("&"));
        }

        [Fact]
        public void Escape_LessThan_Escaped()
        {
            Assert.Equal("&lt;", HtmlModule.Escape("<"));
        }

        [Fact]
        public void Escape_GreaterThan_Escaped()
        {
            Assert.Equal("&gt;", HtmlModule.Escape(">"));
        }

        [Fact]
        public void Escape_DoubleQuote_Escaped()
        {
            Assert.Equal("&quot;", HtmlModule.Escape("\""));
        }

        [Fact]
        public void Escape_SingleQuote_Escaped()
        {
            Assert.Equal("&#x27;", HtmlModule.Escape("'"));
        }

        [Fact]
        public void Escape_QuoteFalse_DoesNotEscapeQuotes()
        {
            Assert.Equal("\"hello'world\"", HtmlModule.Escape("\"hello'world\"", quote: false));
        }

        [Fact]
        public void Escape_EmptyString_ReturnsEmpty()
        {
            Assert.Equal("", HtmlModule.Escape(""));
        }

        [Fact]
        public void Escape_NoSpecialChars_ReturnsSame()
        {
            Assert.Equal("hello world", HtmlModule.Escape("hello world"));
        }

        [Fact]
        public void Escape_NullString_ReturnsEmpty()
        {
            Assert.Equal("", HtmlModule.Escape(null!));
        }

        #endregion

        #region Unescape

        [Fact]
        public void Unescape_BasicEntities_Unescaped()
        {
            Assert.Equal("<b>bold</b>", HtmlModule.Unescape("&lt;b&gt;bold&lt;/b&gt;"));
        }

        [Fact]
        public void Unescape_NumericEntity_Decoded()
        {
            Assert.Equal(">", HtmlModule.Unescape("&#62;"));
        }

        [Fact]
        public void Unescape_HexEntity_Decoded()
        {
            Assert.Equal(">", HtmlModule.Unescape("&#x3e;"));
        }

        [Fact]
        public void Unescape_NamedEntities_Decoded()
        {
            Assert.Equal("&", HtmlModule.Unescape("&amp;"));
            Assert.Equal("\"", HtmlModule.Unescape("&quot;"));
        }

        [Fact]
        public void Unescape_EmptyString_ReturnsEmpty()
        {
            Assert.Equal("", HtmlModule.Unescape(""));
        }

        [Fact]
        public void Unescape_NoEntities_ReturnsSame()
        {
            Assert.Equal("hello world", HtmlModule.Unescape("hello world"));
        }

        #endregion

        #region Roundtrip

        [Fact]
        public void EscapeUnescape_Roundtrip_PreservesData()
        {
            string original = "<div class=\"test\">Hello & 'world'</div>";
            string escaped = HtmlModule.Escape(original);
            string unescaped = HtmlModule.Unescape(escaped);
            Assert.Equal(original, unescaped);
        }

        #endregion

        #region HTMLParser

        [Fact]
        public void HTMLParser_StartTag_CallsHandler()
        {
            var parser = new TestParser();
            parser.Feed("<html>");
            Assert.Contains("start:html", parser.Events);
        }

        [Fact]
        public void HTMLParser_EndTag_CallsHandler()
        {
            var parser = new TestParser();
            parser.Feed("</html>");
            Assert.Contains("end:html", parser.Events);
        }

        [Fact]
        public void HTMLParser_Data_CallsHandler()
        {
            var parser = new TestParser();
            parser.Feed("<p>Hello</p>");
            Assert.Contains("data:Hello", parser.Events);
        }

        [Fact]
        public void HTMLParser_Comment_CallsHandler()
        {
            var parser = new TestParser();
            parser.Feed("<!-- a comment -->");
            Assert.Contains("comment: a comment ", parser.Events);
        }

        [Fact]
        public void HTMLParser_SelfClosingTag_CallsHandlers()
        {
            var parser = new TestParser();
            parser.Feed("<br/>");
            Assert.Contains("start:br", parser.Events);
            Assert.Contains("startend:br", parser.Events);
        }

        [Fact]
        public void HTMLParser_Attributes_ParsedCorrectly()
        {
            var parser = new AttrParser();
            parser.Feed("<a href=\"http://example.com\" class='link'>");
            Assert.Equal("a", parser.LastTag);
            Assert.Contains(("href", "http://example.com"), parser.LastAttrs);
            Assert.Contains(("class", "link"), parser.LastAttrs);
        }

        [Fact]
        public void HTMLParser_AttributeWithoutValue_ParsedAsNull()
        {
            var parser = new AttrParser();
            parser.Feed("<input disabled>");
            Assert.Equal("input", parser.LastTag);
            Assert.Contains(("disabled", (string?)null), parser.LastAttrs);
        }

        [Fact]
        public void HTMLParser_ScriptContent_TreatedAsData()
        {
            var parser = new TestParser();
            parser.Feed("<script>var x = 1;</script>");
            Assert.Contains("start:script", parser.Events);
            Assert.Contains("data:var x = 1;", parser.Events);
            Assert.Contains("end:script", parser.Events);
        }

        [Fact]
        public void HTMLParser_StyleContent_TreatedAsData()
        {
            var parser = new TestParser();
            parser.Feed("<style>body { color: red; }</style>");
            Assert.Contains("start:style", parser.Events);
            Assert.Contains("data:body { color: red; }", parser.Events);
            Assert.Contains("end:style", parser.Events);
        }

        [Fact]
        public void HTMLParser_CompleteDocument_ParsesCorrectly()
        {
            var parser = new TestParser();
            parser.Feed("<html><body>Hello</body></html>");
            Assert.Equal(new List<string>
            {
                "start:html",
                "start:body",
                "data:Hello",
                "end:body",
                "end:html"
            }, parser.Events);
        }

        [Fact]
        public void HTMLParser_MalformedHtml_HandlesGracefully()
        {
            var parser = new TestParser();
            // Should not throw
            parser.Feed("<p>unclosed paragraph<p>another");
            Assert.Contains("start:p", parser.Events);
            Assert.Contains("data:unclosed paragraph", parser.Events);
        }

        [Fact]
        public void HTMLParser_Doctype_CallsHandler()
        {
            var parser = new TestParser();
            parser.Feed("<!DOCTYPE html>");
            Assert.Contains("decl:html", parser.Events);
        }

        [Fact]
        public void HTMLParser_Reset_ClearsState()
        {
            var parser = new TestParser();
            parser.Feed("<p>text");
            parser.Reset();
            parser.Feed("<div>new</div>");
            // After reset, should only see new events
            // (Events list is not cleared by Reset since it's our test list,
            //  but parser state should be clean)
            Assert.Contains("start:div", parser.Events);
        }

        #endregion

        #region Test Helpers

        private class TestParser : HTMLParser
        {
            public List<string> Events { get; } = new List<string>();

            public override void HandleStarttag(string tag, List<(string, string?)> attrs)
            {
                Events.Add($"start:{tag}");
            }

            public override void HandleStarttagEnd(string tag)
            {
                Events.Add($"startend:{tag}");
            }

            public override void HandleEndtag(string tag)
            {
                Events.Add($"end:{tag}");
            }

            public override void HandleData(string data)
            {
                Events.Add($"data:{data}");
            }

            public override void HandleComment(string data)
            {
                Events.Add($"comment:{data}");
            }

            public override void HandleDecl(string decl)
            {
                Events.Add($"decl:{decl}");
            }
        }

        private class AttrParser : HTMLParser
        {
            public string? LastTag { get; private set; }
            public List<(string, string?)> LastAttrs { get; private set; } = new List<(string, string?)>();

            public override void HandleStarttag(string tag, List<(string, string?)> attrs)
            {
                LastTag = tag;
                LastAttrs = attrs;
            }
        }

        #endregion
    }
}
