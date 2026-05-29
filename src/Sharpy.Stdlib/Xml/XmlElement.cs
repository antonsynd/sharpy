using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible Element class wrapping <see cref="XElement"/>.
    /// Represents an XML element with tag, text, tail, and attributes.
    /// </summary>
    public sealed class XmlElement : IEnumerable<XmlElement>
    {
        private readonly XElement _element;

        internal XmlElement(XElement element)
        {
            _element = element ?? throw new ArgumentNullException(nameof(element));
        }

        internal XElement Underlying => _element;

        /// <summary>
        /// The element's tag name. Includes namespace in {uri}tag format if present.
        /// </summary>
        public string Tag
        {
            get
            {
                XName name = _element.Name;
                if (string.IsNullOrEmpty(name.NamespaceName))
                {
                    return name.LocalName;
                }

                return "{" + name.NamespaceName + "}" + name.LocalName;
            }
            set
            {
                if (value == null)
                {
                    throw new TypeError("tag cannot be None");
                }

                _element.Name = ParseName(value);
            }
        }

        /// <summary>
        /// The text content of the element (text before the first child).
        /// </summary>
        public string? Text
        {
            get
            {
                XNode? first = _element.FirstNode;
                while (first != null)
                {
                    if (first is XText t)
                    {
                        return t.Value;
                    }

                    if (first is XElement)
                    {
                        break;
                    }

                    first = first.NextNode;
                }

                return null;
            }
            set
            {
                // Remove existing text nodes before first element child
                XNode? node = _element.FirstNode;
                while (node != null)
                {
                    XNode? next = node.NextNode;
                    if (node is XText)
                    {
                        node.Remove();
                        break;
                    }

                    if (node is XElement)
                    {
                        break;
                    }

                    node = next;
                }

                if (value != null)
                {
                    XNode? firstChild = _element.FirstNode;
                    if (firstChild != null)
                    {
                        firstChild.AddBeforeSelf(new XText(value));
                    }
                    else
                    {
                        _element.Add(new XText(value));
                    }
                }
            }
        }

        /// <summary>
        /// The text after this element's end tag and before the next sibling.
        /// </summary>
        public string? Tail
        {
            get
            {
                XNode? next = _element.NextNode;
                if (next is XText t)
                {
                    return t.Value;
                }

                return null;
            }
            set
            {
                XNode? next = _element.NextNode;
                if (next is XText t)
                {
                    if (value != null)
                    {
                        t.Value = value;
                    }
                    else
                    {
                        t.Remove();
                    }
                }
                else if (value != null)
                {
                    _element.AddAfterSelf(new XText(value));
                }
            }
        }

        /// <summary>
        /// Dictionary of the element's attributes.
        /// </summary>
        public Dict<string, string> Attrib
        {
            get
            {
                Dict<string, string> result = new Dict<string, string>();
                foreach (XAttribute attr in _element.Attributes())
                {
                    string key;
                    if (string.IsNullOrEmpty(attr.Name.NamespaceName))
                    {
                        key = attr.Name.LocalName;
                    }
                    else
                    {
                        key = "{" + attr.Name.NamespaceName + "}" + attr.Name.LocalName;
                    }

                    result[key] = attr.Value;
                }

                return result;
            }
        }

        /// <summary>
        /// Get the value of an attribute.
        /// </summary>
        /// <param name="key">The attribute name.</param>
        /// <param name="default">Default value if attribute not found.</param>
        /// <returns>The attribute value or the default.</returns>
        public string? Get(string key, string? @default = null)
        {
            XName name = ParseName(key);
            XAttribute? attr = _element.Attribute(name);
            if (attr == null)
            {
                return @default;
            }

            return attr.Value;
        }

        /// <summary>
        /// Set an attribute value.
        /// </summary>
        /// <param name="key">The attribute name.</param>
        /// <param name="value">The attribute value.</param>
        public void Set(string key, string value)
        {
            XName name = ParseName(key);
            _element.SetAttributeValue(name, value);
        }

        /// <summary>
        /// Returns the number of direct child elements.
        /// </summary>
        public int Len()
        {
            int count = 0;
            foreach (XElement _ in _element.Elements())
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Append a child element.
        /// </summary>
        /// <param name="element">The element to append.</param>
        public void Append(XmlElement element)
        {
            if (element == null)
            {
                throw new TypeError("argument must be an Element, not NoneType");
            }

            _element.Add(element._element);
        }

        /// <summary>
        /// Insert a child element at the given position.
        /// </summary>
        /// <param name="index">Position to insert at.</param>
        /// <param name="element">The element to insert.</param>
        public void Insert(int index, XmlElement element)
        {
            if (element == null)
            {
                throw new TypeError("argument must be an Element, not NoneType");
            }

            System.Collections.Generic.List<XElement> children =
                new System.Collections.Generic.List<XElement>(_element.Elements());

            if (index >= children.Count)
            {
                _element.Add(element._element);
            }
            else
            {
                if (index < 0)
                {
                    index = children.Count + index;
                    if (index < 0) index = 0;
                }

                children[index].AddBeforeSelf(element._element);
            }
        }

        /// <summary>
        /// Remove a child element.
        /// </summary>
        /// <param name="element">The element to remove.</param>
        public void Remove(XmlElement element)
        {
            if (element == null)
            {
                throw new TypeError("argument must be an Element, not NoneType");
            }

            element._element.Remove();
        }

        /// <summary>
        /// Find the first child element matching the given path.
        /// </summary>
        /// <param name="path">Simple tag name or path expression.</param>
        /// <returns>The first matching element, or null.</returns>
        public XmlElement? Find(string path)
        {
            XElement? found = XPathHelper.FindFirst(_element, path);
            return found != null ? new XmlElement(found) : null;
        }

        /// <summary>
        /// Find all matching elements.
        /// </summary>
        /// <param name="path">Simple tag name or path expression.</param>
        /// <returns>A list of matching elements.</returns>
        public List<XmlElement> Findall(string path)
        {
            List<XmlElement> result = new List<XmlElement>();
            foreach (XElement e in XPathHelper.FindAll(_element, path))
            {
                result.Append(new XmlElement(e));
            }

            return result;
        }

        /// <summary>
        /// Iterate over matching elements.
        /// </summary>
        /// <param name="path">Simple tag name or path expression.</param>
        /// <returns>An iterator of matching elements.</returns>
        public IEnumerable<XmlElement> Iterfind(string path)
        {
            foreach (XElement e in XPathHelper.FindAll(_element, path))
            {
                yield return new XmlElement(e);
            }
        }

        /// <summary>
        /// Create an iterator that iterates over this element and all elements
        /// in the tree below it (depth first).
        /// </summary>
        /// <param name="tag">Optional tag filter.</param>
        /// <returns>An iterator over matching elements.</returns>
        public IEnumerable<XmlElement> Iter(string? tag = null)
        {
            if (tag == null || tag == "*" || tag == Tag)
            {
                yield return this;
            }

            foreach (XElement desc in _element.Descendants())
            {
                XmlElement wrapped = new XmlElement(desc);
                if (tag == null || tag == "*" || wrapped.Tag == tag)
                {
                    yield return wrapped;
                }
            }
        }

        /// <summary>
        /// Iterate over direct child elements.
        /// </summary>
        /// <returns>An enumerator of child elements.</returns>
        public IEnumerator<XmlElement> GetEnumerator()
        {
            foreach (XElement child in _element.Elements())
            {
                yield return new XmlElement(child);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Parse a potentially namespaced name in {uri}local format.
        /// </summary>
        internal static XName ParseName(string name)
        {
            if (name.StartsWith("{"))
            {
                int end = name.IndexOf('}');
                if (end > 0)
                {
                    string ns = name.Substring(1, end - 1);
                    string local = name.Substring(end + 1);
                    return XName.Get(local, ns);
                }
            }

            return XName.Get(name);
        }
    }
}
