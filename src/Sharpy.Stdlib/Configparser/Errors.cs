using System;

namespace Sharpy
{
    /// <summary>
    /// Raised when a section already exists.
    /// </summary>
    public class DuplicateSectionError : Exception
    {
        /// <summary>The section name that was duplicated.</summary>
        public string Section { get; }

        /// <summary>Initializes a new instance of <see cref="DuplicateSectionError"/>.</summary>
        public DuplicateSectionError(string section)
            : base("Section '" + section + "' already exists")
        {
            Section = section;
        }
    }

    /// <summary>
    /// Raised when a specified section is not found.
    /// </summary>
    public class NoSectionError : Exception
    {
        /// <summary>The section name that was not found.</summary>
        public string Section { get; }

        /// <summary>Initializes a new instance of <see cref="NoSectionError"/>.</summary>
        public NoSectionError(string section)
            : base("No section: '" + section + "'")
        {
            Section = section;
        }
    }

    /// <summary>
    /// Raised when a specified option is not found in a section.
    /// </summary>
    public class NoOptionError : Exception
    {
        /// <summary>The option name that was not found.</summary>
        public string Option { get; }

        /// <summary>The section searched.</summary>
        public string Section { get; }

        /// <summary>Initializes a new instance of <see cref="NoOptionError"/>.</summary>
        public NoOptionError(string option, string section)
            : base("No option '" + option + "' in section: '" + section + "'")
        {
            Option = option;
            Section = section;
        }
    }

    /// <summary>
    /// Raised when interpolation cannot be completed because the number of
    /// substitutions exceeds the maximum recursion depth.
    /// </summary>
    public class InterpolationDepthError : Exception
    {
        /// <summary>The section where interpolation failed.</summary>
        public string Section { get; }

        /// <summary>The raw value that couldn't be interpolated.</summary>
        public string RawValue { get; }

        /// <summary>Initializes a new instance of <see cref="InterpolationDepthError"/>.</summary>
        public InterpolationDepthError(string section, string rawValue)
            : base("Recursion limit exceeded in value substitution: section '" + section + "'")
        {
            Section = section;
            RawValue = rawValue;
        }
    }

    /// <summary>
    /// Raised when interpolation references a missing option.
    /// </summary>
    public class InterpolationMissingOptionError : Exception
    {
        /// <summary>The section where interpolation failed.</summary>
        public string Section { get; }

        /// <summary>The raw value containing the reference.</summary>
        public string RawValue { get; }

        /// <summary>The missing option that was referenced.</summary>
        public string Reference { get; }

        /// <summary>Initializes a new instance of <see cref="InterpolationMissingOptionError"/>.</summary>
        public InterpolationMissingOptionError(string section, string rawValue, string reference)
            : base("Bad value substitution: option '" + reference + "' in section '" + section + "'")
        {
            Section = section;
            RawValue = rawValue;
            Reference = reference;
        }
    }

    /// <summary>
    /// Raised when errors occur parsing a configuration file.
    /// </summary>
    public class ParsingError : Exception
    {
        /// <summary>The source filename.</summary>
        public new string? Source { get; }

        /// <summary>The line number where the error occurred.</summary>
        public int LineNumber { get; }

        /// <summary>Initializes a new instance of <see cref="ParsingError"/>.</summary>
        public ParsingError(string message, string? source, int lineNumber)
            : base(message)
        {
            Source = source;
            LineNumber = lineNumber;
        }
    }
}
