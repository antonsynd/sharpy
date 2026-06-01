using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Represents an XML element.
    /// Mirrors Python's xml.etree.ElementTree.Element.
    /// Wraps <see cref="XElement"/> and provides a Python-compatible API.
    /// </summary>
    [SharpyModuleType("xml")]
    public sealed class Element : IEnumerable<Element>, ISized
    {
        private readonly XElement _element;

        /// <summary>Create a new Element with the given tag and optional attributes.</summary>
        /// <param name="tag">The element tag name. Supports <c>{uri}local</c> notation for namespaces.</param>
        /// <param name="attrib">Optional dictionary of attributes.</param>
        public Element(string tag, Dict<string, string>? attrib = null)
        {
            _element = new XElement(ParseName(tag));
            if (attrib is object)
            {
                foreach (var pair in (IEnumerable<KeyValuePair<string, string>>)attrib)
                {
                    _element.SetAttributeValue(ParseName(pair.Key), pair.Value);
                }
            }
        }

        /// <summary>Create an Element wrapping an existing XElement.</summary>
        internal Element(XElement element)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
        }

        /// <summary>The underlying XElement.</summary>
        internal XElement Underlying => _element;

        #region Tag

        /// <summary>
        /// Gets or sets the element tag name.
        /// Namespace-qualified tags are returned as <c>{uri}local</c>.
        /// </summary>
        public string Tag
        {
            get { return FormatName(_element.Name); }
            set { _element.Name = ParseName(value); }
        }

        #endregion

        #region Text / Tail

        /// <summary>
        /// Gets or sets the text content before the first child element.
        /// In Python ElementTree, <c>element.text</c> is the text between
        /// the start tag and the first child (or end tag).
        /// </summary>
        public string? Text
        {
            get
            {
                // Walk FirstNode forward, collecting text nodes before the first child element
                XNode? node = _element.FirstNode;
                while (node != null)
                {
                    if (node is XElement)
                    {
                        break;
                    }

                    if (node is XText text)
                    {
                        return text.Value;
                    }

                    node = node.NextNode;
                }

                return null;
            }
            set
            {
                // Remove existing text nodes before the first child element
                XNode? node = _element.FirstNode;
                while (node != null)
                {
                    XNode? next = node.NextNode;
                    if (node is XElement)
                    {
                        break;
                    }

                    if (node is XText)
                    {
                        node.Remove();
                    }

                    node = next;
                }

                if (value != null)
                {
                    // Insert text at the beginning
                    if (_element.FirstNode != null)
                    {
                        _element.FirstNode.AddBeforeSelf(new XText(value));
                    }
                    else
                    {
                        _element.Add(new XText(value));
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the tail text after this element's end tag.
        /// In Python ElementTree, <c>element.tail</c> is the text between
        /// this element's end tag and the next sibling (or parent's end tag).
        /// </summary>
        public string? Tail
        {
            get
            {
                XNode? node = _element.NextNode;
                while (node != null)
                {
                    if (node is XElement)
                    {
                        break;
                    }

                    if (node is XText text)
                    {
                        return text.Value;
                    }

                    node = node.NextNode;
                }

                return null;
            }
            set
            {
                // Remove existing tail text nodes
                XNode? node = _element.NextNode;
                while (node != null)
                {
                    XNode? next = node.NextNode;
                    if (node is XElement)
                    {
                        break;
                    }

                    if (node is XText)
                    {
                        node.Remove();
                        break;
                    }

                    node = next;
                }

                if (value != null)
                {
                    _element.AddAfterSelf(new XText(value));
                }
            }
        }

        #endregion

        #region Attributes

        /// <summary>
        /// Gets a dictionary of the element's attributes.
        /// Modifying the returned dictionary does not affect the element;
        /// use <see cref="Set"/> to change attributes.
        /// </summary>
        public Dict<string, string> Attrib
        {
            get
            {
                var dict = new Dict<string, string>();
                foreach (XAttribute attr in _element.Attributes())
                {
                    dict[FormatName(attr.Name)] = attr.Value;
                }

                return dict;
            }
        }

        /// <summary>
        /// Get the value of an attribute, or a default value if not found.
        /// </summary>
        /// <param name="key">The attribute name.</param>
        /// <param name="default">The default value if the attribute is not found.</param>
        /// <returns>The attribute value, or <paramref name="default"/>.</returns>
        public string? Get(string key, string? @default = null)
        {
            XAttribute? attr = _element.Attribute(ParseName(key));
            return attr != null ? attr.Value : @default;
        }

        /// <summary>Set the value of an attribute.</summary>
        /// <param name="key">The attribute name.</param>
        /// <param name="value">The attribute value.</param>
        public void Set(string key, string value)
        {
            _element.SetAttributeValue(ParseName(key), value);
        }

        /// <summary>Return a list of attribute names.</summary>
        public List<string> Keys()
        {
            var result = new List<string>();
            foreach (XAttribute attr in _element.Attributes())
            {
                result.Append(FormatName(attr.Name));
            }

            return result;
        }

        /// <summary>Return a list of (name, value) tuples for all attributes.</summary>
        public List<(string, string)> Items()
        {
            var result = new List<(string, string)>();
            foreach (XAttribute attr in _element.Attributes())
            {
                result.Append((FormatName(attr.Name), attr.Value));
            }

            return result;
        }

        #endregion

        #region Child Access

        /// <summary>
        /// Get a child element by index, with Python-style negative indexing.
        /// </summary>
        /// <param name="index">The index (negative values count from the end).</param>
        /// <returns>The child element at the given index.</returns>
        /// <exception cref="IndexError">Thrown when the index is out of range.</exception>
        public Element this[int index]
        {
            get
            {
                var children = _element.Elements().ToArray();
                int count = children.Length;
                if (index < 0)
                {
                    index += count;
                }

                if (index < 0 || index >= count)
                {
                    throw new IndexError("child index out of range");
                }

                return new Element(children[index]);
            }
        }

        /// <summary>Return the number of direct child elements.</summary>
        int ISized.Count => _element.Elements().Count();

        /// <summary>Return the number of direct child elements.</summary>
        public int Len()
        {
            return _element.Elements().Count();
        }

        #endregion

        #region IEnumerable<Element>

        /// <summary>Iterate over direct child elements.</summary>
        public IEnumerator<Element> GetEnumerator()
        {
            foreach (XElement child in _element.Elements())
            {
                yield return new Element(child);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region Mutation

        /// <summary>Append a child element.</summary>
        public void Append(Element element)
        {
            if (element == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            _element.Add(element._element);
        }

        /// <summary>Append all elements from the iterable.</summary>
        public void Extend(IEnumerable<Element> elements)
        {
            if (elements == null)
            {
                throw new TypeError("expected iterable, got NoneType");
            }

            foreach (Element el in elements)
            {
                Append(el);
            }
        }

        /// <summary>Insert a child element at the given position.</summary>
        /// <param name="index">The position to insert at.</param>
        /// <param name="element">The element to insert.</param>
        public void Insert(int index, Element element)
        {
            if (element == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            var children = _element.Elements().ToArray();
            if (index >= children.Length)
            {
                _element.Add(element._element);
            }
            else
            {
                if (index < 0)
                {
                    index = System.Math.Max(0, children.Length + index);
                }

                children[index].AddBeforeSelf(element._element);
            }
        }

        /// <summary>Remove a child element.</summary>
        /// <param name="element">The child element to remove.</param>
        /// <exception cref="ValueError">If the element is not a direct child.</exception>
        public void Remove(Element element)
        {
            if (element == null)
            {
                throw new TypeError("expected Element, got NoneType");
            }

            // Find the matching child XElement
            bool found = false;
            foreach (XElement child in _element.Elements())
            {
                if (ReferenceEquals(child, element._element))
                {
                    child.Remove();
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new ValueError("Element.remove(x): x not in element");
            }
        }

        /// <summary>Remove all child elements and text.</summary>
        public void Clear()
        {
            _element.RemoveAll();
        }

        #endregion

        #region Find / Iter

        /// <summary>
        /// Find the first matching child element by path.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>The first matching element, or null if not found.</returns>
        public Element? Find(string path, Dict<string, string>? namespaces = null)
        {
            return XPathMatcher.FindFirst(_element, path, namespaces);
        }

        /// <summary>
        /// Find all matching child elements by path.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>A list of matching elements.</returns>
        public List<Element> FindAll(string path, Dict<string, string>? namespaces = null)
        {
            var result = new List<Element>();
            foreach (Element el in XPathMatcher.FindAll(_element, path, namespaces))
            {
                result.Append(el);
            }

            return result;
        }

        /// <summary>
        /// Find the text content of the first matching child element.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="default">Default value if not found.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>The text content of the matching element, or <paramref name="default"/>.</returns>
        public string? FindText(string path, string? @default = null, Dict<string, string>? namespaces = null)
        {
            Element? el = Find(path, namespaces);
            if (el != null)
            {
                return el.Text ?? @default;
            }

            return @default;
        }

        /// <summary>
        /// Iterate over all descendant elements (and optionally the element itself)
        /// that match the given tag.
        /// </summary>
        /// <param name="tag">Optional tag filter. Null or "*" matches all elements.</param>
        /// <returns>An enumerable of matching elements.</returns>
        public IEnumerable<Element> Iter(string? tag = null)
        {
            bool matchAll = tag == null || tag == "*";

            if (matchAll || Tag == tag)
            {
                yield return this;
            }

            foreach (XElement desc in _element.Descendants())
            {
                Element el = new Element(desc);
                if (matchAll || el.Tag == tag)
                {
                    yield return el;
                }
            }
        }

        /// <summary>
        /// Find all matching elements using an XPath-like expression,
        /// including descendants.
        /// </summary>
        /// <param name="path">An XPath-like path expression.</param>
        /// <param name="namespaces">Optional namespace prefix mapping.</param>
        /// <returns>An enumerable of matching elements.</returns>
        public IEnumerable<Element> IterFind(string path, Dict<string, string>? namespaces = null)
        {
            return XPathMatcher.FindAll(_element, path, namespaces);
        }

        /// <summary>
        /// Iterate over all text content in this element and its descendants.
        /// </summary>
        /// <returns>An enumerable of text strings.</returns>
        public IEnumerable<string> IterText()
        {
            foreach (XNode node in _element.DescendantNodes())
            {
                if (node is XText text)
                {
                    yield return text.Value;
                }
            }
        }

        #endregion

        #region Name Helpers

        /// <summary>
        /// Parse a Python-style <c>{uri}local</c> tag name to an <see cref="XName"/>.
        /// If no namespace is present, returns a local name.
        /// </summary>
        /// <param name="tag">The tag name, optionally with <c>{uri}</c> prefix.</param>
        /// <returns>The parsed <see cref="XName"/>.</returns>
        internal static XName ParseName(string tag)
        {
            if (tag != null && tag.Length > 0 && tag[0] == '{')
            {
                int close = tag.IndexOf('}');
                if (close > 0)
                {
                    string ns = tag.Substring(1, close - 1);
                    string local = tag.Substring(close + 1);
                    return XName.Get(local, ns);
                }
            }

            return XName.Get(tag ?? string.Empty);
        }

        /// <summary>
        /// Format an <see cref="XName"/> as a Python-style <c>{uri}local</c> string.
        /// </summary>
        /// <param name="name">The XName to format.</param>
        /// <returns>The formatted string.</returns>
        internal static string FormatName(XName name)
        {
            if (name.Namespace != XNamespace.None)
            {
                return "{" + name.NamespaceName + "}" + name.LocalName;
            }

            return name.LocalName;
        }

        #endregion

        /// <summary>Return a string representation of the element.</summary>
        public override string ToString()
        {
            return "<Element '" + Tag + "'>";
        }
    }
}
