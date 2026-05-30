using System;

namespace Sharpy
{
    [SharpyModuleType("configparser", "Error")]
    public class ConfigparserError : Exception
    {
        public ConfigparserError(string message) : base(message) { }
        public ConfigparserError(string message, Exception innerException) : base(message, innerException) { }
    }

    [SharpyModuleType("configparser")]
    public class NoSectionError : ConfigparserError
    {
        public string Section { get; }

        public NoSectionError(string section)
            : base("No section: '" + section + "'")
        {
            Section = section;
        }
    }

    [SharpyModuleType("configparser")]
    public class NoOptionError : ConfigparserError
    {
        public string Option { get; }
        public string Section { get; }

        public NoOptionError(string option, string section)
            : base("No option '" + option + "' in section: '" + section + "'")
        {
            Option = option;
            Section = section;
        }
    }

    [SharpyModuleType("configparser")]
    public class DuplicateSectionError : ConfigparserError
    {
        public string Section { get; }

        public DuplicateSectionError(string section)
            : base("Section '" + section + "' already exists")
        {
            Section = section;
        }
    }

    [SharpyModuleType("configparser")]
    public class DuplicateOptionError : ConfigparserError
    {
        public string Section { get; }
        public string Option { get; }

        public DuplicateOptionError(string section, string option)
            : base("Option '" + option + "' in section '" + section + "' already exists")
        {
            Section = section;
            Option = option;
        }
    }

    [SharpyModuleType("configparser")]
    public class ParsingError : ConfigparserError
    {
        public new string? Source { get; }
        public int LineNumber { get; }

        public ParsingError(string message) : base(message)
        {
            Source = null;
            LineNumber = 0;
        }

        public ParsingError(string message, string? source, int lineNumber)
            : base(message)
        {
            Source = source;
            LineNumber = lineNumber;
        }
    }

    [SharpyModuleType("configparser")]
    public class MissingSectionHeaderError : ParsingError
    {
        public string Filename { get; }
        public int Lineno { get; }
        public string Line { get; }

        public MissingSectionHeaderError(string filename, int lineno, string line)
            : base("File contains no section headers.\nfile: '" + filename + "', line: " + lineno + "\n'" + line + "'",
                   filename, lineno)
        {
            Filename = filename;
            Lineno = lineno;
            Line = line;
        }
    }

    [SharpyModuleType("configparser")]
    public class InterpolationError : ConfigparserError
    {
        public string Section { get; }
        public string Option { get; }
        public string RawValue { get; }

        public InterpolationError(string message, string section, string option, string rawValue)
            : base(message)
        {
            Section = section;
            Option = option;
            RawValue = rawValue;
        }
    }
}
