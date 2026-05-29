using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// A section proxy that provides dict-like access to a section's options.
    /// </summary>
    public class SectionProxy
    {
        private readonly ConfigParserInstance _parser;
        private readonly string _section;

        internal SectionProxy(ConfigParserInstance parser, string section)
        {
            _parser = parser;
            _section = section;
        }

        /// <summary>
        /// Get a value by key from this section.
        /// </summary>
        /// <param name="key">The option key.</param>
        /// <returns>The option value.</returns>
        public string this[string key]
        {
            get
            {
                string? value = _parser.Get(_section, key);
                if (value == null)
                {
                    throw new NoOptionError(key, _section);
                }
                return value;
            }
            set
            {
                _parser.Set(_section, key, value);
            }
        }

        /// <summary>
        /// Get a value by key, returning a fallback if not found.
        /// </summary>
        public string? Get(string key, string? fallback = null)
        {
            return _parser.Get(_section, key, fallback: fallback);
        }

        /// <summary>
        /// Get a value as an integer.
        /// </summary>
        public int GetInt(string key, int fallback = 0)
        {
            return _parser.GetInt(_section, key, fallback);
        }

        /// <summary>
        /// Get a value as a float.
        /// </summary>
        public double GetFloat(string key, double fallback = 0.0)
        {
            return _parser.GetFloat(_section, key, fallback);
        }

        /// <summary>
        /// Get a value as a boolean.
        /// </summary>
        public bool GetBoolean(string key, bool fallback = false)
        {
            return _parser.GetBoolean(_section, key, fallback);
        }

        /// <summary>
        /// Return the list of option keys in this section.
        /// </summary>
        public List<string> Keys()
        {
            return _parser.Options(_section);
        }
    }

    /// <summary>
    /// Provides a ConfigParser for reading/writing INI-style configuration files,
    /// matching Python's configparser.ConfigParser API.
    /// </summary>
    public class ConfigParserInstance
    {
        private const string DefaultSection = "DEFAULT";

        private readonly Dictionary<string, Dictionary<string, string>> _sections;
        private readonly Dictionary<string, string> _defaults;
        private readonly ConfigInterpolation? _interpolation;

        /// <summary>
        /// Create a new ConfigParser with optional interpolation.
        /// </summary>
        /// <param name="interpolation">The interpolation handler. Defaults to BasicInterpolation if null.</param>
        public ConfigParserInstance(ConfigInterpolation? interpolation = null)
        {
            _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            _defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _interpolation = interpolation;
        }

        /// <summary>
        /// Dict-like access to sections. Returns a SectionProxy.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <returns>A <see cref="SectionProxy"/> for the section.</returns>
        public SectionProxy this[string section]
        {
            get
            {
                if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
                {
                    return new SectionProxy(this, DefaultSection);
                }
                if (!_sections.ContainsKey(section))
                {
                    throw new NoSectionError(section);
                }
                return new SectionProxy(this, section);
            }
        }

        /// <summary>
        /// Return a list of section names, excluding DEFAULT.
        /// </summary>
        public List<string> Sections()
        {
            var result = new List<string>();
            foreach (var key in _sections.Keys)
            {
                result.Add(key);
            }
            return result;
        }

        /// <summary>
        /// Add a new section. Raises DuplicateSectionError if it already exists.
        /// </summary>
        /// <param name="section">The section name to add.</param>
        public void AddSection(string section)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot add the DEFAULT section");
            }
            if (_sections.ContainsKey(section))
            {
                throw new DuplicateSectionError(section);
            }
            _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Check if a section exists.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <returns>True if the section exists.</returns>
        public bool HasSection(string section)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                return false; // Python behavior: has_section returns False for DEFAULT
            }
            return _sections.ContainsKey(section);
        }

        /// <summary>
        /// Check if an option exists in a section.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="option">The option name.</param>
        /// <returns>True if the option exists.</returns>
        public bool HasOption(string section, string option)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                return _defaults.ContainsKey(option);
            }
            if (!_sections.ContainsKey(section))
            {
                return false;
            }
            return _sections[section].ContainsKey(option) || _defaults.ContainsKey(option);
        }

        /// <summary>
        /// Return a list of option names for a section, including defaults.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <returns>List of option names.</returns>
        public List<string> Options(string section)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>(_defaults.Keys);
            }
            if (!_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _defaults.Keys)
            {
                result.Add(key);
            }
            foreach (var key in _sections[section].Keys)
            {
                result.Add(key);
            }
            return new List<string>(result);
        }

        /// <summary>
        /// Get a value from a section.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="option">The option name.</param>
        /// <param name="raw">If true, do not interpolate the value.</param>
        /// <param name="fallback">Value to return if option not found. If null, raises NoOptionError.</param>
        /// <returns>The option value.</returns>
        public string? Get(string section, string option, bool raw = false, string? fallback = null)
        {
            string? rawValue = GetRaw(section, option);

            if (rawValue == null)
            {
                if (fallback != null)
                {
                    return fallback;
                }
                // Check if section exists first
                if (!string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase)
                    && !_sections.ContainsKey(section))
                {
                    throw new NoSectionError(section);
                }
                return null;
            }

            if (raw || _interpolation == null)
            {
                return rawValue;
            }

            return _interpolation.BeforeGet(this, section, option, rawValue);
        }

        /// <summary>
        /// Get a value as an integer.
        /// </summary>
        public int GetInt(string section, string option, int fallback = 0)
        {
            string? value = Get(section, option);
            if (value == null)
            {
                return fallback;
            }
            return int.Parse(value);
        }

        /// <summary>
        /// Get a value as a float/double.
        /// </summary>
        public double GetFloat(string section, string option, double fallback = 0.0)
        {
            string? value = Get(section, option);
            if (value == null)
            {
                return fallback;
            }
            return double.Parse(value);
        }

        /// <summary>
        /// Get a value as a boolean.
        /// Recognizes: 1/yes/true/on as true; 0/no/false/off as false.
        /// </summary>
        public bool GetBoolean(string section, string option, bool fallback = false)
        {
            string? value = Get(section, option);
            if (value == null)
            {
                return fallback;
            }
            string lower = value.ToLowerInvariant();
            if (lower == "1" || lower == "yes" || lower == "true" || lower == "on")
            {
                return true;
            }
            if (lower == "0" || lower == "no" || lower == "false" || lower == "off")
            {
                return false;
            }
            throw new ArgumentException("Not a boolean: " + value);
        }

        /// <summary>
        /// Set a value in a section.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="option">The option name.</param>
        /// <param name="value">The value to set.</param>
        public void Set(string section, string option, string value)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                _defaults[option] = value;
                return;
            }
            if (!_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }
            _sections[section][option] = value;
        }

        /// <summary>
        /// Remove an option from a section.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <param name="option">The option name.</param>
        /// <returns>True if the option existed.</returns>
        public bool RemoveOption(string section, string option)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                return _defaults.Remove(option);
            }
            if (!_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }
            return _sections[section].Remove(option);
        }

        /// <summary>
        /// Remove a section.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <returns>True if the section existed.</returns>
        public bool RemoveSection(string section)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return _sections.Remove(section);
        }

        /// <summary>
        /// Read configuration from a file path.
        /// </summary>
        /// <param name="filename">The path to the INI file.</param>
        public void Read(string filename)
        {
            if (!File.Exists(filename))
            {
                return; // Python behavior: silently ignores missing files
            }
            using (var reader = new StreamReader(filename))
            {
                ReadFile(reader, filename);
            }
        }

        /// <summary>
        /// Read configuration from a list of file paths.
        /// </summary>
        /// <param name="filenames">The paths to INI files.</param>
        public void Read(IEnumerable<string> filenames)
        {
            foreach (var filename in filenames)
            {
                Read(filename);
            }
        }

        /// <summary>
        /// Read configuration from a TextReader.
        /// </summary>
        /// <param name="reader">The text reader to read from.</param>
        /// <param name="source">Optional source name for error messages.</param>
        public void ReadFile(TextReader reader, string? source = null)
        {
            ParseIni(reader, source);
        }

        /// <summary>
        /// Read configuration from a string.
        /// </summary>
        /// <param name="str">The INI content string.</param>
        /// <param name="source">Optional source name for error messages.</param>
        public void ReadString(string str, string? source = null)
        {
            using (var reader = new StringReader(str))
            {
                ReadFile(reader, source ?? "<string>");
            }
        }

        /// <summary>
        /// Write the configuration to a TextWriter.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="spaceAroundDelimiters">Whether to include spaces around the = delimiter.</param>
        public void Write(TextWriter writer, bool spaceAroundDelimiters = true)
        {
            string delimiter = spaceAroundDelimiters ? " = " : "=";

            // Write DEFAULT section first if non-empty
            if (_defaults.Count > 0)
            {
                writer.WriteLine("[" + DefaultSection + "]");
                foreach (var kvp in _defaults)
                {
                    writer.WriteLine(kvp.Key + delimiter + kvp.Value);
                }
                writer.WriteLine();
            }

            // Write other sections
            foreach (var section in _sections)
            {
                writer.WriteLine("[" + section.Key + "]");
                foreach (var kvp in section.Value)
                {
                    writer.WriteLine(kvp.Key + delimiter + kvp.Value);
                }
                writer.WriteLine();
            }
        }

        /// <summary>
        /// Return a dictionary of key-value pairs for a section, including defaults.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <returns>Dictionary of option values.</returns>
        public Dictionary<string, string> Items(string section)
        {
            if (!string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase)
                && !_sections.ContainsKey(section))
            {
                throw new NoSectionError(section);
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Start with defaults
            foreach (var kvp in _defaults)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Overlay section values
            if (!string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase)
                && _sections.ContainsKey(section))
            {
                foreach (var kvp in _sections[section])
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Access the DEFAULT section values.
        /// </summary>
        /// <returns>Dictionary of default values.</returns>
        public Dictionary<string, string> Defaults()
        {
            return new Dictionary<string, string>(_defaults, StringComparer.OrdinalIgnoreCase);
        }

        private string? GetRaw(string section, string option)
        {
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                if (_defaults.TryGetValue(option, out string? defVal))
                {
                    return defVal;
                }
                return null;
            }

            if (_sections.TryGetValue(section, out Dictionary<string, string>? sectionDict))
            {
                if (sectionDict.TryGetValue(option, out string? val))
                {
                    return val;
                }
            }

            // Fallback to defaults
            if (_defaults.TryGetValue(option, out string? defaultVal))
            {
                return defaultVal;
            }

            return null;
        }

        private void ParseIni(TextReader reader, string? source)
        {
            string? currentSection = null;
            string? pendingKey = null;
            string? pendingValue = null;
            int lineNumber = 0;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                // Handle multiline continuation (leading whitespace)
                if (pendingKey != null && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    pendingValue = pendingValue + "\n" + line.TrimStart();
                    continue;
                }

                // Flush pending key-value
                if (pendingKey != null)
                {
                    SetParsed(currentSection, pendingKey, pendingValue ?? "");
                    pendingKey = null;
                    pendingValue = null;
                }

                // Strip inline comments and trim
                string trimmed = line.TrimStart();

                // Skip empty lines
                if (trimmed.Length == 0)
                {
                    continue;
                }

                // Skip comment lines
                if (trimmed[0] == '#' || trimmed[0] == ';')
                {
                    continue;
                }

                // Section header
                if (trimmed[0] == '[')
                {
                    int endBracket = trimmed.IndexOf(']');
                    if (endBracket < 0)
                    {
                        throw new ParsingError(
                            "No closing bracket for section header at line " + lineNumber,
                            source,
                            lineNumber);
                    }
                    string sectionName = trimmed.Substring(1, endBracket - 1).Trim();
                    if (string.Equals(sectionName, DefaultSection, StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = DefaultSection;
                    }
                    else
                    {
                        currentSection = sectionName;
                        if (!_sections.ContainsKey(sectionName))
                        {
                            _sections[sectionName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    continue;
                }

                // Key-value pair
                int eqIdx = trimmed.IndexOf('=');
                int colonIdx = trimmed.IndexOf(':');
                int delimIdx;

                if (eqIdx >= 0 && colonIdx >= 0)
                {
                    delimIdx = Math.Min(eqIdx, colonIdx);
                }
                else if (eqIdx >= 0)
                {
                    delimIdx = eqIdx;
                }
                else if (colonIdx >= 0)
                {
                    delimIdx = colonIdx;
                }
                else
                {
                    throw new ParsingError(
                        "No key-value delimiter found at line " + lineNumber,
                        source,
                        lineNumber);
                }

                string key = trimmed.Substring(0, delimIdx).Trim();
                string value = trimmed.Substring(delimIdx + 1).Trim();

                if (currentSection == null)
                {
                    throw new ParsingError(
                        "Key-value pair found before any section header at line " + lineNumber,
                        source,
                        lineNumber);
                }

                pendingKey = key;
                pendingValue = value;
            }

            // Flush last pending key-value
            if (pendingKey != null)
            {
                SetParsed(currentSection, pendingKey, pendingValue ?? "");
            }
        }

        private void SetParsed(string? section, string key, string value)
        {
            if (section == null)
            {
                return;
            }
            if (string.Equals(section, DefaultSection, StringComparison.OrdinalIgnoreCase))
            {
                _defaults[key] = value;
            }
            else
            {
                if (!_sections.ContainsKey(section))
                {
                    _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                _sections[section][key] = value;
            }
        }
    }
}
