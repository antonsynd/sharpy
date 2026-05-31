using System;

namespace Sharpy
{
    /// <summary>Defines configparser value interpolation hooks.</summary>
    public interface IInterpolation
    {
        /// <summary>Interpolates a value before it is returned to the caller.</summary>
        string BeforeGet(ConfigParser parser, string section, string option, string rawValue);

        /// <summary>Normalizes a value before it is stored in the parser.</summary>
        string BeforeSet(ConfigParser parser, string section, string option, string value);
    }

    /// <summary>Implements configparser.BasicInterpolation using %(name)s substitutions.</summary>
    [SharpyModuleType("configparser")]
    public sealed class BasicInterpolation : IInterpolation
    {
        /// <summary>Interpolates %(name)s references in an option value.</summary>
        public string BeforeGet(ConfigParser parser, string section, string option, string rawValue)
        {
            var result = Interpolate(parser, section, option, rawValue, 10);
#pragma warning disable CA1307
            return result.Replace("%%", "%");
#pragma warning restore CA1307
        }

        /// <summary>Returns the value unchanged before storing it.</summary>
        public string BeforeSet(ConfigParser parser, string section, string option, string value)
        {
            return value;
        }

        private static string Interpolate(ConfigParser parser, string section, string option, string rawValue, int depth)
        {
            if (depth <= 0)
            {
                throw new InterpolationDepthError(option, section, rawValue);
            }

            var result = rawValue;
            int startIdx = 0;

            while (startIdx < result.Length)
            {
                int idx = result.IndexOf('%', startIdx);
                if (idx < 0 || idx >= result.Length - 1)
                    break;

                char next = result[idx + 1];
                if (next == '%')
                {
                    startIdx = idx + 2;
                    continue;
                }

                if (next != '(')
                {
                    throw new InterpolationSyntaxError(option, section,
                        "'%' must be followed by '%' or '(', found: '" + next + "'");
                }

                int endIdx = result.IndexOf(")s", idx + 2, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    throw new InterpolationSyntaxError(option, section,
                        "bad interpolation variable reference '" + result.Substring(idx) + "'");
                }

                string key = result.Substring(idx + 2, endIdx - idx - 2);
                string? replacement;
                try
                {
                    replacement = parser.Get(section, key, raw: true);
                }
                catch (ConfigparserError)
                {
                    replacement = null;
                }
                if (replacement == null)
                {
                    throw new InterpolationMissingOptionError(option, section, rawValue, key);
                }

                result = result.Substring(0, idx) + replacement + result.Substring(endIdx + 2);
                startIdx = idx + replacement.Length;
            }

            if (HasUnescapedInterpolation(result))
            {
                return Interpolate(parser, section, option, result, depth - 1);
            }

            return result;
        }

        private static bool HasUnescapedInterpolation(string s)
        {
            int idx = 0;
            while (idx < s.Length - 1)
            {
                int pos = s.IndexOf('%', idx);
                if (pos < 0 || pos >= s.Length - 1)
                    return false;
                if (s[pos + 1] == '%')
                {
                    idx = pos + 2;
                    continue;
                }
                if (s[pos + 1] == '(')
                    return true;
                idx = pos + 1;
            }
            return false;
        }
    }

    /// <summary>Implements configparser.ExtendedInterpolation using ${section:option} substitutions.</summary>
    [SharpyModuleType("configparser")]
    public sealed class ExtendedInterpolation : IInterpolation
    {
        /// <summary>Interpolates ${section:option} references in an option value.</summary>
        public string BeforeGet(ConfigParser parser, string section, string option, string rawValue)
        {
            return Interpolate(parser, section, option, rawValue, 10);
        }

        /// <summary>Returns the value unchanged before storing it.</summary>
        public string BeforeSet(ConfigParser parser, string section, string option, string value)
        {
            return value;
        }

        private static string Interpolate(ConfigParser parser, string section, string option, string rawValue, int depth)
        {
            if (depth <= 0)
            {
                throw new InterpolationDepthError(option, section, rawValue);
            }

            var result = rawValue;
            int startIdx = 0;

            while (true)
            {
                int idx = result.IndexOf("${", startIdx, StringComparison.Ordinal);
                if (idx < 0)
                    break;

                int endIdx = result.IndexOf("}", idx + 2, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    throw new InterpolationSyntaxError(option, section,
                        "bad interpolation variable reference '" + result.Substring(idx) + "'");
                }

                string reference = result.Substring(idx + 2, endIdx - idx - 2);
                string refSection;
                string refOption;

                int colonIdx = reference.IndexOf(':');
                if (colonIdx >= 0)
                {
                    // Check for multiple colons
                    if (reference.IndexOf(':', colonIdx + 1) >= 0)
                    {
                        throw new InterpolationSyntaxError(option, section,
                            "More than one ':' found in interpolation variable reference '" + reference + "'");
                    }

                    refSection = reference.Substring(0, colonIdx);
                    refOption = reference.Substring(colonIdx + 1);
                }
                else
                {
                    refSection = section;
                    refOption = reference;
                }

                string? replacement;
                try
                {
                    replacement = parser.Get(refSection, refOption, raw: true);
                }
                catch (ConfigparserError)
                {
                    replacement = null;
                }
                if (replacement == null)
                {
                    throw new InterpolationMissingOptionError(option, section, rawValue, refOption);
                }

                result = result.Substring(0, idx) + replacement + result.Substring(endIdx + 1);
                startIdx = idx + replacement.Length;
            }

            if (result.IndexOf("${", StringComparison.Ordinal) >= 0)
            {
                return Interpolate(parser, section, option, result, depth - 1);
            }

            return result;
        }
    }
}
