using System;

namespace Sharpy
{
    /// <summary>Parses and stores INI-style configuration data.</summary>
    [SharpyModuleType("configparser")]
    public sealed partial class ConfigParser
    {
        private const string DefaultSectionName = "DEFAULT";

        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string?>> _sections =
            new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string?>>(StringComparer.Ordinal);

        private readonly System.Collections.Generic.Dictionary<string, string?> _defaults =
            new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        private readonly IInterpolation _interpolation;
        private readonly bool _allowNoValue;

        /// <summary>Initializes a new config parser.</summary>
        public ConfigParser(IInterpolation? interpolation = null, bool allowNoValue = false)
        {
            _interpolation = interpolation ?? new BasicInterpolation();
            _allowNoValue = allowNoValue;
        }

        /// <summary>Gets a proxy for the named section.</summary>
        public SectionProxy this[string section]
        {
            get
            {
                if (!string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase)
                    && !_sections.ContainsKey(section))
                {
                    throw new NoSectionError(section);
                }
                return new SectionProxy(this, section);
            }
        }

        /// <summary>Returns the non-default section names.</summary>
        public List<string> Sections()
        {
            return new List<string>(new System.Collections.Generic.List<string>(_sections.Keys));
        }

        /// <summary>Adds a new section.</summary>
        public void AddSection(string section)
        {
            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValueError("Invalid section name: '" + section + "'");
            }
            if (_sections.ContainsKey(section))
            {
                throw new DuplicateSectionError(section);
            }
            _sections[section] = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Determines whether a non-default section exists.</summary>
        public bool HasSection(string section)
        {
            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return _sections.ContainsKey(section);
        }

        /// <summary>Removes a non-default section.</summary>
        public bool RemoveSection(string section)
        {
            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return _sections.Remove(section);
        }

        /// <summary>Gets an option value, optionally applying interpolation.</summary>
        public string? Get(string section, string option, string? fallback = null, bool raw = false)
        {
            string normalizedOption = option.ToLowerInvariant();

            if (!string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase)
                && !_sections.ContainsKey(section))
            {
                if (fallback != null)
                {
                    return fallback;
                }
                throw new NoSectionError(section);
            }

            string? rawValue = GetRaw(section, normalizedOption);

            if (rawValue == null)
            {
                // A null raw value is ambiguous: the option may be genuinely absent,
                // or present with no value (allow_no_value=True). Python returns None
                // for the latter and raises NoOptionError only for the former.
                if (OptionExists(section, normalizedOption))
                {
                    return null;
                }
                if (fallback != null)
                {
                    return fallback;
                }
                throw new NoOptionError(option, section);
            }

            if (raw)
            {
                return rawValue;
            }

            return _interpolation.BeforeGet(this, section, normalizedOption, rawValue);
        }

        /// <summary>Gets an option as an integer.</summary>
        public int GetInt(string section, string option, int? fallback = null)
        {
            string? value = null;
            try
            {
                value = Get(section, option);
            }
            catch (ConfigparserError) when (fallback != null)
            {
                return fallback.Value;
            }

            if (!int.TryParse(value, out int result))
            {
                throw new ValueError("invalid literal for int(): '" + value + "'");
            }
            return result;
        }

        /// <summary>Gets an option as a floating-point number.</summary>
        public double GetFloat(string section, string option, double? fallback = null)
        {
            string? value = null;
            try
            {
                value = Get(section, option);
            }
            catch (ConfigparserError) when (fallback != null)
            {
                return fallback.Value;
            }

            if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double result))
            {
                throw new ValueError("invalid literal for float(): '" + value + "'");
            }
            return result;
        }

        /// <summary>Gets an option as a boolean using configparser truth values.</summary>
        public bool GetBoolean(string section, string option, bool? fallback = null)
        {
            string? value = null;
            try
            {
                value = Get(section, option);
            }
            catch (ConfigparserError) when (fallback != null)
            {
                return fallback.Value;
            }

            if (value == null)
            {
                throw new ValueError("Not a boolean: null");
            }

            switch (value.ToLowerInvariant())
            {
                case "1":
                case "yes":
                case "true":
                case "on":
                    return true;
                case "0":
                case "no":
                case "false":
                case "off":
                    return false;
                default:
                    throw new ValueError("Not a boolean: " + value);
            }
        }

        /// <summary>Sets an option value in a section.</summary>
        public void Set(string section, string option, string value)
        {
            string normalizedOption = option.ToLowerInvariant();

            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                _defaults[normalizedOption] = value;
                return;
            }
            if (!_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }
            _sections[section][normalizedOption] = value;
        }

        /// <summary>Determines whether a section or defaults contain an option.</summary>
        public bool HasOption(string section, string option)
        {
            string normalizedOption = option.ToLowerInvariant();

            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return _defaults.ContainsKey(normalizedOption);
            }
            if (!_sections.ContainsKey(section))
            {
                return false;
            }
            return _sections[section].ContainsKey(normalizedOption) || _defaults.ContainsKey(normalizedOption);
        }

        /// <summary>Removes an option from a section or from defaults.</summary>
        public bool RemoveOption(string section, string option)
        {
            string normalizedOption = option.ToLowerInvariant();

            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return _defaults.Remove(normalizedOption);
            }
            if (!_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }
            return _sections[section].Remove(normalizedOption);
        }

        /// <summary>Returns the option names available in a section.</summary>
        public List<string> Options(string section)
        {
            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>(new System.Collections.Generic.List<string>(_defaults.Keys));
            }
            if (!_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }

            var result = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _defaults.Keys)
            {
                result.Add(key);
            }
            foreach (var key in _sections[section].Keys)
            {
                result.Add(key);
            }
            return new List<string>(new System.Collections.Generic.List<string>(result));
        }

        /// <summary>Returns the section items with defaults applied.</summary>
        public Dict<string, string> Items(string section)
        {
            if (!string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase)
                && !_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }

            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _defaults)
            {
                if (kvp.Value != null)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            if (!string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase)
                && _sections.ContainsKey(section))
            {
                foreach (var kvp in _sections[section])
                {
                    if (kvp.Value != null)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }
            }

            return new Dict<string, string>(result);
        }

        /// <summary>Returns the default section values.</summary>
        public Dict<string, string> Defaults()
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _defaults)
            {
                if (kvp.Value != null)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return new Dict<string, string>(result);
        }

        // Whether the option key is present (in the section or DEFAULT), regardless of
        // whether its stored value is null. Distinguishes allow_no_value keys from absent keys.
        private bool OptionExists(string section, string option)
        {
            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                return _defaults.ContainsKey(option);
            }
            if (_sections.TryGetValue(section, out var sectionDict) && sectionDict.ContainsKey(option))
            {
                return true;
            }
            return _defaults.ContainsKey(option);
        }

        private string? GetRaw(string section, string option)
        {
            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                if (_defaults.TryGetValue(option, out string? defVal))
                {
                    return defVal;
                }
                return null;
            }

            if (_sections.TryGetValue(section, out var sectionDict))
            {
                if (sectionDict.TryGetValue(option, out string? val))
                {
                    return val;
                }
            }

            if (_defaults.TryGetValue(option, out string? defaultVal))
            {
                return defaultVal;
            }

            return null;
        }
    }
}
