using System;

namespace Sharpy
{
    public interface IInterpolation
    {
        string BeforeGet(ConfigParser parser, string section, string option, string rawValue);
        string BeforeSet(ConfigParser parser, string section, string option, string value);
    }

    [SharpyModuleType("configparser")]
    public sealed class BasicInterpolation : IInterpolation
    {
        public string BeforeGet(ConfigParser parser, string section, string option, string rawValue)
        {
            return Interpolate(parser, section, option, rawValue, 10);
        }

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
                    // %% escape — replace with single % and skip
                    result = result.Substring(0, idx) + result.Substring(idx + 1);
                    startIdx = idx + 1;
                    continue;
                }

                if (next != '(')
                {
                    throw new InterpolationSyntaxError(option, section,
                        "'%' must be followed by '%' or '(', found: '" + next + "'");
                }

                // We have %(
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

            if (result.IndexOf("%(", StringComparison.Ordinal) >= 0)
            {
                return Interpolate(parser, section, option, result, depth - 1);
            }

            return result;
        }
    }

    [SharpyModuleType("configparser")]
    public sealed class ExtendedInterpolation : IInterpolation
    {
        public string BeforeGet(ConfigParser parser, string section, string option, string rawValue)
        {
            return Interpolate(parser, section, option, rawValue, 10);
        }

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
