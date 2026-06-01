using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Internal helper that translates Python ElementTree XPath-like patterns
    /// to LINQ queries on XElement trees.
    /// </summary>
    /// <remarks>
    /// Supported patterns:
    /// <list type="bullet">
    /// <item><c>tag</c> — direct children with matching tag</item>
    /// <item><c>{ns}tag</c> — direct children with namespace-qualified tag</item>
    /// <item><c>.</c> — the current element</item>
    /// <item><c>*</c> — all direct children</item>
    /// <item><c>.//tag</c> — descendants with matching tag</item>
    /// <item><c>./tag</c> — direct children with matching tag</item>
    /// <item><c>[@attrib]</c> — elements with the given attribute</item>
    /// <item><c>[@attrib='value']</c> — elements with attribute matching value</item>
    /// <item><c>[tag]</c> — elements that have a child with the given tag</item>
    /// <item><c>[position]</c> — element at 1-based position</item>
    /// </list>
    /// </remarks>
    internal static class XPathMatcher
    {
        /// <summary>Find all elements matching the path expression.</summary>
        internal static IEnumerable<Element> FindAll(XElement root, string path, Dict<string, string>? namespaces)
        {
            if (path == null)
            {
                throw new TypeError("expected str, got NoneType");
            }

            if (path == ".")
            {
                return new Element[] { new Element(root) };
            }

            if (path == "*")
            {
                return WrapElements(root.Elements());
            }

            // Descendant pattern: .//tag or .//
            if (path.StartsWith(".//"))
            {
                string tagPart = path.Substring(3);
                return FindDescendants(root, tagPart, namespaces);
            }

            // Child pattern: ./tag
            if (path.StartsWith("./"))
            {
                string tagPart = path.Substring(2);
                return FindChildren(root, tagPart, namespaces);
            }

            // Check for predicate: tag[predicate]
            int bracketStart = path.IndexOf('[');
            if (bracketStart >= 0)
            {
                return ApplyPredicate(root, path, bracketStart, namespaces);
            }

            // Simple tag match against direct children
            return FindChildren(root, path, namespaces);
        }

        /// <summary>Find the first element matching the path expression, or null.</summary>
        internal static Element? FindFirst(XElement root, string path, Dict<string, string>? namespaces)
        {
            foreach (Element el in FindAll(root, path, namespaces))
            {
                return el;
            }

            return null;
        }

        private static IEnumerable<Element> FindDescendants(XElement root, string tagPart, Dict<string, string>? namespaces)
        {
            if (string.IsNullOrEmpty(tagPart) || tagPart == "*")
            {
                return WrapElements(root.Descendants());
            }

            XName name = ResolveName(tagPart, namespaces);
            return WrapElements(root.Descendants(name));
        }

        private static IEnumerable<Element> FindChildren(XElement root, string tagPart, Dict<string, string>? namespaces)
        {
            if (string.IsNullOrEmpty(tagPart) || tagPart == "*")
            {
                return WrapElements(root.Elements());
            }

            XName name = ResolveName(tagPart, namespaces);
            return WrapElements(root.Elements(name));
        }

        private static IEnumerable<Element> ApplyPredicate(XElement root, string path, int bracketStart, Dict<string, string>? namespaces)
        {
            string tagPart = bracketStart > 0 ? path.Substring(0, bracketStart) : "";
            int bracketEnd = path.IndexOf(']', bracketStart);
            if (bracketEnd < 0)
            {
                throw new ParseError("unbalanced '[' in path: " + path);
            }

            string predicate = path.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            // Get candidate elements
            IEnumerable<XElement> candidates;
            if (string.IsNullOrEmpty(tagPart) || tagPart == "*")
            {
                candidates = root.Elements();
            }
            else if (tagPart.StartsWith(".//"))
            {
                string innerTag = tagPart.Substring(3);
                if (string.IsNullOrEmpty(innerTag) || innerTag == "*")
                {
                    candidates = root.Descendants();
                }
                else
                {
                    XName name = ResolveName(innerTag, namespaces);
                    candidates = root.Descendants(name);
                }
            }
            else
            {
                XName name = ResolveName(tagPart, namespaces);
                candidates = root.Elements(name);
            }

            // Apply predicate filter
            if (predicate.StartsWith("@"))
            {
                return ApplyAttributePredicate(candidates, predicate.Substring(1));
            }

            // Positional predicate
            if (int.TryParse(predicate, out int position))
            {
                return ApplyPositionPredicate(candidates, position);
            }

            // Child tag predicate: [tag]
            return ApplyChildTagPredicate(candidates, predicate, namespaces);
        }

        private static IEnumerable<Element> ApplyAttributePredicate(IEnumerable<XElement> candidates, string attrExpr)
        {
            // [@attrib='value'] or [@attrib]
            int eqIndex = attrExpr.IndexOf('=');
            if (eqIndex >= 0)
            {
                string attrName = attrExpr.Substring(0, eqIndex);
                string attrValue = attrExpr.Substring(eqIndex + 1);
                // Strip quotes
                if (attrValue.Length >= 2 &&
                    ((attrValue[0] == '\'' && attrValue[attrValue.Length - 1] == '\'') ||
                     (attrValue[0] == '"' && attrValue[attrValue.Length - 1] == '"')))
                {
                    attrValue = attrValue.Substring(1, attrValue.Length - 2);
                }

                XName name = Element.ParseName(attrName);
                foreach (XElement el in candidates)
                {
                    XAttribute? attr = el.Attribute(name);
                    if (attr != null && attr.Value == attrValue)
                    {
                        yield return new Element(el);
                    }
                }
            }
            else
            {
                XName name = Element.ParseName(attrExpr);
                foreach (XElement el in candidates)
                {
                    if (el.Attribute(name) != null)
                    {
                        yield return new Element(el);
                    }
                }
            }
        }

        private static IEnumerable<Element> ApplyPositionPredicate(IEnumerable<XElement> candidates, int position)
        {
            // 1-based indexing like Python ElementTree
            int current = 1;
            foreach (XElement el in candidates)
            {
                if (current == position)
                {
                    yield return new Element(el);
                    yield break;
                }

                current++;
            }
        }

        private static IEnumerable<Element> ApplyChildTagPredicate(IEnumerable<XElement> candidates, string childTag, Dict<string, string>? namespaces)
        {
            XName name = ResolveName(childTag, namespaces);
            foreach (XElement el in candidates)
            {
                if (el.Element(name) != null)
                {
                    yield return new Element(el);
                }
            }
        }

        private static XName ResolveName(string tag, Dict<string, string>? namespaces)
        {
            // Check for namespace prefix: prefix:local
            if (namespaces is object)
            {
                int colonIndex = tag.IndexOf(':');
                if (colonIndex > 0)
                {
                    string prefix = tag.Substring(0, colonIndex);
                    string local = tag.Substring(colonIndex + 1);
                    try
                    {
                        string uri = namespaces[prefix];
                        return XName.Get(local, uri);
                    }
                    catch (KeyError)
                    {
                        throw new ParseError("prefix '" + prefix + "' not found in prefix map");
                    }
                }
            }

            return Element.ParseName(tag);
        }

        private static IEnumerable<Element> WrapElements(IEnumerable<XElement> elements)
        {
            foreach (XElement el in elements)
            {
                yield return new Element(el);
            }
        }
    }
}
