// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using html = global::Sharpy.Html;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.HTML.HtmlModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class HTML
    {
        [global::Sharpy.SharpyModule("html.html_module_tests")]
        public static partial class HtmlModuleTests
        {
            public class TestParser : global::Sharpy.HTMLParser
            {
                public Sharpy.List<string> Events;
                public override void HandleStarttag(string tag, Sharpy.List<global::System.ValueTuple<string, string?>> attrs)
#line 14 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (15, 9) - (15, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    string attrStr = ", ".Join(_ToStringList(attrs));
#line (16, 9) - (16, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"starttag:{(tag)} [{(attrStr)}]"));
                }

                public override void HandleEndtag(string tag)
#line 19 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (20, 9) - (20, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"endtag:{(tag)}"));
                }

                public override void HandleStartendtag(string tag, Sharpy.List<global::System.ValueTuple<string, string?>> attrs)
#line 23 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (24, 9) - (24, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    string attrStr = ", ".Join(_ToStringList(attrs));
#line (25, 9) - (25, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"startendtag:{(tag)} [{(attrStr)}]"));
                }

                public override void HandleData(string data)
#line 28 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (29, 9) - (29, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"data:{(data)}"));
                }

                public override void HandleComment(string data)
#line 32 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (33, 9) - (33, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"comment:{(data)}"));
                }

                public override void HandleEntityref(string name)
#line 36 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (37, 9) - (37, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"entityref:{(name)}"));
                }

                public override void HandleCharref(string name)
#line 40 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (41, 9) - (41, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"charref:{(name)}"));
                }

                public override void HandleDecl(string decl)
#line 44 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (45, 9) - (45, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"decl:{(decl)}"));
                }

                public override void HandlePi(string data)
#line 48 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (49, 9) - (49, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"pi:{(data)}"));
                }

                public TestParser(bool convertCharrefs = true) : base(convertCharrefs: convertCharrefs)
#line 9 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (11, 9) - (11, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events = new Sharpy.List<string>()
                    {
                    };
                }
            }

            public class DefaultHandlerParser : global::Sharpy.HTMLParser
            {
                public Sharpy.List<string> Events;
                public override void HandleStarttag(string tag, Sharpy.List<global::System.ValueTuple<string, string?>> attrs)
#line 60 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (61, 9) - (61, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"starttag:{(tag)}"));
                }

                public override void HandleEndtag(string tag)
#line 64 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (65, 9) - (65, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"endtag:{(tag)}"));
                }

                public override void HandleData(string data)
#line 68 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (69, 9) - (69, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events.Append(FormattableString.Invariant($"data:{(data)}"));
                }

                public DefaultHandlerParser(bool convertCharrefs = true) : base(convertCharrefs: convertCharrefs)
