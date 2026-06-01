using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Sharpy.Tests
{
    public class XmlModuleTests
    {
        #region FromString - Basic Parsing

        [Fact]
        public void Fromstring_SimpleElement_ReturnsParsedElement()
        {
            Element el = Xml.Fromstring("<root/>");
            Assert.Equal("root", el.Tag);
        }

        [Fact]
        public void Fromstring_ElementWithText_ReturnsTextContent()
        {
            Element el = Xml.Fromstring("<msg>hello</msg>");
            Assert.Equal("msg", el.Tag);
            Assert.Equal("hello", el.Text);
        }

        [Fact]
        public void Fromstring_ElementWithAttributes_ReturnsAttributes()
        {
            Element el = Xml.Fromstring("<item id=\"1\" name=\"test\"/>");
            Assert.Equal("item", el.Tag);
            Assert.Equal("1", el.Get("id"));
            Assert.Equal("test", el.Get("name"));
        }

        [Fact]
        public void Fromstring_ElementWithNamespace_FormatsTagCorrectly()
        {
            Element el = Xml.Fromstring("<ns:root xmlns:ns=\"http://example.com\"/>");
            Assert.Equal("{http://example.com}root", el.Tag);
        }

        [Fact]
        public void Fromstring_InvalidXml_ThrowsParseError()
        {
            Assert.Throws<ParseError>(() => Xml.Fromstring("<unclosed>"));
        }

        [Fact]
        public void Fromstring_ElementWithChildren_IteratesChildren()
        {
            Element root = Xml.Fromstring("<root><a/><b/><c/></root>");
            var tags = new System.Collections.Generic.List<string>();
            foreach (Element child in root)
            {
                tags.Add(child.Tag);
            }

            Assert.Equal(3, tags.Count);
            Assert.Equal("a", tags[0]);
            Assert.Equal("b", tags[1]);
            Assert.Equal("c", tags[2]);
        }

        #endregion

        #region Element Construction

        [Fact]
        public void Element_Constructor_CreatesElementWithTag()
        {
            Element el = new Element("div");
            Assert.Equal("div", el.Tag);
            Assert.Null(el.Text);
        }

        [Fact]
        public void Element_ConstructorWithAttrib_SetsAttributes()
        {
            var attrib = new Dict<string, string>();
            attrib["class"] = "main";
            attrib["id"] = "content";

            Element el = new Element("div", attrib);
            Assert.Equal("main", el.Get("class"));
            Assert.Equal("content", el.Get("id"));
        }

        [Fact]
        public void CreateElement_Function_CreatesElement()
        {
            Element el = Xml.CreateElement("span");
            Assert.Equal("span", el.Tag);
        }

        [Fact]
        public void SubElement_AppendsChildToParent()
        {
            Element parent = new Element("root");
            Element child = Xml.SubElement(parent, "child");

            Assert.Equal("child", child.Tag);
            Assert.Equal(1, parent.Len());
            Assert.Equal("child", parent[0].Tag);
        }

        #endregion

        #region Element Indexing

        [Fact]
        public void Element_Indexing_PositiveIndex_ReturnsChild()
        {
            Element root = Xml.Fromstring("<root><a/><b/><c/></root>");
            Assert.Equal("a", root[0].Tag);
            Assert.Equal("b", root[1].Tag);
            Assert.Equal("c", root[2].Tag);
        }

        [Fact]
        public void Element_Indexing_NegativeIndex_ReturnsFromEnd()
        {
            Element root = Xml.Fromstring("<root><a/><b/><c/></root>");
            Assert.Equal("c", root[-1].Tag);
            Assert.Equal("b", root[-2].Tag);
            Assert.Equal("a", root[-3].Tag);
        }

        [Fact]
        public void Element_Indexing_OutOfRange_ThrowsIndexError()
        {
            Element root = Xml.Fromstring("<root><a/></root>");
            Assert.Throws<IndexError>(() => root[5]);
        }

        [Fact]
        public void Element_Len_ReturnsChildCount()
        {
            Element root = Xml.Fromstring("<root><a/><b/></root>");
            Assert.Equal(2, root.Len());
        }

        [Fact]
        public void Element_Len_Empty_ReturnsZero()
        {
            Element root = new Element("empty");
            Assert.Equal(0, root.Len());
        }

        [Fact]
        public void Element_ISized_Count_MatchesLen()
        {
            Element root = Xml.Fromstring("<root><a/><b/><c/></root>");
            int count = Builtins.Len((ISized)root);
            Assert.Equal(3, count);
        }

        #endregion

        #region Text and Tail

        [Fact]
        public void Element_Text_Read_ReturnsTextContent()
        {
            Element el = Xml.Fromstring("<p>Hello world</p>");
            Assert.Equal("Hello world", el.Text);
        }

        [Fact]
        public void Element_Text_Write_SetsTextContent()
        {
            Element el = new Element("p");
            el.Text = "New text";
            Assert.Equal("New text", el.Text);
        }

        [Fact]
        public void Element_Text_Null_WhenNoText()
        {
            Element el = new Element("br");
            Assert.Null(el.Text);
        }

        [Fact]
        public void Element_Tail_ReadWrite()
        {
            Element root = Xml.Fromstring("<root><a/>tail text<b/></root>");
            Element a = root[0];
            Assert.Equal("tail text", a.Tail);
        }

        #endregion

        #region Attributes

        [Fact]
        public void Element_Get_ExistingAttribute_ReturnsValue()
        {
            Element el = Xml.Fromstring("<div class=\"main\"/>");
            Assert.Equal("main", el.Get("class"));
        }

        [Fact]
        public void Element_Get_MissingAttribute_ReturnsDefault()
        {
            Element el = Xml.Fromstring("<div/>");
            Assert.Null(el.Get("missing"));
            Assert.Equal("fallback", el.Get("missing", "fallback"));
        }

        [Fact]
        public void Element_Set_AddsOrUpdatesAttribute()
        {
            Element el = new Element("div");
            el.Set("id", "main");
            Assert.Equal("main", el.Get("id"));

            el.Set("id", "updated");
            Assert.Equal("updated", el.Get("id"));
        }

        [Fact]
        public void Element_Keys_ReturnsAttributeNames()
        {
            Element el = Xml.Fromstring("<div id=\"1\" class=\"main\"/>");
            List<string> keys = el.Keys();
            Assert.Equal(2, ((ICollection<string>)keys).Count);
        }

        [Fact]
        public void Element_Items_ReturnsNameValueTuples()
        {
            Element el = Xml.Fromstring("<div id=\"1\"/>");
            List<(string, string)> items = el.Items();
            Assert.Single((IEnumerable<(string, string)>)items);
            Assert.Equal(("id", "1"), items[0]);
        }

        [Fact]
        public void Element_Attrib_ReturnsDictOfAttributes()
        {
            Element el = Xml.Fromstring("<div id=\"1\" class=\"main\"/>");
            Dict<string, string> attrib = el.Attrib;
            Assert.Equal("1", attrib["id"]);
            Assert.Equal("main", attrib["class"]);
        }

        #endregion

        #region Mutation

        [Fact]
        public void Element_Append_AddsChild()
        {
            Element root = new Element("root");
            Element child = new Element("child");
            root.Append(child);

            Assert.Equal(1, root.Len());
            Assert.Equal("child", root[0].Tag);
        }

        [Fact]
        public void Element_Insert_AtPosition()
        {
            Element root = Xml.Fromstring("<root><a/><c/></root>");
            Element b = new Element("b");
            root.Insert(1, b);

            Assert.Equal(3, root.Len());
            Assert.Equal("b", root[1].Tag);
        }

        [Fact]
        public void Element_Remove_RemovesChild()
        {
            Element root = new Element("root");
            Element child = new Element("child");
            root.Append(child);

            Assert.Equal(1, root.Len());
            root.Remove(child);
            Assert.Equal(0, root.Len());
        }

        [Fact]
        public void Element_Remove_NotChild_ThrowsValueError()
        {
            Element root = new Element("root");
            Element other = new Element("other");
            Assert.Throws<ValueError>(() => root.Remove(other));
        }

        [Fact]
        public void Element_Clear_RemovesAllChildren()
        {
            Element root = Xml.Fromstring("<root><a/><b/><c/></root>");
            Assert.Equal(3, root.Len());

            root.Clear();
            Assert.Equal(0, root.Len());
        }

        [Fact]
        public void Element_Extend_AddsMultipleChildren()
        {
            Element root = new Element("root");
            var children = new System.Collections.Generic.List<Element>
            {
                new Element("a"),
                new Element("b")
            };
            root.Extend(children);

            Assert.Equal(2, root.Len());
        }

        #endregion

        #region Find / FindAll / FindText

        [Fact]
        public void Element_Find_SimpleTag_ReturnsFirstMatch()
        {
            Element root = Xml.Fromstring("<root><a/><b/><a/></root>");
            Element? found = root.Find("a");
            Assert.NotNull(found);
            Assert.Equal("a", found!.Tag);
        }

        [Fact]
        public void Element_Find_NoMatch_ReturnsNull()
        {
            Element root = Xml.Fromstring("<root><a/></root>");
            Element? found = root.Find("nonexistent");
            Assert.Null(found);
        }

        [Fact]
        public void Element_FindAll_ReturnsAllMatches()
        {
            Element root = Xml.Fromstring("<root><a/><b/><a/></root>");
            List<Element> found = root.FindAll("a");
            Assert.Equal(2, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void Element_FindText_ReturnsTextOfFirstMatch()
        {
            Element root = Xml.Fromstring("<root><name>Alice</name><name>Bob</name></root>");
            string? text = root.FindText("name");
            Assert.Equal("Alice", text);
        }

        [Fact]
        public void Element_FindText_NoMatch_ReturnsDefault()
        {
            Element root = Xml.Fromstring("<root/>");
            Assert.Null(root.FindText("missing"));
            Assert.Equal("default", root.FindText("missing", "default"));
        }

        #endregion

        #region Iter / IterText

        [Fact]
        public void Element_Iter_NoTag_ReturnsAllDescendants()
        {
            Element root = Xml.Fromstring("<root><a><b/></a><c/></root>");
            var tags = new System.Collections.Generic.List<string>();
            foreach (Element el in root.Iter())
            {
                tags.Add(el.Tag);
            }

            // root + a + b + c = 4
            Assert.Equal(4, tags.Count);
            Assert.Contains("root", tags);
            Assert.Contains("a", tags);
            Assert.Contains("b", tags);
            Assert.Contains("c", tags);
        }

        [Fact]
        public void Element_Iter_WithTag_FiltersDescendants()
        {
            Element root = Xml.Fromstring("<root><a><a/></a><b/></root>");
            var tags = new System.Collections.Generic.List<string>();
            foreach (Element el in root.Iter("a"))
            {
                tags.Add(el.Tag);
            }

            Assert.Equal(2, tags.Count);
        }

        [Fact]
        public void Element_IterText_ReturnsAllTextContent()
        {
            Element root = Xml.Fromstring("<root>Hello <b>world</b>!</root>");
            var texts = new System.Collections.Generic.List<string>();
            foreach (string text in root.IterText())
            {
                texts.Add(text);
            }

            Assert.Contains("Hello ", texts);
            Assert.Contains("world", texts);
            Assert.Contains("!", texts);
        }

        #endregion

        #region XPath Patterns

        [Fact]
        public void Find_Dot_ReturnsSelf()
        {
            Element root = Xml.Fromstring("<root/>");
            Element? found = root.Find(".");
            Assert.NotNull(found);
            Assert.Equal("root", found!.Tag);
        }

        [Fact]
        public void FindAll_Wildcard_ReturnsAllDirectChildren()
        {
            Element root = Xml.Fromstring("<root><a/><b/><c/></root>");
            List<Element> found = root.FindAll("*");
            Assert.Equal(3, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void FindAll_DescendantPattern_FindsDeep()
        {
            Element root = Xml.Fromstring("<root><a><target/></a><target/></root>");
            List<Element> found = root.FindAll(".//target");
            Assert.Equal(2, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void FindAll_AttributePredicate_FiltersOnAttribute()
        {
            Element root = Xml.Fromstring("<root><item id=\"1\"/><item/><item id=\"2\"/></root>");
            List<Element> found = root.FindAll("item[@id]");
            Assert.Equal(2, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void FindAll_AttributeValuePredicate_FiltersOnValue()
        {
            Element root = Xml.Fromstring("<root><item id=\"1\"/><item id=\"2\"/></root>");
            List<Element> found = root.FindAll("item[@id='1']");
            Assert.Single((IEnumerable<Element>)found);
            Assert.Equal("1", found[0].Get("id"));
        }

        [Fact]
        public void FindAll_ChildTagPredicate_FiltersOnChildPresence()
        {
            Element root = Xml.Fromstring("<root><a><name/></a><a/><a><name/></a></root>");
            List<Element> found = root.FindAll("a[name]");
            Assert.Equal(2, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void FindAll_PositionPredicate_ReturnsNthElement()
        {
            Element root = Xml.Fromstring("<root><a/><a/><a/></root>");
            List<Element> found = root.FindAll("a[2]");
            Assert.Single((IEnumerable<Element>)found);
        }

        #endregion

        #region Serialization (Tostring)

        [Fact]
        public void Tostring_SimpleElement_ReturnsXmlString()
        {
            Element el = Xml.Fromstring("<root/>");
            string xml = Xml.Tostring(el);
            Assert.Equal("<root />", xml);
        }

        [Fact]
        public void Tostring_ElementWithText_PreservesText()
        {
            Element el = Xml.Fromstring("<msg>hello</msg>");
            string xml = Xml.Tostring(el);
            Assert.Contains("hello", xml);
        }

        [Fact]
        public void Tostring_TextMethod_ReturnsTextOnly()
        {
            Element el = Xml.Fromstring("<root>Hello <b>world</b></root>");
            string text = Xml.Tostring(el, method: "text");
            Assert.Equal("Hello world", text);
        }

        [Fact]
        public void Tostring_Roundtrip_PreservesStructure()
        {
            string original = "<root><a id=\"1\">text</a><b/></root>";
            Element el = Xml.Fromstring(original);
            string serialized = Xml.Tostring(el);

            // Re-parse and verify
            Element reparsed = Xml.Fromstring(serialized);
            Assert.Equal("root", reparsed.Tag);
            Assert.Equal(2, reparsed.Len());
            Assert.Equal("a", reparsed[0].Tag);
            Assert.Equal("1", reparsed[0].Get("id"));
        }

        #endregion

        #region Comment / ProcessingInstruction

        [Fact]
        public void Comment_CreatesCommentElement()
        {
            Element comment = Xml.Comment("This is a comment");
            Assert.Equal("{sharpy:internal}comment", comment.Tag);
            Assert.Equal("This is a comment", comment.Text);
        }

        [Fact]
        public void Comment_Serialization()
        {
            Element comment = Xml.Comment(" a comment ");
            Assert.Equal("<!-- a comment -->", Xml.Tostring(comment));
        }

        [Fact]
        public void ProcessingInstruction_CreatesPIElement()
        {
            Element pi = Xml.ProcessingInstruction("xml-stylesheet", "type=\"text/xsl\"");
            Assert.Equal("{sharpy:internal}pi-xml-stylesheet", pi.Tag);
            Assert.Equal("type=\"text/xsl\"", pi.Text);
        }

        [Fact]
        public void ProcessingInstruction_Serialization()
        {
            Element pi = Xml.ProcessingInstruction("xml-stylesheet", "type=\"text/xsl\"");
            Assert.Equal("<?xml-stylesheet type=\"text/xsl\"?>", Xml.Tostring(pi));
        }

        [Fact]
        public void ProcessingInstruction_NoText_Serialization()
        {
            Element pi = Xml.ProcessingInstruction("target");
            Assert.Equal("<?target?>", Xml.Tostring(pi));
        }

        #endregion

        #region ElementTree

        [Fact]
        public void ElementTree_Constructor_WithRoot()
        {
            Element root = new Element("root");
            ElementTree tree = new ElementTree(root);
            Element? retrieved = tree.Getroot();
            Assert.NotNull(retrieved);
            Assert.Equal("root", retrieved!.Tag);
        }

        [Fact]
        public void ElementTree_Constructor_Empty()
        {
            ElementTree tree = new ElementTree();
            Assert.Null(tree.Getroot());
        }

        [Fact]
        public void ElementTree_Find_DelegatesToRoot()
        {
            Element root = Xml.Fromstring("<root><child/></root>");
            ElementTree tree = new ElementTree(root);
            Element? found = tree.Find("child");
            Assert.NotNull(found);
            Assert.Equal("child", found!.Tag);
        }

        [Fact]
        public void ElementTree_FindAll_DelegatesToRoot()
        {
            Element root = Xml.Fromstring("<root><a/><b/><a/></root>");
            ElementTree tree = new ElementTree(root);
            List<Element> found = tree.FindAll("a");
            Assert.Equal(2, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void ElementTree_Parse_FromFile_ReturnsTree()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "<?xml version=\"1.0\"?><root><child>text</child></root>");
                ElementTree tree = Xml.Parse(tempFile);
                Element? root = tree.Getroot();
                Assert.NotNull(root);
                Assert.Equal("root", root!.Tag);
                Assert.Equal("child", root[0].Tag);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ElementTree_Parse_InvalidFile_ThrowsParseError()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "<broken>");
                Assert.Throws<ParseError>(() => Xml.Parse(tempFile));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ElementTree_Write_CreatesFile()
        {
            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
                Element root = new Element("data");
                root.Text = "content";
                ElementTree tree = new ElementTree(root);
                tree.Write(tempFile);

                string content = File.ReadAllText(tempFile);
                Assert.Contains("<data>content</data>", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ElementTree_Iter_IteratesAllElements()
        {
            Element root = Xml.Fromstring("<root><a/><b/></root>");
            ElementTree tree = new ElementTree(root);
            var tags = new System.Collections.Generic.List<string>();
            foreach (Element el in tree.Iter())
            {
                tags.Add(el.Tag);
            }

            Assert.Equal(3, tags.Count);
        }

        #endregion

        #region Utilities

        [Fact]
        public void Iselement_Element_ReturnsTrue()
        {
            Element el = new Element("root");
            Assert.True(Xml.Iselement(el));
        }

        [Fact]
        public void Iselement_NonElement_ReturnsFalse()
        {
            Assert.False(Xml.Iselement("not an element"));
            Assert.False(Xml.Iselement(null));
            Assert.False(Xml.Iselement(42));
        }

        [Fact]
        public void Indent_AddsWhitespace()
        {
            Element root = Xml.Fromstring("<root><a><b/></a><c/></root>");
            Xml.Indent(root);

            string xml = Xml.Tostring(root);
            // After indenting, the output should contain newlines
            Assert.Contains("\n", xml);
        }

        [Fact]
        public void IndentTree_DelegatesToIndent()
        {
            Element root = Xml.Fromstring("<root><a/><b/></root>");
            ElementTree tree = new ElementTree(root);
            Xml.IndentTree(tree);

            Element? treeRoot = tree.Getroot();
            Assert.NotNull(treeRoot);
            string xml = Xml.Tostring(treeRoot!);
            Assert.Contains("\n", xml);
        }

        #endregion

        #region Namespaces

        [Fact]
        public void Find_WithNamespaces_ResolvesPrefix()
        {
            string xmlStr = "<root xmlns:ns=\"http://example.com\"><ns:child/></root>";
            Element root = Xml.Fromstring(xmlStr);

            var ns = new Dict<string, string>();
            ns["ns"] = "http://example.com";
            Element? found = root.Find("ns:child", ns);

            Assert.NotNull(found);
            Assert.Equal("{http://example.com}child", found!.Tag);
        }

        [Fact]
        public void FindAll_WithNamespaces_ReturnsMatches()
        {
            string xmlStr = "<root xmlns:ns=\"http://example.com\"><ns:a/><ns:b/><ns:a/></root>";
            Element root = Xml.Fromstring(xmlStr);

            var ns = new Dict<string, string>();
            ns["ns"] = "http://example.com";
            List<Element> found = root.FindAll("ns:a", ns);
            Assert.Equal(2, ((ICollection<Element>)found).Count);
        }

        [Fact]
        public void Element_Tag_WithNamespace_FormattedCorrectly()
        {
            Element el = new Element("{http://example.com}item");
            Assert.Equal("{http://example.com}item", el.Tag);
        }

        [Fact]
        public void Element_Set_TagChangesTag()
        {
            Element el = new Element("old");
            Assert.Equal("old", el.Tag);
            el.Tag = "new";
            Assert.Equal("new", el.Tag);
        }

        #endregion

        #region ParseError

        [Fact]
        public void ParseError_HasPositionInfo()
        {
            var err = new ParseError("test error", position: 10, line: 2, column: 5);
            Assert.Equal(10, err.Position);
            Assert.Equal(2, err.Line);
            Assert.Equal(5, err.Column);
            Assert.Contains("test error", err.Message);
            Assert.Contains("line 2", err.Message);
            Assert.Contains("column 5", err.Message);
        }

        [Fact]
        public void ParseError_DefaultPosition()
        {
            var err = new ParseError("simple error");
            Assert.Equal(0, err.Position);
            Assert.Equal(0, err.Line);
            Assert.Equal(0, err.Column);
        }

        #endregion

        #region ToString Representation

        [Fact]
        public void Element_ToString_ShowsTag()
        {
            Element el = new Element("data");
            Assert.Equal("<Element 'data'>", el.ToString());
        }

        [Fact]
        public void ElementTree_ToString_ShowsRoot()
        {
            Element root = new Element("root");
            ElementTree tree = new ElementTree(root);
            Assert.Contains("root", tree.ToString());
        }

        [Fact]
        public void ElementTree_ToString_Empty()
        {
            ElementTree tree = new ElementTree();
            Assert.Contains("empty", tree.ToString());
        }

        #endregion
    }
}
