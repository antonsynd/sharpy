using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible xml module providing ElementTree-style API.
    /// Backed by System.Xml.Linq (LINQ to XML).
    /// </summary>
    public static partial class Xml
    {
        /// <summary>
        /// Parse an XML file into an ElementTree.
        /// </summary>
        /// <param name="source">The file path to parse.</param>
        /// <returns>An ElementTree representing the parsed XML.</returns>
        /// <example>
        /// <code>
        /// tree = xml.parse("data.xml")
        /// root = tree.getroot()
        /// </code>
        /// </example>
        public static XmlElementTree Parse(string source)
        {
            if (source == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            XDocument doc = XDocument.Load(source);
            return new XmlElementTree(doc);
        }

        /// <summary>
        /// Parse an XML string into an Element.
        /// </summary>
        /// <param name="text">The XML string to parse.</param>
        /// <returns>The root Element of the parsed XML.</returns>
        /// <example>
        /// <code>
        /// root = xml.fromstring("&lt;root&gt;&lt;child/&gt;&lt;/root&gt;")
        /// </code>
        /// </example>
        public static XmlElement Fromstring(string text)
        {
            if (text == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            XElement element = XElement.Parse(text);
            return new XmlElement(element);
        }

        /// <summary>
        /// Serialize an Element to a string.
        /// </summary>
        /// <param name="element">The element to serialize.</param>
        /// <param name="encoding">The encoding. Use "unicode" for a string result.</param>
        /// <returns>The XML string representation.</returns>
        /// <example>
        /// <code>
        /// xml_str = xml.tostring(root, encoding="unicode")
        /// </code>
        /// </example>
        public static string Tostring(XmlElement element, string encoding = "unicode")
        {
            if (element == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            if (string.Equals(encoding, "unicode", StringComparison.OrdinalIgnoreCase))
            {
                return element.Underlying.ToString();
            }

            // For non-unicode encodings, return string with XML declaration
            Encoding enc = GetEncodingByName(encoding);
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
                    element.Underlying.WriteTo(writer);
                }

                return enc.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// Create a new Element with the given tag and attributes.
        /// </summary>
        /// <param name="tag">The tag name (may include {namespace} prefix).</param>
        /// <param name="attrib">Optional dictionary of attributes.</param>
        /// <returns>A new Element.</returns>
        /// <example>
        /// <code>
        /// root = xml.Element("root")
        /// child = xml.Element("item", attrib={"id": "1"})
        /// </code>
        /// </example>
        public static XmlElement Element(string tag, Dict<string, string>? attrib = null)
        {
            if (tag == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            XName name = XmlElement.ParseName(tag);
            XElement xelem = new XElement(name);

            if (!object.ReferenceEquals(attrib, null))
            {
                foreach (var kvp in attrib.Items())
                {
                    XName attrName = XmlElement.ParseName(kvp.Item1);
                    xelem.SetAttributeValue(attrName, kvp.Item2);
                }
            }

            return new XmlElement(xelem);
        }

        /// <summary>
        /// Create a sub-element and append it to the parent.
        /// </summary>
        /// <param name="parent">The parent element.</param>
        /// <param name="tag">The tag name for the new element.</param>
        /// <param name="attrib">Optional dictionary of attributes.</param>
        /// <returns>The new child element.</returns>
        /// <example>
        /// <code>
        /// root = xml.Element("root")
        /// child = xml.SubElement(root, "child", attrib={"id": "1"})
        /// </code>
        /// </example>
        public static XmlElement SubElement(XmlElement parent, string tag, Dict<string, string>? attrib = null)
        {
            if (parent == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            if (tag == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            XmlElement child = Element(tag, attrib);
            parent.Append(child);
            return child;
        }

        /// <summary>
        /// Create an ElementTree from a root element.
        /// </summary>
        /// <param name="root">The root element.</param>
        /// <returns>A new ElementTree wrapping the root.</returns>
        public static XmlElementTree ElementTree(XmlElement? root = null)
        {
            return new XmlElementTree(root);
        }

        private static Encoding GetEncodingByName(string encoding)
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