#line 55 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                {
#line (57, 9) - (57, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    this.Events = new Sharpy.List<string>()
                    {
                    };
                }
            }

            internal static Sharpy.List<string> _ToStringList(Sharpy.List<global::System.ValueTuple<string, string?>> attrs)
            {
#line (73, 5) - (73, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Sharpy.List<string> result = new Sharpy.List<string>()
                {
                };
#line (74, 5) - (79, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                foreach (var __loopVar_0 in attrs)
                {
                    var attr = __loopVar_0;
#line (75, 9) - (79, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    if (attr.Item2 == null)
                    {
#line (76, 13) - (76, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                        result.Append(FormattableString.Invariant($"({(attr.Item1)}, null)"));
                    }
                    else
                    {
#line (78, 13) - (78, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                        result.Append(FormattableString.Invariant($"({(attr.Item1)}, {(attr.Item2)})"));
                    }
                }

#line (79, 5) - (79, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                return result;
            }
        }
    }

    public static partial class HTML
    {
        public partial class HtmlModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestEscapeScriptTag()
            {
#line (86, 5) - (86, 96) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt;", html.Escape("<script>alert(1)</script>"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeDoubleQuotesDefault()
            {
#line (90, 5) - (90, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("&quot;hello&quot;", html.Escape("\"hello\""));
            }

            [Xunit.FactAttribute]
            public void TestEscapeDoubleQuotesQuoteFalse()
            {
#line (94, 5) - (94, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("\"hello\"", html.Escape("\"hello\"", quote: false));
            }

            [Xunit.FactAttribute]
            public void TestEscapeSingleQuotesDefault()
            {
#line (98, 5) - (98, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("it&#x27;s", html.Escape("it's"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeSingleQuotesQuoteFalse()
            {
#line (102, 5) - (102, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("it's", html.Escape("it's", quote: false));
            }

            [Xunit.FactAttribute]
            public void TestEscapeAmpersand()
            {
#line (106, 5) - (106, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("a &amp; b", html.Escape("a & b"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeEmptyString()
            {
#line (110, 5) - (110, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("", html.Escape(""));
            }

            [Xunit.FactAttribute]
            public void TestEscapeNoSpecialChars()
            {
#line (114, 5) - (114, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("hello world", html.Escape("hello world"));
            }

            [Xunit.FactAttribute]
            public void TestEscapeAllSpecialChars()
            {
#line (118, 5) - (118, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("&lt;&gt;&amp;&quot;&#x27;", html.Escape("<>&\"'"));
            }

            [Xunit.FactAttribute]
            public void TestUnescapeNamedEntities()
            {
#line (124, 5) - (124, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("<b>", html.Unescape("&lt;b&gt;"));
            }

            [Xunit.FactAttribute]
            public void TestUnescapeDecimalCharRef()
            {
#line (128, 5) - (128, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("<", html.Unescape("&#60;"));
            }

            [Xunit.FactAttribute]
            public void TestUnescapeHexCharRef()
            {
#line (132, 5) - (132, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("<", html.Unescape("&#x3c;"));
            }

            [Xunit.FactAttribute]
            public void TestUnescapeAmpersandEntity()
            {
#line (136, 5) - (136, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("&", html.Unescape("&amp;"));
            }

            [Xunit.FactAttribute]
            public void TestUnescapeEmptyString()
            {
#line (140, 5) - (140, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("", html.Unescape(""));
            }

            [Xunit.FactAttribute]
            public void TestUnescapeNoEntities()
            {
#line (144, 5) - (144, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("hello world", html.Unescape("hello world"));
            }

            [Xunit.FactAttribute]
            public void TestRoundtripEscapeUnescape()
            {
#line (148, 5) - (148, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                string original = "<div class=\"test\">it's & cool</div>";
#line (149, 5) - (149, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                string escaped = html.Escape(original);
#line (150, 5) - (150, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                string unescaped = html.Unescape(escaped);
#line (151, 5) - (151, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(original, unescaped);
            }

            [Xunit.FactAttribute]
            public void TestParserStartTagSimple()
            {
#line (157, 5) - (157, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (158, 5) - (158, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<div>");
#line (159, 5) - (159, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (160, 5) - (160, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:div []", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserStartTagWithAttributes()
            {
#line (164, 5) - (164, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (165, 5) - (165, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<p class=\"main\">");
#line (166, 5) - (166, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (167, 5) - (167, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:p [(class, main)]", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserStartTagMultipleAttributes()
            {
#line (171, 5) - (171, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (172, 5) - (172, 52) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<input disabled type=text name=\"foo\">");
#line (173, 5) - (173, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (174, 5) - (174, 90) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:input [(disabled, null), (type, text), (name, foo)]", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserStartTagSingleQuotedAttr()
            {
#line (178, 5) - (178, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (179, 5) - (179, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<div class='main'>");
#line (180, 5) - (180, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (181, 5) - (181, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:div [(class, main)]", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserEndTag()
            {
#line (185, 5) - (185, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (186, 5) - (186, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("</div>");
#line (187, 5) - (187, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (188, 5) - (188, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("endtag:div", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserSelfClosingSlash()
            {
#line (192, 5) - (192, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (193, 5) - (193, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<br/>");
#line (194, 5) - (194, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (195, 5) - (195, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("startendtag:br []", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserSelfClosingSpaceSlash()
            {
#line (199, 5) - (199, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (200, 5) - (200, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<br />");
#line (201, 5) - (201, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (202, 5) - (202, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("startendtag:br []", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserBareBrNotSelfClosing()
            {
#line (206, 5) - (206, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (207, 5) - (207, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<br>");
#line (208, 5) - (208, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (209, 5) - (209, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:br []", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserDefaultStartendtagCallsStartAndEnd()
            {
#line (214, 5) - (214, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new DefaultHandlerParser();
#line (215, 5) - (215, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<br/>");
#line (216, 5) - (216, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(p.Events));
#line (217, 5) - (217, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:br", p.Events[0]);
#line (218, 5) - (218, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("endtag:br", p.Events[1]);
            }

            [Xunit.FactAttribute]
            public void TestParserDataSimpleText()
            {
#line (222, 5) - (222, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (223, 5) - (223, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("hello world");
#line (224, 5) - (224, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (225, 5) - (225, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (226, 5) - (226, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("data:hello world", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserComment()
            {
#line (230, 5) - (230, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (231, 5) - (231, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<!-- comment -->");
#line (232, 5) - (232, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (233, 5) - (233, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("comment: comment ", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserDoctype()
            {
#line (237, 5) - (237, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (238, 5) - (238, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<!DOCTYPE html>");
#line (239, 5) - (239, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (240, 5) - (240, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("decl:DOCTYPE html", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserProcessingInstruction()
            {
#line (244, 5) - (244, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (245, 5) - (245, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<?xml version=\"1.0\"?>");
#line (246, 5) - (246, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (247, 5) - (247, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("pi:xml version=\"1.0\"", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserEntityRefConvertCharrefsOff()
            {
#line (253, 5) - (253, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser(false);
#line (254, 5) - (254, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("&amp;");
#line (255, 5) - (255, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (256, 5) - (256, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (257, 5) - (257, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("entityref:amp", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserCharRefDecimalConvertCharrefsOff()
            {
#line (261, 5) - (261, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser(false);
#line (262, 5) - (262, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("&#60;");
#line (263, 5) - (263, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (264, 5) - (264, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (265, 5) - (265, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("charref:60", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserCharRefHexConvertCharrefsOff()
            {
#line (269, 5) - (269, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser(false);
#line (270, 5) - (270, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("&#x3c;");
#line (271, 5) - (271, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (272, 5) - (272, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (273, 5) - (273, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("charref:x3c", p.Events[0]);
            }

            [Xunit.FactAttribute]
            public void TestParserConvertCharrefsDefault()
            {
#line (277, 5) - (277, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (278, 5) - (278, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("&amp; &#60; &#x3c;");
#line (279, 5) - (279, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (281, 5) - (281, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                string combined = "";
#line (282, 5) - (285, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                foreach (var __loopVar_1 in p.Events)
                {
                    var e = __loopVar_1;
#line (283, 9) - (285, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    if (e.Startswith("data:"))
                    {
#line (284, 13) - (284, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                        combined = combined + global::Sharpy.Slice.GetSlice(e, 5, null, null);
                    }
                }

#line (285, 5) - (285, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("& < <", combined);
            }

            [Xunit.FactAttribute]
            public void TestParserEntityRefMixedWithTextConvertCharrefsOff()
            {
#line (291, 5) - (291, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser(false);
#line (292, 5) - (292, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("&amp; &#60; &#x3c;");
#line (293, 5) - (293, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (294, 5) - (294, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(5, global::Sharpy.Builtins.Len(p.Events));
#line (295, 5) - (295, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("entityref:amp", p.Events[0]);
#line (296, 5) - (296, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("data: ", p.Events[1]);
#line (297, 5) - (297, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("charref:60", p.Events[2]);
#line (298, 5) - (298, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("data: ", p.Events[3]);
#line (299, 5) - (299, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("charref:x3c", p.Events[4]);
            }

            [Xunit.FactAttribute]
            public void TestParserScriptContent()
            {
#line (303, 5) - (303, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (304, 5) - (304, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<script>var x = 1 < 2;</script>");
#line (305, 5) - (305, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(p.Events));
#line (306, 5) - (306, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:script []", p.Events[0]);
#line (307, 5) - (307, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("data:var x = 1 < 2;", p.Events[1]);
#line (308, 5) - (308, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("endtag:script", p.Events[2]);
            }

            [Xunit.FactAttribute]
            public void TestParserStyleContent()
            {
#line (312, 5) - (312, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (313, 5) - (313, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<style>h1 > h2 { color: red; }</style>");
#line (314, 5) - (314, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(p.Events));
#line (315, 5) - (315, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:style []", p.Events[0]);
#line (316, 5) - (316, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("data:h1 > h2 { color: red; }", p.Events[1]);
#line (317, 5) - (317, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("endtag:style", p.Events[2]);
            }

            [Xunit.FactAttribute]
            public void TestParserTagsAreLowercased()
            {
#line (321, 5) - (321, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (322, 5) - (322, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<DIV CLASS=\"test\"></DIV>");
#line (323, 5) - (323, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(p.Events));
#line (324, 5) - (324, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:div [(class, test)]", p.Events[0]);
#line (325, 5) - (325, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("endtag:div", p.Events[1]);
            }

            [Xunit.FactAttribute]
            public void TestParserMultipleFeedsChunked()
            {
#line (329, 5) - (329, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (330, 5) - (330, 18) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<di");
#line (331, 5) - (331, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("v>");
#line (332, 5) - (332, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("hello");
#line (333, 5) - (333, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("</div>");
#line (334, 5) - (334, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (335, 5) - (335, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(p.Events));
#line (336, 5) - (336, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("starttag:div []", p.Events[0]);
#line (337, 5) - (337, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("data:hello", p.Events[1]);
#line (338, 5) - (338, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("endtag:div", p.Events[2]);
            }

            [Xunit.FactAttribute]
            public void TestParserCompleteDocument()
            {
#line (342, 5) - (342, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (343, 5) - (343, 100) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<!DOCTYPE html><html><head><title>Test</title></head><body><p>Hello</p></body></html>");
#line (344, 5) - (344, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("decl:DOCTYPE html", p.Events);
#line (345, 5) - (345, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("starttag:html []", p.Events);
#line (346, 5) - (346, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("starttag:head []", p.Events);
#line (347, 5) - (347, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("starttag:title []", p.Events);
#line (348, 5) - (348, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("data:Test", p.Events);
#line (349, 5) - (349, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("endtag:title", p.Events);
#line (350, 5) - (350, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("endtag:head", p.Events);
#line (351, 5) - (351, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("starttag:body []", p.Events);
#line (352, 5) - (352, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("starttag:p []", p.Events);
#line (353, 5) - (353, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("data:Hello", p.Events);
#line (354, 5) - (354, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("endtag:p", p.Events);
#line (355, 5) - (355, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("endtag:body", p.Events);
#line (356, 5) - (356, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("endtag:html", p.Events);
            }

            [Xunit.FactAttribute]
            public void TestParserGetposTracking()
            {
#line (360, 5) - (360, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (361, 5) - (361, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<div>\nline2</div>");
#line (362, 5) - (362, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                global::System.ValueTuple<int, int> pos = p.Getpos();
#line (364, 5) - (364, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.True(pos.Item1 > 0);
            }

            [Xunit.FactAttribute]
            public void TestParserReset()
            {
#line (368, 5) - (368, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (369, 5) - (369, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<div>");
#line (370, 5) - (370, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(p.Events));
#line (371, 5) - (371, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Reset();
#line (373, 5) - (373, 39) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                global::System.ValueTuple<int, int> pos = p.Getpos();
#line (374, 5) - (374, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(1, pos.Item1);
#line (375, 5) - (375, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(0, pos.Item2);
#line (377, 5) - (377, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<span>");
#line (378, 5) - (378, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(p.Events));
            }

            [Xunit.FactAttribute]
            public void TestParserMalformedBareAngleBracket()
            {
#line (382, 5) - (382, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (383, 5) - (383, 20) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("a < b");
#line (384, 5) - (384, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Close();
#line (386, 5) - (386, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                string combined = "";
#line (387, 5) - (390, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                foreach (var __loopVar_2 in p.Events)
                {
                    var e = __loopVar_2;
#line (388, 9) - (390, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                    if (e.Startswith("data:"))
                    {
#line (389, 13) - (389, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                        combined = combined + global::Sharpy.Slice.GetSlice(e, 5, null, null);
                    }
                }

#line (390, 5) - (390, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("a", combined);
#line (391, 5) - (391, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Contains("b", combined);
            }

            [Xunit.FactAttribute]
            public void TestParserGetStarttagTextReturnsLastStartTag()
            {
#line (395, 5) - (395, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (396, 5) - (396, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                p.Feed("<div class=\"main\">");
#line (397, 5) - (397, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Equal("<div class=\"main\">", p.GetStarttagText());
            }

            [Xunit.FactAttribute]
            public void TestParserGetStarttagTextNullBeforeAnyTag()
            {
#line (401, 5) - (401, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                var p = new TestParser();
#line (402, 5) - (402, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/html/html_module_tests.spy"
                Xunit.Assert.Null(p.GetStarttagText());
            }
        }
    }
}
