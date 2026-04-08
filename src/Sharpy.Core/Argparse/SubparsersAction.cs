using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Manages subparser commands for ArgumentParser.
    /// </summary>
    public sealed class SubparsersAction
    {
        private readonly string _title;
        private readonly Dictionary<string, ArgumentParser> _parsers = new Dictionary<string, ArgumentParser>();

        /// <summary>The destination attribute name for the subcommand.</summary>
        public string Dest { get; }

        internal SubparsersAction(string title, string dest)
        {
            _title = title;
            Dest = dest;
        }

        /// <summary>Add a subcommand parser.</summary>
        public ArgumentParser AddParser(string name, string help = "")
        {
            var parser = new ArgumentParser(description: help, prog: name);
            _parsers[name] = parser;
            return parser;
        }

        /// <summary>Check if a parser with the given name exists.</summary>
        public bool HasParser(string name)
        {
            return _parsers.ContainsKey(name);
        }

        /// <summary>Get the parser for the given subcommand name.</summary>
        public ArgumentParser GetParser(string name)
        {
            return _parsers[name];
        }
    }
}
