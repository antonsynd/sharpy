using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible ElementTree class wrapping <see cref="XDocument"/>.
    /// Represents an entire XML document tree.
    /// </summary>
    public sealed class XmlElementTree
    {
        private XDocument _document;

        /// <summary>
        /// Create an ElementTree from a root element.
        /// </summary>
        /// <param name="root">The root element. If null, creates an empty tree.</param>
        public XmlElementTree(XmlElement? root = null)
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

        internal XmlElementTree(XDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Get the root element of the tree.
        /// </summary>
        /// <returns>The root element, or null if the tree is empty.</returns>
        public XmlElement? Getroot()
        {
            XElement? root = _document.Root;
            return root != null ? new XmlElement(root) : null;
        }

        /// <summary>
        /// Replace the root element.
        /// </summary>
        /// <param name="element">The new root element.</param>
        internal void SetRoot(XElement element)
        {
            _document = new XDocument(element);
        }

        /// <summary>
        /// Find the first element matching the given path from the root.
        /// </summary>
        /// <param name="path">Simple tag name or path expression.</param>
        /// <returns>The first matching element, or null.</returns>
        public XmlElement? Find(string path)
        {
            XElement? root = _document.Root;
            if (root == null) return null;
            XElement? found = XPathHelper.FindFirst(root, path);
            return found != null ? new XmlElement(found) : null;
        }

        /// <summary>
        /// Find all elements matching the given path from the root.
        /// </summary>
        /// <param name="path">Simple tag name or path expression.</param>
        /// <returns>A list of matching elements.</returns>
        public List<XmlElement> Findall(string path)
        {
            List<XmlElement> result = new List<XmlElement>();
            XElement? root = _document.Root;
            if (root == null) return result;
            foreach (XElement e in XPathHelper.FindAll(_element: root, path: path))
            {
                result.Append(new XmlElement(e));
            }

            return result;
        }

        /// <summary>
        /// Write the XML tree to a file.
        /// </summary>
        /// <param name="filePath">The file path to write to.</param>
        /// <param name="xmlDeclaration">Whether to include the XML declaration.</param>
        /// <param name="encoding">The encoding to use (default: "utf-8").</param>
        public void Write(string filePath, bool xmlDeclaration = false, string encoding = "utf-8")
        {
            if (filePath == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            Encoding enc = GetEncoding(encoding);
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = enc,
                OmitXmlDeclaration = !xmlDeclaration
            };

            using (XmlWriter writer = XmlWriter.Create(filePath, settings))
            {
                _document.WriteTo(writer);
            }
        }

        /// <summary>
        /// Write the XML tree to a TextWriter.
        /// </summary>
        /// <param name="fp">The TextWriter to write to.</param>
        /// <param name="xmlDeclaration">Whether to include the XML declaration.</param>
        /// <param name="encoding">The encoding to use in the declaration.</param>
        public void Write(TextWriter fp, bool xmlDeclaration = false, string encoding = "utf-8")
        {
            if (fp == null)
            {
                throw new TypeError("expected file, got NoneType");
            }

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = !xmlDeclaration
            };

            using (XmlWriter writer = XmlWriter.Create(fp, settings))
            {
                _document.WriteTo(writer);
            }
        }

        private static Encoding GetEncoding(string encoding)
        {
            if (encoding == null)
            {
                return Encoding.UTF8;
            }

            switch (encoding.ToLowerInvariant())
            {
                case "utf-8":
                case "utf8":
                    return new UTF8Encoding(false);
                case "utf-16":
                case "utf16":
                    return Encoding.Unicode;
                case "ascii":
                    return Encoding.ASCII;
                case "latin-1":
                case "iso-8859-1":
#if NET10_0_OR_GREATER
                    return Encoding.Latin1;
#else
                    return Encoding.GetEncoding("iso-8859-1");
#endif
                default:
                    return Encoding.GetEncoding(encoding);
            }
        }
    }
}
