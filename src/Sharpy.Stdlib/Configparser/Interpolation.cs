using System;

namespace Sharpy
{
    /// <summary>
    /// Base class for configparser interpolation handlers.
    /// </summary>
    public abstract class ConfigInterpolation
    {
        /// <summary>
        /// Interpolate a value before returning it.
        /// </summary>
        /// <param name="parser">The parser instance.</param>
        /// <param name="section">The section name.</param>
        /// <param name="option">The option name.</param>
        /// <param name="rawValue">The raw value from the config file.</param>
        /// <returns>The interpolated value.</returns>
        public abstract string BeforeGet(ConfigParserInstance parser, string section, string option, string rawValue);
    }

    /// <summary>
    /// Basic interpolation using %(name)s syntax.
    /// Values can contain format strings which refer to other values in the same section,
    /// or values in the DEFAULT section.
    /// </summary>
    public class BasicInterpolation : ConfigInterpolation
    {
        /// <inheritdoc/>
        public override string BeforeGet(ConfigParserInstance parser, string section, string option, string rawValue)
        {
            return Interpolate(parser, section, rawValue, maxDepth: 10);
        }

        private static string Interpolate(ConfigParserInstance parser, string section, string rawValue, int maxDepth)
        {
            if (maxDepth <= 0)
            {
                throw new InterpolationDepthError(section, rawValue);
            }

            int startIdx = 0;
            var result = rawValue;

            while (true)
            {
                int idx = result.IndexOf("%(", startIdx, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                int endIdx = result.IndexOf(")s", idx + 2, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    break;
                }

                string key = result.Substring(idx + 2, endIdx - idx - 2);
                string replacement = parser.Get(section, key, raw: true, fallback: null)
                    ?? throw new InterpolationMissingOptionError(section, rawValue, key);

                result = result.Substring(0, idx) + replacement + result.Substring(endIdx + 2);
                startIdx = idx + replacement.Length;
            }

            // Recurse if there are still interpolation markers
            if (result.IndexOf("%(", StringComparison.Ordinal) >= 0)
            {
                return Interpolate(parser, section, result, maxDepth - 1);
            }

            return result;
        }
    }

    /// <summary>
    /// Extended interpolation using ${section:option} syntax.
    /// Values can reference other sections using ${section:option} or same-section
    /// values using ${option}.
    /// </summary>
    public class ExtendedInterpolation : ConfigInterpolation
    {
        /// <inheritdoc/>
        public override string BeforeGet(ConfigParserInstance parser, string section, string option, string rawValue)
        {
            return Interpolate(parser, section, rawValue, maxDepth: 10);
        }

        private static string Interpolate(ConfigParserInstance parser, string section, string rawValue, int maxDepth)
        {
            if (maxDepth <= 0)
            {
                throw new InterpolationDepthError(section, rawValue);
            }

            int startIdx = 0;
            var result = rawValue;

            while (true)
            {
                int idx = result.IndexOf("${", startIdx, StringComparison.Ordinal);
                if (idx < 0)
                {
                    break;
                }

                int endIdx = result.IndexOf("}", idx + 2, StringComparison.Ordinal);
                if (endIdx < 0)
                {
                    break;
                }

                string reference = result.Substring(idx + 2, endIdx - idx - 2);
                string refSection;
                string refOption;

                int colonIdx = reference.IndexOf(':');
                if (colonIdx >= 0)
                {
                    refSection = reference.Substring(0, colonIdx);
                    refOption = reference.Substring(colonIdx + 1);
                }
                else
                {
                    refSection = section;
                    refOption = reference;
                }

                string replacement = parser.Get(refSection, refOption, raw: true, fallback: null)
                    ?? throw new InterpolationMissingOptionError(section, rawValue, refOption);

                result = result.Substring(0, idx) + replacement + result.Substring(endIdx + 1);
                startIdx = idx + replacement.Length;
            }

            // Recurse if there are still interpolation markers
            if (result.IndexOf("${", StringComparison.Ordinal) >= 0)
            {
                return Interpolate(parser, section, result, maxDepth - 1);
            }

            return result;
        }
    }
}
