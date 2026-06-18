// Generated from src/Sharpy.Stdlib.Tests/Spy — do not edit directly.
// To regenerate: bash build_tools/regenerate_spy_tests.sh
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Sharpy.Stdlib.Tests.Spy;
using static global::Sharpy.Unittest;
using os = global::Sharpy.OsModule;
using tempfile = global::Sharpy.TempfileModule;
using xml = global::Sharpy.Xml;
using Xunit;
using static Sharpy.Stdlib.Tests.Spy.XML.XmlModuleTests;

namespace Sharpy.Stdlib.Tests.Spy
{
    public static partial class XML
    {
        [global::Sharpy.SharpyModule("xml.xml_module_tests")]
        public static partial class XmlModuleTests
        {
        }
    }

    public static partial class XML
    {
        public partial class XmlModuleTestsTests
        {
            [Xunit.FactAttribute]
            public void TestFromstringSimpleElementReturnsParsedElement()
            {
#line (37, 5) - (37, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<root/>");
#line (38, 5) - (38, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("root", el.Tag);
            }

            [Xunit.FactAttribute]
            public void TestFromstringElementWithTextReturnsTextContent()
            {
#line (42, 5) - (42, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<msg>hello</msg>");
#line (43, 5) - (43, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("msg", el.Tag);
#line (44, 5) - (44, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("hello", el.Text);
            }

            [Xunit.FactAttribute]
            public void TestFromstringElementWithAttributesReturnsAttributes()
            {
#line (48, 5) - (48, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<item id=\"1\" name=\"test\"/>");
#line (49, 5) - (49, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("item", el.Tag);
#line (50, 5) - (50, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("1", el.Get("id"));
#line (51, 5) - (51, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("test", el.Get("name"));
            }

            [Xunit.FactAttribute]
            public void TestFromstringElementWithNamespaceFormatsTagCorrectly()
            {
#line (55, 5) - (55, 70) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<ns:root xmlns:ns=\"http://example.com\"/>");
#line (56, 5) - (56, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("{http://example.com}root", el.Tag);
            }

            [Xunit.FactAttribute]
            public void TestFromstringInvalidXmlThrowsParseError()
            {
#line (60, 5) - (63, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Throws<ParseError>((global::System.Action)(() =>
                {
#line (61, 9) - (61, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    xml.Fromstring("<unclosed>");
                }));
            }

            [Xunit.FactAttribute]
            public void TestFromstringElementWithChildrenIteratesChildren()
            {
#line (65, 5) - (65, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><c/></root>");
#line (66, 5) - (66, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<string> tags = new Sharpy.List<string>()
                {
                };
#line (67, 5) - (69, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                foreach (var __loopVar_0 in root)
                {
                    var child = __loopVar_0;
#line (68, 9) - (68, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    tags.Append(child.Tag);
                }

#line (69, 5) - (69, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(tags));
#line (70, 5) - (70, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("a", tags[0]);
#line (71, 5) - (71, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("b", tags[1]);
#line (72, 5) - (72, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("c", tags[2]);
            }

            [Xunit.FactAttribute]
            public void TestElementConstructorCreatesElementWithTag()
            {
#line (78, 5) - (78, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("div");
#line (79, 5) - (79, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("div", el.Tag);
#line (80, 5) - (80, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Null(el.Text);
            }

            [Xunit.FactAttribute]
            public void TestElementConstructorWithAttribSetsAttributes()
            {
#line (84, 5) - (84, 65) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.Dict<string, string> attrib = new Sharpy.Dict<string, string>()
                {
                    {
                        "class",
                        "main"
                    },
                    {
                        "id",
                        "content"
                    }
                };
#line (85, 5) - (85, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("div", attrib);
#line (86, 5) - (86, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("main", el.Get("class"));
#line (87, 5) - (87, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("content", el.Get("id"));
            }

            [Xunit.FactAttribute]
            public void TestCreateElementFunctionCreatesElement()
            {
#line (91, 5) - (91, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.CreateElement("span");
#line (92, 5) - (92, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("span", el.Tag);
            }

