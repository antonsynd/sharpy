using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Module-level functions for the xml module.
    /// Mirrors Python's xml.etree.ElementTree module functions.
    /// </summary>
    public static partial class Xml
    {
        /// <summary>
        /// Create a new Element with the given tag and optional attributes.
        /// </summary>
        /// <param name="tag">The element tag name.</param>
        /// <param name="attrib">Optional dictionary of attributes.</param>
        /// <returns>A new <see cref="Element"/>.</returns>
        public static Element CreateElement(string tag, Dict<string, string>? attrib = null)
        {
            return new Element(tag, attrib);
        }

        /// <summary>
        /// Create a child element and append it to the parent.
        /// </summary>
        /// <param name="parent">The parent element.</param>
        /// <param name="tag">The child element tag name.</param>
        /// <param name="attrib">Optional dictionary of attributes.</param>
        /// <returns>The newly created child element.</returns>
        public static Element SubElement(Element parent, string tag, Dict<string, string>? attrib = null)
        {
            if (parent == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            Element child = new Element(tag, attrib);
            parent.Append(child);
            return child;
        }

        /// <summary>
        /// Parse an XML file and return an ElementTree.
        /// </summary>
        /// <param name="source">The file path to parse.</param>
        /// <returns>An <see cref="ElementTree"/> representing the parsed document.</returns>
        /// <exception cref="ParseError">If the XML is malformed.</exception>
        public static ElementTree Parse(string source)
        {
            if (source == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            try
            {
                XDocument doc = XDocument.Load(source);
                return new ElementTree(doc);
            }
            catch (XmlException ex)
            {
                throw ParseError.FromXmlException(ex);
            }
        }

        /// <summary>
        /// Parse an XML string and return the root Element.
        /// </summary>
        /// <param name="text">The XML string to parse.</param>
        /// <returns>The root <see cref="Element"/>.</returns>
        /// <exception cref="ParseError">If the XML is malformed.</exception>
        public static Element Fromstring(string text)
        {
            if (text == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            try
            {
                XElement element = XElement.Parse(text);
                return new Element(element);
            }
            catch (XmlException ex)
            {
                throw ParseError.FromXmlException(ex);
            }
        }

        /// <summary>
        /// Serialize an Element to an XML string.
        /// </summary>
        /// <param name="element">The element to serialize.</param>
        /// <param name="encoding">The encoding. Use "unicode" for a string result.</param>
        /// <param name="method">The serialization method ("xml", "html", or "text").</param>
        /// <returns>The XML string representation.</returns>
        public static string Tostring(Element element, string encoding = "unicode", string method = "xml")
        {
            if (element == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            if (method == "text")
            {
                return element.Underlying.Value;
            }

            string tag = element.Tag;
            if (tag == CommentTag)
            {
                return "<!--" + (element.Text ?? "") + "-->";
            }

            if (tag.StartsWith(PITagPrefix))
            {
                string target = tag.Substring(PITagPrefix.Length);
                string? piText = element.Text;
                return piText != null ? "<?" + target + " " + piText + "?>" : "<?" + target + "?>";
            }

            if (encoding == "unicode")
            {
                return element.Underlying.ToString(SaveOptions.DisableFormatting);
            }

            // For non-unicode encodings, include XML declaration
            Encoding enc = Encoding.GetEncoding(encoding);
            using (MemoryStream ms = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings
                {
                    Encoding = enc,
                    OmitXmlDeclaration = false,
                    Indent = false
                };

                using (XmlWriter writer = XmlWriter.Create(ms, settings))
                {
                    element.Underlying.Save(writer);
                }

                return enc.GetString(ms.ToArray());
            }
        }

        internal const string CommentTag = "{sharpy:internal}comment";
        internal const string PITagPrefix = "{sharpy:internal}pi-";

        /// <summary>
        /// Create a comment element with the given text.
        /// Serialized as <c>&lt;!-- text --&gt;</c> by <see cref="Tostring"/>.
        /// </summary>
        /// <param name="text">The comment text.</param>
        /// <returns>A new comment element.</returns>
        public static Element Comment(string text)
        {
            Element el = new Element(CommentTag);
            el.Text = text;
            return el;
        }

        /// <summary>
        /// Create a processing instruction element.
        /// Serialized as <c>&lt;?target text?&gt;</c> by <see cref="Tostring"/>.
        /// </summary>
        /// <param name="target">The PI target.</param>
        /// <param name="text">Optional PI text.</param>
        /// <returns>A new PI element.</returns>
        public static Element ProcessingInstruction(string target, string? text = null)
        {
            Element el = new Element(PITagPrefix + target);
            if (text != null)
            {
                el.Text = text;
            }

            return el;
        }

        /// <summary>
        /// Check if an object is an Element.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if the object is an Element.</returns>
        public static bool Iselement(object? obj)
        {
            return obj is Element;
        }

        /// <summary>
        /// Add indentation to an element tree for pretty-printing.
        /// Modifies the tree in-place by adding whitespace text.
        /// </summary>
        /// <param name="element">The root element to indent.</param>
        /// <param name="space">The indentation string per level (default: two spaces).</param>
        /// <param name="level">The starting indentation level (default: 0).</param>
        public static void Indent(Element element, string space = "  ", int level = 0)
        {
            if (element == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            IndentElement(element.Underlying, space, level);
        }

        /// <summary>
        /// Add indentation to an element tree for pretty-printing.
        /// </summary>
        /// <param name="tree">The ElementTree to indent.</param>
        /// <param name="space">The indentation string per level (default: two spaces).</param>
        /// <param name="level">The starting indentation level (default: 0).</param>
        public static void IndentTree(ElementTree tree, string space = "  ", int level = 0)
        {
            if (tree == null)
            {
                throw new TypeError("expected ElementTree, got NoneType");
            }

            Element? root = tree.Getroot();
            if (root != null)
            {
                Indent(root, space, level);
            }
        }

        private static void IndentElement(XElement element, string space, int level)
        {
            // Collect child elements
            var children = new System.Collections.Generic.List<XElement>();
            foreach (XElement child in element.Elements())
            {
                children.Add(child);
            }

            if (children.Count == 0)
            {
                return;
            }

            string indent = "\n" + RepeatString(space, level + 1);
            string childTail = "\n" + RepeatString(space, level);

            // Set text before first child
            XNode? firstNode = element.FirstNode;
            bool hasTextBefore = false;
            if (firstNode is XText existingText)
            {
                string trimmed = existingText.Value.Trim();
                if (trimmed.Length == 0)
                {
                    existingText.Value = indent;
                    hasTextBefore = true;
                }
            }

            if (!hasTextBefore)
            {
                // Only add indentation if there's no meaningful text before first child
                XNode? node = element.FirstNode;
                if (node is XElement)
                {
                    node.AddBeforeSelf(new XText(indent));
                }
            }

            // Set tail of each child
            for (int i = 0; i < children.Count; i++)
            {
                XElement child = children[i];
                string tail = (i < children.Count - 1) ? indent : childTail;

                // Set tail text after this child element
                XNode? nextNode = child.NextNode;
                if (nextNode is XText tailText)
                {
                    string trimmed = tailText.Value.Trim();
                    if (trimmed.Length == 0)
                    {
                        tailText.Value = tail;
                    }
                }
                else
                {
                    child.AddAfterSelf(new XText(tail));
                }

                // Recursively indent children
                IndentElement(child, space, level + 1);
            }
        }

        private static string RepeatString(string s, int count)
        {
            if (count <= 0)
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++)
            {
                sb.Append(s);
            }

            return sb.ToString();
        }
    }
}
