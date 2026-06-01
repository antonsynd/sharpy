using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Represents an XML document as an element tree.
    /// Mirrors Python's xml.etree.ElementTree.ElementTree.
    /// </summary>
    [SharpyModuleType("xml")]
    public sealed class ElementTree
    {
        private XDocument _document;

        /// <summary>Create a new ElementTree with an optional root element.</summary>
        /// <param name="root">The root element, or null for an empty tree.</param>
        public ElementTree(Element? root = null)
        {
            if (root != null)
            {
                _document = new XDocument(root.Underlying);
            }
            else
            {
                _document = new XDocument();
            }
        }

        /// <summary>Create an ElementTree wrapping an existing XDocument.</summary>
        internal ElementTree(XDocument doc)
        {
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        /// <summary>The underlying XDocument.</summary>
        internal XDocument Underlying => _document;

        /// <summary>
        /// Parse an XML file and return an ElementTree.
        /// </summary>
        /// <param name="source">The file path to parse.</param>
        /// <returns>An ElementTree representing the parsed document.</returns>
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
        /// Parse an XML string and return an ElementTree.
        /// </summary>
        /// <param name="text">The XML string to parse.</param>
        /// <returns>An ElementTree representing the parsed document.</returns>
        /// <exception cref="ParseError">If the XML is malformed.</exception>
        public static ElementTree ParseString(string text)
        {
            if (text == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            try
            {
                XDocument doc = XDocument.Parse(text);
                return new ElementTree(doc);
            }
            catch (XmlException ex)
            {
                throw ParseError.FromXmlException(ex);
            }
        }

        /// <summary>
        /// Get the root element of the tree.
        /// </summary>
        /// <returns>The root element, or null if the tree is empty.</returns>
        public Element? Getroot()
        {
            if (_document.Root != null)
            {
                return new Element(_document.Root);
            }

            return null;
        }

        /// <summary>
        /// Find the first matching element by path from the root.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>The first matching element, or null if not found.</returns>
        public Element? Find(string path, Dict<string, string>? namespaces = null)
        {
            if (_document.Root == null)
            {
                return null;
            }

            return new Element(_document.Root).Find(path, namespaces);
        }

        /// <summary>
        /// Find all matching elements by path from the root.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>A list of matching elements.</returns>
        public List<Element> FindAll(string path, Dict<string, string>? namespaces = null)
        {
            if (_document.Root == null)
            {
                return new List<Element>();
            }

            return new Element(_document.Root).FindAll(path, namespaces);
        }

        /// <summary>
        /// Iterate over all elements matching the given tag.
        /// </summary>
        /// <param name="tag">Optional tag filter. Null or "*" matches all.</param>
        /// <returns>An enumerable of matching elements.</returns>
        public IEnumerable<Element> Iter(string? tag = null)
        {
            if (_document.Root == null)
            {
                yield break;
            }

            foreach (Element el in new Element(_document.Root).Iter(tag))
            {
                yield return el;
            }
        }

        /// <summary>
        /// Find all matching elements using an XPath-like expression from the root.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>An enumerable of matching elements.</returns>
        public IEnumerable<Element> IterFind(string path, Dict<string, string>? namespaces = null)
        {
            if (_document.Root == null)
            {
                yield break;
            }

            foreach (Element el in new Element(_document.Root).IterFind(path, namespaces))
            {
                yield return el;
            }
        }

        /// <summary>
        /// Write the XML tree to a file.
        /// </summary>
        /// <param name="filePath">The file path to write to.</param>
        /// <param name="xmlDeclaration">Whether to include the XML declaration.</param>
        /// <param name="encoding">The encoding name (default: "utf-8").</param>
        public void Write(string filePath, bool xmlDeclaration = true, string encoding = "utf-8")
        {
            if (filePath == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            Encoding enc = Encoding.GetEncoding(encoding);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = !xmlDeclaration,
                Encoding = enc
            };

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                _document.Save(writer);
            }
        }

        /// <summary>Return a string representation of the tree.</summary>
        public override string ToString()
        {
            Element? root = Getroot();
            if (root != null)
            {
                return "<ElementTree " + root.ToString() + ">";
            }

            return "<ElementTree (empty)>";
        }
    }
}
