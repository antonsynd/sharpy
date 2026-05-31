using System;

namespace Sharpy
{
    /// <summary>Represents the base exception for configparser errors.</summary>
    [SharpyModuleType("configparser", "Error")]
    public class ConfigparserError : Exception
    {
        /// <summary>Initializes a new configparser exception with the specified message.</summary>
        public ConfigparserError(string message) : base(message) { }

        /// <summary>Initializes a new configparser exception with an inner exception.</summary>
        public ConfigparserError(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>Raised when a requested section does not exist.</summary>
    [SharpyModuleType("configparser")]
    public class NoSectionError : ConfigparserError
    {
        /// <summary>Gets the missing section name.</summary>
        public string Section { get; }

        /// <summary>Initializes the exception for the missing section.</summary>
        public NoSectionError(string section)
            : base("No section: '" + section + "'")
        {
            Section = section;
        }
    }

    /// <summary>Raised when a requested option does not exist.</summary>
    [SharpyModuleType("configparser")]
    public class NoOptionError : ConfigparserError
    {
        /// <summary>Gets the missing option name.</summary>
        public string Option { get; }

        /// <summary>Gets the section containing the missing option.</summary>
        public string Section { get; }

        /// <summary>Initializes the exception for the missing option.</summary>
        public NoOptionError(string option, string section)
            : base("No option '" + option + "' in section: '" + section + "'")
        {
            Option = option;
            Section = section;
        }
    }

    /// <summary>Raised when adding a section that already exists.</summary>
    [SharpyModuleType("configparser")]
    public class DuplicateSectionError : ConfigparserError
    {
        /// <summary>Gets the duplicate section name.</summary>
        public string Section { get; }

        /// <summary>Initializes the exception for the duplicate section.</summary>
        public DuplicateSectionError(string section)
            : base("Section '" + section + "' already exists")
        {
            Section = section;
        }
    }

    /// <summary>Raised when adding an option that already exists.</summary>
    [SharpyModuleType("configparser")]
    public class DuplicateOptionError : ConfigparserError
    {
        /// <summary>Gets the section containing the duplicate option.</summary>
        public string Section { get; }

        /// <summary>Gets the duplicate option name.</summary>
        public string Option { get; }

        /// <summary>Initializes the exception for the duplicate option.</summary>
        public DuplicateOptionError(string section, string option)
            : base("Option '" + option + "' in section '" + section + "' already exists")
        {
            Section = section;
            Option = option;
        }
    }

    /// <summary>Raised when parsing invalid configuration data.</summary>
    [SharpyModuleType("configparser")]
    public class ParsingError : ConfigparserError
    {
        /// <summary>Gets the source being parsed, if available.</summary>
        public new string? Source { get; }

        /// <summary>Gets the line number associated with the parse error.</summary>
        public int LineNumber { get; }

        /// <summary>Initializes a parse error with no source location.</summary>
        public ParsingError(string message) : base(message)
        {
            Source = null;
            LineNumber = 0;
        }

        /// <summary>Initializes a parse error with source location information.</summary>
        public ParsingError(string message, string? source, int lineNumber)
            : base(message)
        {
            Source = source;
            LineNumber = lineNumber;
        }
    }

    /// <summary>Raised when data appears before any section header.</summary>
    [SharpyModuleType("configparser")]
    public class MissingSectionHeaderError : ParsingError
    {
        /// <summary>Gets the filename or source name.</summary>
        public string Filename { get; }

        /// <summary>Gets the line number with the missing header.</summary>
        public int Lineno { get; }

        /// <summary>Gets the offending line text.</summary>
        public string Line { get; }

        /// <summary>Initializes the exception for a missing section header.</summary>
        public MissingSectionHeaderError(string filename, int lineno, string line)
            : base("File contains no section headers.\nfile: '" + filename + "', line: " + lineno + "\n'" + line + "'",
                   filename, lineno)
        {
            Filename = filename;
            Lineno = lineno;
            Line = line;
        }
    }

    /// <summary>Raised when interpolation fails for an option value.</summary>
    [SharpyModuleType("configparser")]
    public class InterpolationError : ConfigparserError
    {
        /// <summary>Gets the section containing the interpolation error.</summary>
        public string Section { get; }

        /// <summary>Gets the option containing the interpolation error.</summary>
        public string Option { get; }

        /// <summary>Gets the raw value that failed to interpolate.</summary>
        public string RawValue { get; }

        /// <summary>Initializes the exception for an interpolation failure.</summary>
        public InterpolationError(string message, string section, string option, string rawValue)
            : base(message)
        {
            Section = section;
            Option = option;
            RawValue = rawValue;
        }
    }

    [SharpyModuleType("configparser")]
    public class InterpolationDepthError : InterpolationError
    {
        public InterpolationDepthError(string option, string section, string rawval)
            : base("Recursion limit exceeded in value substitution: section '" + section + "' option '" + option + "'",
                   section, option, rawval)
        {
        }
    }

    [SharpyModuleType("configparser")]
    public class InterpolationMissingOptionError : InterpolationError
    {
        public string Reference { get; }

        public InterpolationMissingOptionError(string option, string section, string rawval, string reference)
            : base("Bad value substitution: option '" + option + "' in section '" + section +
                   "' contains an interpolation key '" + reference + "' which is not a valid option name.",
                   section, option, rawval)
        {
            Reference = reference;
        }
    }

    [SharpyModuleType("configparser")]
    public class InterpolationSyntaxError : InterpolationError
    {
        public InterpolationSyntaxError(string option, string section, string msg)
            : base(msg, section, option, "")
        {
        }
    }
}
