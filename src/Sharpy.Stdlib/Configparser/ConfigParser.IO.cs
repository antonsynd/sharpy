using System;
using System.IO;
using SCG = System.Collections.Generic;

namespace Sharpy
{
    public sealed partial class ConfigParser
    {
        public void ReadString(string content, string source = "<string>")
        {
            using (var reader = new StringReader(content))
            {
                ParseIni(reader, source);
            }
        }

        public void Read(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }
            string content = File.ReadAllText(filename);
            ReadString(content, filename);
        }

        public void ReadDict(SCG.Dictionary<string, SCG.Dictionary<string, string>> dictionary)
        {
            foreach (var section in dictionary)
            {
                if (!string.Equals(section.Key, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!_sections.ContainsKey(section.Key))
                    {
                        _sections[section.Key] = new SCG.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                foreach (var kvp in section.Value)
                {
                    Set(section.Key, kvp.Key, kvp.Value);
                }
            }
        }

        public void Write(TextWriter writer, bool spaceAroundDelimiters = true)
        {
            string delimiter = spaceAroundDelimiters ? " = " : "=";

            if (_defaults.Count > 0)
            {
                writer.WriteLine("[" + DefaultSectionName + "]");
                foreach (var kvp in _defaults)
                {
                    if (kvp.Value != null)
                    {
                        writer.WriteLine(kvp.Key + delimiter + kvp.Value);
                    }
                    else
                    {
                        writer.WriteLine(kvp.Key);
                    }
                }
                writer.WriteLine();
            }

            foreach (var section in _sections)
            {
                writer.WriteLine("[" + section.Key + "]");
                foreach (var kvp in section.Value)
                {
                    if (kvp.Value != null)
                    {
                        writer.WriteLine(kvp.Key + delimiter + kvp.Value);
                    }
                    else
                    {
                        writer.WriteLine(kvp.Key);
                    }
                }
                writer.WriteLine();
            }
        }

        public void WriteToFile(string filename, bool spaceAroundDelimiters = true)
        {
            using (var writer = new StreamWriter(filename))
            {
                Write(writer, spaceAroundDelimiters);
            }
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

                if (pendingKey != null && line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    pendingValue = pendingValue + "\n" + line.TrimStart();
                    continue;
                }

                if (pendingKey != null)
                {
                    FlushPending(currentSection, pendingKey, pendingValue);
                    pendingKey = null;
                    pendingValue = null;
                }

                string trimmed = line.TrimStart();

                if (trimmed.Length == 0) continue;

                if (trimmed[0] == '#' || trimmed[0] == ';') continue;

                if (trimmed[0] == '[')
                {
                    int endBracket = trimmed.IndexOf(']');
                    if (endBracket < 0)
                    {
                        throw new ParsingError(
                            "No closing bracket for section header at line " + lineNumber,
                            source, lineNumber);
                    }

                    string sectionName = trimmed.Substring(1, endBracket - 1);

                    if (string.Equals(sectionName.Trim(), DefaultSectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        currentSection = DefaultSectionName;
                    }
                    else
                    {
                        currentSection = sectionName;
                        if (!_sections.ContainsKey(sectionName))
                        {
                            _sections[sectionName] = new SCG.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    continue;
                }

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
                else if (_allowNoValue)
                {
                    if (currentSection == null)
                    {
                        throw new MissingSectionHeaderError(source ?? "<???>", lineNumber, line);
                    }
                    pendingKey = trimmed.Trim().ToLowerInvariant();
                    pendingValue = null;
                    continue;
                }
                else
                {
                    throw new ParsingError(
                        "Source contains parsing errors: line " + lineNumber,
                        source, lineNumber);
                }

                if (currentSection == null)
                {
                    throw new MissingSectionHeaderError(source ?? "<???>", lineNumber, line);
                }

                string key = trimmed.Substring(0, delimIdx).Trim().ToLowerInvariant();
                string value = trimmed.Substring(delimIdx + 1).Trim();

                pendingKey = key;
                pendingValue = value;
            }

            if (pendingKey != null)
            {
                FlushPending(currentSection, pendingKey, pendingValue);
            }
        }

        private void FlushPending(string? section, string key, string? value)
        {
            if (section == null) return;

            if (string.Equals(section, DefaultSectionName, StringComparison.OrdinalIgnoreCase))
            {
                _defaults[key] = value;
            }
            else
            {
                if (!_sections.ContainsKey(section))
                {
                    _sections[section] = new SCG.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                }
                _sections[section][key] = value;
            }
        }
    }
}