            [Xunit.FactAttribute]
            public void TestSubElementAppendsChildToParent()
            {
#line (96, 5) - (96, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var parent = new global::Sharpy.Element("root");
#line (97, 5) - (97, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var child = xml.SubElement(parent, "child");
#line (98, 5) - (98, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("child", child.Tag);
#line (99, 5) - (99, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, parent.Len());
#line (100, 5) - (100, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("child", parent[0].Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementIndexingPositiveIndexReturnsChild()
            {
#line (106, 5) - (106, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><c/></root>");
#line (107, 5) - (107, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("a", root[0].Tag);
#line (108, 5) - (108, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("b", root[1].Tag);
#line (109, 5) - (109, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("c", root[2].Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementIndexingNegativeIndexReturnsFromEnd()
            {
#line (113, 5) - (113, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><c/></root>");
#line (114, 5) - (114, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("c", root[-1].Tag);
#line (115, 5) - (115, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("b", root[-2].Tag);
#line (116, 5) - (116, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("a", root[-3].Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementIndexingOutOfRangeThrowsIndexError()
            {
#line (120, 5) - (120, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/></root>");
#line (121, 5) - (124, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Throws<IndexError>((global::System.Action)(() =>
                {
#line (122, 9) - (122, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    _ = root[5];
                }));
            }

            [Xunit.FactAttribute]
            public void TestElementLenReturnsChildCount()
            {
#line (126, 5) - (126, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/></root>");
#line (127, 5) - (127, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, root.Len());
            }

            [Xunit.FactAttribute]
            public void TestElementLenEmptyReturnsZero()
            {
#line (131, 5) - (131, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("empty");
#line (132, 5) - (132, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(0, root.Len());
            }

            [Xunit.FactAttribute]
            public void TestElementIsizedCountMatchesLen()
            {
#line (136, 5) - (136, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><c/></root>");
#line (137, 5) - (137, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(root));
            }

            [Xunit.FactAttribute]
            public void TestElementTextReadReturnsTextContent()
            {
#line (143, 5) - (143, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<p>Hello world</p>");
#line (144, 5) - (144, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("Hello world", el.Text);
            }

            [Xunit.FactAttribute]
            public void TestElementTextWriteSetsTextContent()
            {
#line (148, 5) - (148, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("p");
#line (149, 5) - (149, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                el.Text = "New text";
#line (150, 5) - (150, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("New text", el.Text);
            }

            [Xunit.FactAttribute]
            public void TestElementTextNullWhenNoText()
            {
#line (154, 5) - (154, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("br");
#line (155, 5) - (155, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Null(el.Text);
            }

            [Xunit.FactAttribute]
            public void TestElementTailReadWrite()
            {
#line (159, 5) - (159, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/>tail text<b/></root>");
#line (160, 5) - (160, 16) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var a = root[0];
#line (161, 5) - (161, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("tail text", a.Tail);
            }

            [Xunit.FactAttribute]
            public void TestElementGetExistingAttributeReturnsValue()
            {
#line (167, 5) - (167, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<div class=\"main\"/>");
#line (168, 5) - (168, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("main", el.Get("class"));
            }

            [Xunit.FactAttribute]
            public void TestElementGetMissingAttributeReturnsDefault()
            {
#line (172, 5) - (172, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<div/>");
#line (173, 5) - (173, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Null(el.Get("missing"));
#line (174, 5) - (174, 56) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("fallback", el.Get("missing", "fallback"));
            }

            [Xunit.FactAttribute]
            public void TestElementSetAddsOrUpdatesAttribute()
            {
#line (178, 5) - (178, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("div");
#line (179, 5) - (179, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                el.Set("id", "main");
#line (180, 5) - (180, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("main", el.Get("id"));
#line (181, 5) - (181, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                el.Set("id", "updated");
#line (182, 5) - (182, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("updated", el.Get("id"));
            }

            [Xunit.FactAttribute]
            public void TestElementKeysReturnsAttributeNames()
            {
#line (186, 5) - (186, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<div id=\"1\" class=\"main\"/>");
#line (187, 5) - (187, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<string> keys = el.Keys();
#line (188, 5) - (188, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(keys));
            }

            [Xunit.FactAttribute]
            public void TestElementItemsReturnsNameValueTuples()
            {
#line (192, 5) - (192, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<div id=\"1\"/>");
#line (193, 5) - (193, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::System.ValueTuple<string, string>> items = el.Items();
#line (194, 5) - (194, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(items));
#line (195, 5) - (195, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(("id", "1"), items[0]);
            }

            [Xunit.FactAttribute]
            public void TestElementAttribReturnsDictOfAttributes()
            {
#line (199, 5) - (199, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<div id=\"1\" class=\"main\"/>");
#line (200, 5) - (200, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.Dict<string, string> attrib = el.Attrib;
#line (201, 5) - (201, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("1", attrib["id"]);
#line (202, 5) - (202, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("main", attrib["class"]);
            }

            [Xunit.FactAttribute]
            public void TestElementAppendAddsChild()
            {
#line (208, 5) - (208, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("root");
#line (209, 5) - (209, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var child = new global::Sharpy.Element("child");
#line (210, 5) - (210, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Append(child);
#line (211, 5) - (211, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, root.Len());
#line (212, 5) - (212, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("child", root[0].Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementInsertAtPosition()
            {
#line (216, 5) - (216, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><c/></root>");
#line (217, 5) - (217, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var b = new global::Sharpy.Element("b");
#line (218, 5) - (218, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Insert(1, b);
#line (219, 5) - (219, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(3, root.Len());
#line (220, 5) - (220, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("b", root[1].Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementRemoveRemovesChild()
            {
#line (224, 5) - (224, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("root");
#line (225, 5) - (225, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var child = new global::Sharpy.Element("child");
#line (226, 5) - (226, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Append(child);
#line (227, 5) - (227, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, root.Len());
#line (228, 5) - (228, 23) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Remove(child);
#line (229, 5) - (229, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(0, root.Len());
            }

            [Xunit.FactAttribute]
            public void TestElementRemoveNotChildThrowsValueError()
            {
#line (233, 5) - (233, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("root");
#line (234, 5) - (234, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var other = new global::Sharpy.Element("other");
#line (235, 5) - (238, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (236, 9) - (236, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    root.Remove(other);
                }));
            }

