using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// A group of mutually exclusive optional arguments.
    /// At most one may be provided. If required, exactly one must be provided.
    /// </summary>
    public sealed class MutuallyExclusiveGroup
    {
        private readonly ArgumentParser _parser;
        private readonly bool _required;
        private readonly System.Collections.Generic.List<string> _destNames = new System.Collections.Generic.List<string>();
        private readonly System.Collections.Generic.List<string> _longNames = new System.Collections.Generic.List<string>();

        internal MutuallyExclusiveGroup(ArgumentParser parser, bool required)
        {
            _parser = parser;
            _required = required;
        }

        /// <summary>Add an optional argument to this mutually exclusive group.</summary>
        public void AddOptionalArgument(
            string longName,
            string? shortName = null,
            string type = "str",
            string help = "",
            object? defaultValue = null,
            string action = "store",
            string? dest = null)
        {
            _parser.AddOptionalArgument(longName, shortName: shortName, type: type, help: help,
                defaultValue: defaultValue, action: action, dest: dest);
            string destName = dest ?? NormalizeDest(longName);
            _destNames.Add(destName);
            _longNames.Add(longName);
        }

        /// <summary>Validate that at most one (or exactly one if required) option was provided.</summary>
        internal void Validate(HashSet<string> seen)
        {
            var provided = new System.Collections.Generic.List<string>();
            for (int i = 0; i < _destNames.Count; i++)
            {
                if (seen.Contains(_destNames[i]))
                {
                    provided.Add(_longNames[i]);
                }
            }

            if (provided.Count > 1)
            {
                throw new ArgumentError("argument " + provided[1] +
                    ": not allowed with argument " + provided[0]);
            }

            if (_required && provided.Count == 0)
            {
                throw new ArgumentError("one of the arguments " +
                    string.Join(", ", _longNames) + " is required");
            }
        }

        private static string NormalizeDest(string name)
        {
            string dest = name;
            while (dest.StartsWith("-"))
            {
                dest = dest.Substring(1);
            }
            return dest.Replace('-', '_');
        }
    }
}
