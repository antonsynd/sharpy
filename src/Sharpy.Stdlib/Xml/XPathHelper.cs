using System.Collections.Generic;
using System.Xml.Linq;

namespace Sharpy
{
    /// <summary>
    /// Helper for simple XPath-like path expressions.
    /// Supports: tag, ./tag, .//tag, */tag, and attribute predicates [@attr='value'].
    /// </summary>
    internal static class XPathHelper
    {
        internal static XElement? FindFirst(XElement element, string path)
        {
            foreach (XElement e in FindAll(element, path))
            {
                return e;
            }

            return null;
        }

        internal static IEnumerable<XElement> FindAll(XElement _element, string path)
        {
            if (path == null)
            {
                yield break;
            }

            // Handle .// prefix (all descendants)
            if (path.StartsWith(".//"))
            {
                string rest = path.Substring(3);
                ParseTagAndPredicate(rest, out string tag, out string? attrName, out string? attrValue);
                foreach (XElement desc in _element.Descendants())
                {
                    if (MatchesTag(desc, tag) && MatchesPredicate(desc, attrName, attrValue))
                    {
                        yield return desc;
                    }
                }

                yield break;
            }

            // Handle ./ prefix (direct children)
            if (path.StartsWith("./"))
            {
                path = path.Substring(2);
            }

            // Handle */ prefix (grandchildren)
            if (path.StartsWith("*/"))
            {
                string rest = path.Substring(2);
                ParseTagAndPredicate(rest, out string tag, out string? attrName, out string? attrValue);
                foreach (XElement child in _element.Elements())
                {
                    foreach (XElement grandchild in child.Elements())
                    {
                        if (MatchesTag(grandchild, tag) && MatchesPredicate(grandchild, attrName, attrValue))
                        {
                            yield return grandchild;
                        }
                    }
                }

                yield break;
            }

            // Simple tag match (direct children)
            {
                ParseTagAndPredicate(path, out string tag, out string? attrName, out string? attrValue);
                foreach (XElement child in _element.Elements())
                {
                    if (MatchesTag(child, tag) && MatchesPredicate(child, attrName, attrValue))
                    {
                        yield return child;
                    }
                }
            }
        }

        private static bool MatchesTag(XElement element, string tag)
        {
            if (tag == "*") return true;

            XName expected = XmlElement.ParseName(tag);
            return element.Name == expected;
        }

        private static bool MatchesPredicate(XElement element, string? attrName, string? attrValue)
        {
            if (attrName == null) return true;

            XName name = XmlElement.ParseName(attrName);
            XAttribute? attr = element.Attribute(name);
            if (attr == null) return false;
            if (attrValue == null) return true;
            return attr.Value == attrValue;
        }

        private static void ParseTagAndPredicate(
            string expr,
            out string tag,
            out string? attrName,
            out string? attrValue)
        {
            attrName = null;
            attrValue = null;

            int bracketStart = expr.IndexOf('[');
            if (bracketStart < 0)
            {
                tag = expr;
                return;
            }

            tag = expr.Substring(0, bracketStart);
            int bracketEnd = expr.IndexOf(']', bracketStart);
            if (bracketEnd < 0)
            {
                return;
            }

            string predicate = expr.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            // [@attr='value'] or [@attr]
            if (predicate.StartsWith("@"))
            {
                predicate = predicate.Substring(1);
                int eqIdx = predicate.IndexOf('=');
                if (eqIdx < 0)
                {
                    attrName = predicate;
                }
                else
                {
                    attrName = predicate.Substring(0, eqIdx);
                    string val = predicate.Substring(eqIdx + 1);
                    // Strip quotes
                    if (val.Length >= 2 &&
                        ((val[0] == '\'' && val[val.Length - 1] == '\'') ||
                         (val[0] == '"' && val[val.Length - 1] == '"')))
                    {
                        val = val.Substring(1, val.Length - 2);
                    }

                    attrValue = val;
                }
            }
        }
    }
}