            [Xunit.FactAttribute]
            public void TestElementClearRemovesAllChildren()
            {
#line (240, 5) - (240, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><c/></root>");
#line (241, 5) - (241, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(3, root.Len());
#line (242, 5) - (242, 17) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Clear();
#line (243, 5) - (243, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(0, root.Len());
            }

            [Xunit.FactAttribute]
            public void TestElementExtendAddsMultipleChildren()
            {
#line (247, 5) - (247, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("root");
#line (248, 5) - (248, 60) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> children = new Sharpy.List<global::Sharpy.Element>()
                {
                    new global::Sharpy.Element("a"),
                    new global::Sharpy.Element("b")
                };
#line (249, 5) - (249, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Extend(children);
#line (250, 5) - (250, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, root.Len());
            }

            [Xunit.FactAttribute]
            public void TestElementFindSimpleTagReturnsFirstMatch()
            {
#line (256, 5) - (256, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><a/></root>");
#line (257, 5) - (257, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var found = root.Find("a");
#line (258, 5) - (258, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(found);
#line (259, 5) - (259, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("a", found.Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementFindNoMatchReturnsNull()
            {
#line (263, 5) - (263, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/></root>");
#line (264, 5) - (264, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var found = root.Find("nonexistent");
#line (265, 5) - (265, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Null(found);
            }

            [Xunit.FactAttribute]
            public void TestElementFindAllReturnsAllMatches()
            {
#line (269, 5) - (269, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><a/></root>");
#line (270, 5) - (270, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("a");
#line (271, 5) - (271, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestElementFindTextReturnsTextOfFirstMatch()
            {
#line (275, 5) - (275, 77) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><name>Alice</name><name>Bob</name></root>");
#line (276, 5) - (276, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var text = root.FindText("name");
#line (277, 5) - (277, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("Alice", text);
            }

            [Xunit.FactAttribute]
            public void TestElementFindTextNoMatchReturnsDefault()
            {
#line (281, 5) - (281, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root/>");
#line (282, 5) - (282, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Null(root.FindText("missing"));
#line (283, 5) - (283, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("default", root.FindText("missing", "default"));
            }

            [Xunit.FactAttribute]
            public void TestElementIterNoTagReturnsAllDescendants()
            {
#line (289, 5) - (289, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><b/></a><c/></root>");
#line (290, 5) - (290, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<string> tags = new Sharpy.List<string>()
                {
                };
#line (291, 5) - (294, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                foreach (var __loopVar_1 in root.Iter())
                {
                    var el = __loopVar_1;
#line (292, 9) - (292, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    tags.Append(el.Tag);
                }

#line (294, 5) - (294, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(4, global::Sharpy.Builtins.Len(tags));
#line (295, 5) - (295, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("root", tags);
#line (296, 5) - (296, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("a", tags);
#line (297, 5) - (297, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("b", tags);
#line (298, 5) - (298, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("c", tags);
            }

            [Xunit.FactAttribute]
            public void TestElementIterWithTagFiltersDescendants()
            {
#line (302, 5) - (302, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><a/></a><b/></root>");
#line (303, 5) - (303, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<string> tags = new Sharpy.List<string>()
                {
                };
#line (304, 5) - (306, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                foreach (var __loopVar_2 in root.Iter("a"))
                {
                    var el = __loopVar_2;
#line (305, 9) - (305, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    tags.Append(el.Tag);
                }

#line (306, 5) - (306, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(tags));
            }

            [Xunit.FactAttribute]
            public void TestElementIterTextReturnsAllTextContent()
            {
#line (310, 5) - (310, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root>Hello <b>world</b>!</root>");
#line (311, 5) - (311, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<string> texts = new Sharpy.List<string>()
                {
                };
#line (312, 5) - (314, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                foreach (var __loopVar_3 in root.IterText())
                {
                    var text = __loopVar_3;
#line (313, 9) - (313, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    texts.Append(text);
                }

#line (314, 5) - (314, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("Hello ", texts);
#line (315, 5) - (315, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("world", texts);
#line (316, 5) - (316, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("!", texts);
            }

            [Xunit.FactAttribute]
            public void TestFindDotReturnsSelf()
            {
#line (322, 5) - (322, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root/>");
#line (323, 5) - (323, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var found = root.Find(".");
#line (324, 5) - (324, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(found);
#line (325, 5) - (325, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("root", found.Tag);
            }

            [Xunit.FactAttribute]
            public void TestFindAllWildcardReturnsAllDirectChildren()
            {
#line (329, 5) - (329, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><c/></root>");
#line (330, 5) - (330, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("*");
#line (331, 5) - (331, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestFindAllDescendantPatternFindsDeep()
            {
#line (335, 5) - (335, 68) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><target/></a><target/></root>");
#line (336, 5) - (336, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll(".//target");
#line (337, 5) - (337, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestFindAllAttributePredicateFiltersOnAttribute()
            {
#line (341, 5) - (341, 82) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><item id=\"1\"/><item/><item id=\"2\"/></root>");
#line (342, 5) - (342, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("item[@id]");
#line (343, 5) - (343, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestFindAllAttributeValuePredicateFiltersOnValue()
            {
#line (347, 5) - (347, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><item id=\"1\"/><item id=\"2\"/></root>");
#line (348, 5) - (348, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("item[@id='1']");
#line (349, 5) - (349, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(found));
#line (350, 5) - (350, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("1", found[0].Get("id"));
            }

            [Xunit.FactAttribute]
            public void TestFindAllChildTagPredicateFiltersOnChildPresence()
            {
#line (354, 5) - (354, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><name/></a><a/><a><name/></a></root>");
#line (355, 5) - (355, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("a[name]");
#line (356, 5) - (356, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestFindAllPositionPredicateReturnsNthElement()
            {
#line (360, 5) - (360, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><a/><a/></root>");
#line (361, 5) - (361, 50) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("a[2]");
#line (362, 5) - (362, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestTostringSimpleElementReturnsXmlString()
            {
#line (368, 5) - (368, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<root/>");
#line (369, 5) - (369, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string s = xml.Tostring(el);
#line (370, 5) - (370, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("<root />", s);
            }

            [Xunit.FactAttribute]
            public void TestTostringElementWithTextPreservesText()
            {
#line (374, 5) - (374, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<msg>hello</msg>");
#line (375, 5) - (375, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string s = xml.Tostring(el);
#line (376, 5) - (376, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("hello", s);
            }

            [Xunit.FactAttribute]
            public void TestTostringTextMethodReturnsTextOnly()
            {
#line (380, 5) - (380, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring("<root>Hello <b>world</b></root>");
#line (381, 5) - (381, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string text = xml.Tostring(el, method: "text");
#line (382, 5) - (382, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("Hello world", text);
            }

            [Xunit.FactAttribute]
            public void TestTostringRoundtripPreservesStructure()
            {
#line (386, 5) - (386, 61) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string original = "<root><a id=\"1\">text</a><b/></root>";
#line (387, 5) - (387, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = xml.Fromstring(original);
#line (388, 5) - (388, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string serialized = xml.Tostring(el);
#line (389, 5) - (389, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var reparsed = xml.Fromstring(serialized);
#line (390, 5) - (390, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("root", reparsed.Tag);
#line (391, 5) - (391, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, reparsed.Len());
#line (392, 5) - (392, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("a", reparsed[0].Tag);
#line (393, 5) - (393, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("1", reparsed[0].Get("id"));
            }

            [Xunit.FactAttribute]
            public void TestCommentCreatesCommentElement()
            {
#line (399, 5) - (399, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var comment = xml.Comment("This is a comment");
#line (400, 5) - (400, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("{sharpy:internal}comment", comment.Tag);
#line (401, 5) - (401, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("This is a comment", comment.Text);
            }

            [Xunit.FactAttribute]
            public void TestCommentSerialization()
            {
#line (405, 5) - (405, 41) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var comment = xml.Comment(" a comment ");
#line (406, 5) - (406, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("<!-- a comment -->", xml.Tostring(comment));
            }

            [Xunit.FactAttribute]
            public void TestProcessingInstructionCreatesPiElement()
            {
#line (410, 5) - (410, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var pi = xml.ProcessingInstruction("xml-stylesheet", "type=\"text/xsl\"");
#line (411, 5) - (411, 59) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("{sharpy:internal}pi-xml-stylesheet", pi.Tag);
#line (412, 5) - (412, 43) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("type=\"text/xsl\"", pi.Text);
            }

            [Xunit.FactAttribute]
            public void TestProcessingInstructionSerialization()
            {
#line (416, 5) - (416, 75) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var pi = xml.ProcessingInstruction("xml-stylesheet", "type=\"text/xsl\"");
#line (417, 5) - (417, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("<?xml-stylesheet type=\"text/xsl\"?>", xml.Tostring(pi));
            }

            [Xunit.FactAttribute]
            public void TestProcessingInstructionNoTextSerialization()
            {
#line (421, 5) - (421, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var pi = xml.ProcessingInstruction("target");
#line (422, 5) - (422, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("<?target?>", xml.Tostring(pi));
            }

            [Xunit.FactAttribute]
            public void TestElementTreeConstructorWithRoot()
            {
#line (428, 5) - (428, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("root");
#line (429, 5) - (429, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (430, 5) - (430, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var retrieved = tree.Getroot();
#line (431, 5) - (431, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(retrieved);
#line (432, 5) - (432, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("root", retrieved.Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeConstructorEmpty()
            {
#line (436, 5) - (436, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree();
#line (437, 5) - (437, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Null(tree.Getroot());
            }

            [Xunit.FactAttribute]
            public void TestElementTreeFindDelegatesToRoot()
            {
#line (441, 5) - (441, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><child/></root>");
#line (442, 5) - (442, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (443, 5) - (443, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var found = tree.Find("child");
#line (444, 5) - (444, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(found);
#line (445, 5) - (445, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("child", found.Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeFindAllDelegatesToRoot()
            {
#line (449, 5) - (449, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/><a/></root>");
#line (450, 5) - (450, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (451, 5) - (451, 47) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = tree.FindAll("a");
#line (452, 5) - (452, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestElementTreeParseFromFileReturnsTree()
            {
#line (456, 5) - (456, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string tempFile = tempfile.Mkstemp().Item2;
#line (457, 5) - (457, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var f = global::Sharpy.Builtins.Open(tempFile, "w");
#line (458, 5) - (458, 71) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Write("<?xml version=\"1.0\"?><root><child>text</child></root>");
#line (459, 5) - (459, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Close();
#line (460, 5) - (460, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = xml.Parse(tempFile);
#line (461, 5) - (461, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = tree.Getroot();
#line (462, 5) - (462, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(root);
#line (463, 5) - (463, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("root", root.Tag);
#line (464, 5) - (464, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("child", root[0].Tag);
#line (465, 5) - (465, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                os.Remove(tempFile);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeParseInvalidFileThrowsParseError()
            {
#line (469, 5) - (469, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string tempFile = tempfile.Mkstemp().Item2;
#line (470, 5) - (470, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var f = global::Sharpy.Builtins.Open(tempFile, "w");
#line (471, 5) - (471, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Write("<broken>");
#line (472, 5) - (472, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Close();
#line (473, 5) - (475, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Throws<ParseError>((global::System.Action)(() =>
                {
#line (474, 9) - (474, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    xml.Parse(tempFile);
                }));
#line (475, 5) - (475, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                os.Remove(tempFile);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeWriteCreatesFile()
            {
#line (479, 5) - (479, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string tempFile = tempfile.Mkstemp().Item2;
#line (480, 5) - (480, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("data");
#line (481, 5) - (481, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                root.Text = "content";
#line (482, 5) - (482, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (483, 5) - (483, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                tree.Write(tempFile);
#line (484, 5) - (484, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var f = global::Sharpy.Builtins.Open(tempFile, "r");
#line (485, 5) - (485, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string content = f.Read();
#line (486, 5) - (486, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Close();
#line (487, 5) - (487, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("<data>content</data>", content);
#line (488, 5) - (488, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                os.Remove(tempFile);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeIterIteratesAllElements()
            {
#line (492, 5) - (492, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/></root>");
#line (493, 5) - (493, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (494, 5) - (494, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<string> tags = new Sharpy.List<string>()
                {
                };
#line (495, 5) - (497, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                foreach (var __loopVar_4 in tree.Iter())
                {
                    var el = __loopVar_4;
#line (496, 9) - (496, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    tags.Append(el.Tag);
                }

#line (497, 5) - (497, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(3, global::Sharpy.Builtins.Len(tags));
            }

            [Xunit.FactAttribute]
            public void TestIselementElementReturnsTrue()
            {
#line (503, 5) - (503, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("root");
#line (504, 5) - (504, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.True(xml.Iselement(el));
            }

            [Xunit.FactAttribute]
            public void TestIselementNonElementReturnsFalse()
            {
#line (508, 5) - (508, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.False(xml.Iselement("not an element"));
#line (509, 5) - (509, 36) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.False(xml.Iselement(null));
#line (510, 5) - (510, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.False(xml.Iselement(42));
            }

            [Xunit.FactAttribute]
            public void TestIndentAddsWhitespace()
            {
#line (514, 5) - (514, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><b/></a><c/></root>");
#line (515, 5) - (515, 21) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                xml.Indent(root);
#line (516, 5) - (516, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string s = xml.Tostring(root);
#line (518, 5) - (518, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("\n", s);
            }

            [Xunit.FactAttribute]
            public void TestIndentTreeDelegatesToIndent()
            {
#line (522, 5) - (522, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a/><b/></root>");
#line (523, 5) - (523, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (524, 5) - (524, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                xml.IndentTree(tree);
#line (525, 5) - (525, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var treeRoot = tree.Getroot();
#line (526, 5) - (526, 34) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(treeRoot);
#line (527, 5) - (527, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string s = xml.Tostring(treeRoot);
#line (528, 5) - (528, 22) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("\n", s);
            }

            [Xunit.FactAttribute]
            public void TestFindWithNamespacesResolvesPrefix()
            {
#line (534, 5) - (534, 79) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string xmlStr = "<root xmlns:ns=\"http://example.com\"><ns:child/></root>";
#line (535, 5) - (535, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring(xmlStr);
#line (536, 5) - (536, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.Dict<string, string> ns = new Sharpy.Dict<string, string>()
                {
                    {
                        "ns",
                        "http://example.com"
                    }
                };
#line (537, 5) - (537, 38) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var found = root.Find("ns:child", ns);
#line (538, 5) - (538, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(found);
#line (539, 5) - (539, 53) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("{http://example.com}child", found.Tag);
            }

            [Xunit.FactAttribute]
            public void TestFindAllWithNamespacesReturnsMatches()
            {
#line (543, 5) - (543, 89) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string xmlStr = "<root xmlns:ns=\"http://example.com\"><ns:a/><ns:b/><ns:a/></root>";
#line (544, 5) - (544, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring(xmlStr);
#line (545, 5) - (545, 55) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.Dict<string, string> ns = new Sharpy.Dict<string, string>()
                {
                    {
                        "ns",
                        "http://example.com"
                    }
                };
#line (546, 5) - (546, 54) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> found = root.FindAll("ns:a", ns);
#line (547, 5) - (547, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(found));
            }

            [Xunit.FactAttribute]
            public void TestElementTagWithNamespaceFormattedCorrectly()
            {
#line (551, 5) - (551, 45) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("{http://example.com}item");
#line (552, 5) - (552, 49) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("{http://example.com}item", el.Tag);
            }

            [Xunit.FactAttribute]
            public void TestElementSetTagChangesTag()
            {
#line (556, 5) - (556, 24) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("old");
#line (557, 5) - (557, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("old", el.Tag);
#line (558, 5) - (558, 19) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                el.Tag = "new";
#line (559, 5) - (559, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("new", el.Tag);
            }

            [Xunit.FactAttribute]
            public void TestParseErrorHasPositionInfo()
            {
#line (565, 5) - (565, 66) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var err = new global::Sharpy.ParseError("test error", position: 10, line: 2, column: 5);
#line (566, 5) - (566, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(10, err.Position);
#line (567, 5) - (567, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, err.Line);
#line (568, 5) - (568, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(5, err.Column);
#line (569, 5) - (569, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("test error", global::Sharpy.Builtins.Str(err));
#line (570, 5) - (570, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("line 2", global::Sharpy.Builtins.Str(err));
#line (571, 5) - (571, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("column 5", global::Sharpy.Builtins.Str(err));
            }

            [Xunit.FactAttribute]
            public void TestParseErrorDefaultPosition()
            {
#line (575, 5) - (575, 37) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var err = new global::Sharpy.ParseError("simple error");
#line (576, 5) - (576, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(0, err.Position);
#line (577, 5) - (577, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(0, err.Line);
#line (578, 5) - (578, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(0, err.Column);
            }

            [Xunit.FactAttribute]
            public void TestElementToStringShowsTag()
            {
#line (584, 5) - (584, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("data");
#line (585, 5) - (585, 42) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("<Element 'data'>", global::Sharpy.Builtins.Str(el));
            }

            [Xunit.FactAttribute]
            public void TestElementTreeToStringShowsRoot()
            {
#line (589, 5) - (589, 27) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = new global::Sharpy.Element("root");
#line (590, 5) - (590, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree(root);
#line (591, 5) - (591, 32) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("root", global::Sharpy.Builtins.Str(tree));
            }

            [Xunit.FactAttribute]
            public void TestElementTreeToStringEmpty()
            {
#line (595, 5) - (595, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = new global::Sharpy.ElementTree();
#line (596, 5) - (596, 33) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Contains("empty", global::Sharpy.Builtins.Str(tree));
            }

            [Xunit.FactAttribute]
            public void TestFindAllMultiStepPathFindsNestedElements()
            {
#line (602, 5) - (602, 73) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><b>1</b></a><a><b>2</b></a></root>");
#line (603, 5) - (603, 51) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Sharpy.List<global::Sharpy.Element> results = root.FindAll("a/b");
#line (604, 5) - (604, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(2, global::Sharpy.Builtins.Len(results));
#line (605, 5) - (605, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("1", results[0].Text);
#line (606, 5) - (606, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("2", results[1].Text);
            }

            [Xunit.FactAttribute]
            public void TestFindMultiStepPathFindsFirstMatch()
            {
#line (610, 5) - (610, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = xml.Fromstring("<root><a><b>found</b></a></root>");
#line (611, 5) - (611, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var result = root.Find("a/b");
#line (612, 5) - (612, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(result);
#line (613, 5) - (613, 35) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("found", result.Text);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeParseStaticFromFile()
            {
#line (619, 5) - (619, 44) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                string tempFile = tempfile.Mkstemp().Item2;
#line (620, 5) - (620, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var f = global::Sharpy.Builtins.Open(tempFile, "w");
#line (621, 5) - (621, 48) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Write("<root><child>text</child></root>");
#line (622, 5) - (622, 14) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                f.Close();
#line (623, 5) - (623, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = global::Sharpy.ElementTree.Parse(tempFile);
#line (624, 5) - (624, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = tree.Getroot();
#line (625, 5) - (625, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(root);
#line (626, 5) - (626, 31) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("root", root.Tag);
#line (627, 5) - (627, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                os.Remove(tempFile);
            }

            [Xunit.FactAttribute]
            public void TestElementTreeParseStringReturnsTree()
            {
#line (631, 5) - (631, 58) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var tree = global::Sharpy.ElementTree.ParseString("<doc><item/></doc>");
#line (632, 5) - (632, 26) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var root = tree.Getroot();
#line (633, 5) - (633, 29) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.NotNull(root);
#line (634, 5) - (634, 30) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal("doc", root.Tag);
#line (635, 5) - (635, 28) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Equal(1, root.Len());
            }

            [Xunit.FactAttribute]
            public void TestElementTreeParseStringInvalidXmlThrowsParseError()
            {
#line (639, 5) - (644, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Throws<ParseError>((global::System.Action)(() =>
                {
#line (640, 9) - (640, 46) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    global::Sharpy.ElementTree.ParseString("<invalid>");
                }));
            }

            [Xunit.FactAttribute]
            public void TestRegisterNamespaceDoesNotThrow()
            {
#line (648, 5) - (648, 62) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                xml.RegisterNamespace("test", "http://example.com/test");
            }

            [Xunit.FactAttribute]
            public void TestTostringUnknownMethodThrowsValueError()
            {
#line (654, 5) - (654, 25) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                var el = new global::Sharpy.Element("root");
#line (655, 5) - (657, 1) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                Xunit.Assert.Throws<ValueError>((global::System.Action)(() =>
                {
#line (656, 9) - (656, 40) 1 "src/Sharpy.Stdlib.Tests/Spy/xml/xml_module_tests.spy"
                    xml.Tostring(el, method: "html");
                }));
            }
        }
    }
}
