using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// A named group of arguments for organization in help text.
    /// Arguments still belong to the parent parser; groups are for help formatting.
    /// </summary>
    public sealed class ArgumentGroup
    {
        private readonly ArgumentParser _parser;
        private readonly string _title;

        internal ArgumentGroup(ArgumentParser parser, string title)
        {
            _parser = parser;
            _title = title;
        }

        /// <summary>Add a positional argument to this group.</summary>
        public void AddArgument(
            string name,
            string type = "str",
            string help = "",
            object? defaultValue = null,
            string? nargs = null,
            List<string>? choices = null)
        {
            _parser.AddArgument(name, type: type, help: help, defaultValue: defaultValue, nargs: nargs, choices: choices);
        }

        /// <summary>Add an optional argument to this group.</summary>
        public void AddOptionalArgument(
            string longName,
            string? shortName = null,
            string type = "str",
            string help = "",
            object? defaultValue = null,
            bool required = false,
            string action = "store",
            string? nargs = null,
            List<string>? choices = null,
            string? dest = null)
        {
            _parser.AddOptionalArgument(longName, shortName: shortName, type: type, help: help,
                defaultValue: defaultValue, required: required, action: action, nargs: nargs,
                choices: choices, dest: dest);
        }
    }
}
