using System;
using System.Collections.Generic;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible command-line argument parser.
    /// </summary>
    [SharpyModuleType("argparse")]
    public sealed class ArgumentParser
    {
        private readonly string _description;
        internal readonly System.Collections.Generic.List<ArgumentDef> _arguments = new System.Collections.Generic.List<ArgumentDef>();
        private readonly bool _addHelp;
        private TextWriter _output;
        private SubparsersAction? _subparsers;
        private readonly System.Collections.Generic.List<MutuallyExclusiveGroup> _mutuallyExclusiveGroups = new System.Collections.Generic.List<MutuallyExclusiveGroup>();

        /// <summary>Program name for help text.</summary>
        public string Prog { get; set; }

        /// <summary>Create a new argument parser.</summary>
        public ArgumentParser(string description = "", string prog = "", bool addHelp = true)
        {
            _description = description;
            Prog = string.IsNullOrEmpty(prog) ? "prog" : prog;
            _addHelp = addHelp;
            _output = Console.Out;

            if (addHelp)
            {
                _arguments.Add(new ArgumentDef
                {
                    ShortName = "-h",
                    LongName = "--help",
                    Dest = "help",
                    Action = "help",
                    Help = "show this help message and exit",
                    IsOptional = true,
                    Default = false
                });
            }
        }

        /// <summary>
        /// Add a positional argument.
        /// </summary>
        /// <param name="name">The argument name.</param>
        /// <param name="type">Value type: "str", "int", or "float".</param>
        /// <param name="help">Help text for this argument.</param>
        /// <param name="defaultValue">Default value if not provided.</param>
        /// <param name="nargs">Number of arguments: "*", "+", or "?".</param>
        /// <param name="choices">Restrict values to this set.</param>
        /// <example>
        /// <code>
        /// parser = ArgumentParser(description="My tool")
        /// parser.add_argument("filename", help="input file")
        /// </code>
        /// </example>
        public void AddArgument(
            string name,
            string type = "str",
            string help = "",
            object? defaultValue = null,
            string? nargs = null,
            List<string>? choices = null)
        {
            if (name.StartsWith("-"))
            {
                throw new ValueError("positional argument names must not start with '-'");
            }

            _arguments.Add(new ArgumentDef
            {
                LongName = name,
                Dest = name,
                Type = type,
                Help = help,
                Default = nargs == "*" ? (object)new List<string>() : defaultValue,
                Nargs = nargs,
                Choices = choices,
                IsOptional = false,
                Required = nargs != "*"
            });
        }

        /// <summary>
        /// Add an optional argument with long name only (e.g., "--verbose").
        /// </summary>
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
            ValidateOptionalName(longName);
            if (shortName != null)
            {
                ValidateOptionalName(shortName);
            }

            _arguments.Add(new ArgumentDef
            {
                ShortName = shortName,
                LongName = longName,
                Dest = dest ?? NormalizeDest(longName),
                Type = type,
                Help = help,
                Default = GetDefaultForAction(action, defaultValue),
                Required = required,
                Action = action,
                Nargs = nargs,
                Choices = choices,
                IsOptional = true
            });
        }
        /// <summary>
        /// Add subparsers to this parser, allowing subcommand dispatch.
        /// </summary>
        public SubparsersAction AddSubparsers(string title = "", string dest = "")
        {
            if (_subparsers != null)
            {
                throw new ArgumentError("cannot have multiple subparser arguments");
            }
            _subparsers = new SubparsersAction(title, dest);
            return _subparsers;
        }

        /// <summary>
        /// Add a named argument group for organizational purposes.
        /// Arguments in the group are still parsed by this parser.
        /// </summary>
        public ArgumentGroup AddArgumentGroup(string title)
        {
            return new ArgumentGroup(this, title);
        }

        /// <summary>
        /// Add a mutually exclusive group of optional arguments.
        /// At most one option in the group may be provided.
        /// </summary>
        public MutuallyExclusiveGroup AddMutuallyExclusiveGroup(bool required = false)
        {
            var group = new MutuallyExclusiveGroup(this, required);
            _mutuallyExclusiveGroups.Add(group);
            return group;
        }

        /// <summary>
        /// Parse command-line arguments from a Sharpy list of strings.
        /// </summary>
        public Namespace ParseArgs(List<string> args)
        {
            var collection = (System.Collections.Generic.ICollection<string>)args;
            var array = new string[collection.Count];
            collection.CopyTo(array, 0);
            return ParseArgs(array);
        }

        /// <summary>
        /// Parse command-line arguments from the given string array.
        /// Internal helper used by public overloads and subparser delegation.
        /// </summary>
        private Namespace ParseArgs(string[] args)
        {
            var ns = new Namespace();
            int positionalIndex = 0;
            var positionalArgs = GetPositionalArgs();
            var seen = new HashSet<string>();

            // Set defaults
            foreach (var argDef in _arguments)
            {
                if (argDef.Action != "help")
                {
                    ns.Set(argDef.Dest, argDef.Default);
                }
            }

            // If we have subparsers, set default dest to null
            if (_subparsers != null && !string.IsNullOrEmpty(_subparsers.Dest))
            {
                ns.Set(_subparsers.Dest, null);
            }

            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];

                if (arg == "-h" || arg == "--help")
                {
                    PrintHelp();
                    throw new SystemExit(0);
                }

                if (arg.StartsWith("--") || (arg.StartsWith("-") && arg.Length > 1 && !char.IsDigit(arg[1])))
                {
                    // Optional argument
                    var argDef = FindOptionalArg(arg);
                    if (argDef == null)
                    {
                        throw new ArgumentError("unrecognized arguments: " + arg);
                    }

                    seen.Add(argDef.Dest);
                    i = ProcessOptionalArg(argDef, args, i, ns);
                }
                else
                {
                    // Check if this is a subparser command
                    if (_subparsers != null && _subparsers.HasParser(arg))
                    {
                        if (!string.IsNullOrEmpty(_subparsers.Dest))
                        {
                            ns.Set(_subparsers.Dest, arg);
                        }
                        // Delegate remaining args to the sub-parser
                        var subParser = _subparsers.GetParser(arg);
                        var remaining = new string[args.Length - i - 1];
                        Array.Copy(args, i + 1, remaining, 0, remaining.Length);
                        var subNs = subParser.ParseArgs(remaining);
                        ns.Merge(subNs);
                        return ns;
                    }

                    // Positional argument
                    if (positionalIndex >= positionalArgs.Count)
                    {
                        throw new ArgumentError("unrecognized arguments: " + arg);
                    }

                    var argDef = positionalArgs[positionalIndex];
                    seen.Add(argDef.Dest);

                    if (argDef.Nargs == "*" || argDef.Nargs == "+")
                    {
                        var values = new List<string>();
                        while (i < args.Length && !args[i].StartsWith("-"))
                        {
                            ValidateChoice(argDef, args[i]);
                            values.Append(args[i]);
                            i++;
                        }

                        if (argDef.Nargs == "+" && ((ICollection<string>)values).Count == 0)
                        {
                            throw new ArgumentError(
                                "the following arguments are required: " + argDef.Dest);
                        }

                        ns.Set(argDef.Dest, ConvertList(values, argDef.Type));
                    }
                    else
                    {
                        ValidateChoice(argDef, arg);
                        ns.Set(argDef.Dest, ConvertValue(arg, argDef.Type));
                        i++;
                    }

                    positionalIndex++;
                }
            }

            // Check required arguments
            foreach (var argDef in _arguments)
            {
                if (argDef.Action == "help")
                    continue;

                if (argDef.Required && !seen.Contains(argDef.Dest))
                {
                    if (!argDef.IsOptional)
                    {
                        throw new ArgumentError(
                            "the following arguments are required: " + argDef.Dest);
                    }
                    else
                    {
                        throw new ArgumentError(
                            "the following arguments are required: " + argDef.LongName);
                    }
                }
            }

            // Check mutually exclusive groups
            foreach (var group in _mutuallyExclusiveGroups)
            {
                group.Validate(seen);
            }

            return ns;
        }

        /// <summary>
        /// Parse command-line arguments from Environment.GetCommandLineArgs().
        /// </summary>
        public Namespace ParseArgs()
        {
            string[] allArgs = Environment.GetCommandLineArgs();
            // Skip the first element (program name)
            string[] args = new string[allArgs.Length - 1];
            Array.Copy(allArgs, 1, args, 0, args.Length);
            return ParseArgs(args);
        }

        /// <summary>
        /// Format and return the help text.
        /// </summary>
        public string FormatHelp()
        {
            var sb = new System.Text.StringBuilder();

            // Usage line
            sb.Append("usage: ");
            sb.Append(Prog);
            foreach (var arg in _arguments)
            {
                if (arg.IsOptional)
                {
                    sb.Append(" [");
                    sb.Append(arg.LongName);
                    if (arg.Action == "store")
                    {
                        sb.Append(' ');
                        sb.Append(arg.Dest.ToUpper());
                    }

                    sb.Append(']');
                }
                else
                {
                    sb.Append(' ');
                    sb.Append(arg.Dest);
                }
            }

            sb.Append('\n');

            // Description
            if (!string.IsNullOrEmpty(_description))
            {
                sb.Append('\n');
                sb.Append(_description);
                sb.Append('\n');
            }

            // Positional arguments
            var positionals = GetPositionalArgs();
            if (positionals.Count > 0)
            {
                sb.Append("\npositional arguments:\n");
                foreach (var arg in positionals)
                {
                    sb.Append("  ");
                    sb.Append(arg.Dest);
                    if (!string.IsNullOrEmpty(arg.Help))
                    {
                        sb.Append(new string(' ', System.Math.Max(1, 22 - arg.Dest.Length - 2)));
                        sb.Append(arg.Help);
                    }

                    sb.Append('\n');
                }
            }

            // Optional arguments
            var optionals = GetOptionalArgs();
            if (optionals.Count > 0)
            {
                sb.Append("\noptions:\n");
                foreach (var arg in optionals)
                {
                    sb.Append("  ");
                    string names;
                    if (!string.IsNullOrEmpty(arg.ShortName))
                    {
                        names = arg.ShortName + ", " + arg.LongName;
                    }
                    else
                    {
                        names = arg.LongName;
                    }

                    sb.Append(names);
                    if (!string.IsNullOrEmpty(arg.Help))
                    {
                        sb.Append(new string(' ', System.Math.Max(1, 22 - names.Length - 2)));
                        sb.Append(arg.Help);
                    }

                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Set the output writer for help text (for testing).
        /// </summary>
        public void SetOutput(TextWriter output)
        {
            _output = output;
        }

        private void PrintHelp()
        {
            _output.Write(FormatHelp());
        }

        private System.Collections.Generic.List<ArgumentDef> GetPositionalArgs()
        {
            var result = new System.Collections.Generic.List<ArgumentDef>();
            foreach (var arg in _arguments)
            {
                if (!arg.IsOptional)
                {
                    result.Add(arg);
                }
            }

            return result;
        }

        private System.Collections.Generic.List<ArgumentDef> GetOptionalArgs()
        {
            var result = new System.Collections.Generic.List<ArgumentDef>();
            foreach (var arg in _arguments)
            {
                if (arg.IsOptional)
                {
                    result.Add(arg);
                }
            }

            return result;
        }

        private ArgumentDef? FindOptionalArg(string name)
        {
            foreach (var arg in _arguments)
            {
                if (arg.IsOptional && (arg.LongName == name || arg.ShortName == name))
                {
                    return arg;
                }
            }

            return null;
        }

        private int ProcessOptionalArg(ArgumentDef argDef, string[] args, int i, Namespace ns)
        {
            switch (argDef.Action)
            {
                case "store_true":
                    ns.Set(argDef.Dest, true);
                    return i + 1;

                case "store_false":
                    ns.Set(argDef.Dest, false);
                    return i + 1;

                case "count":
                    object? current = null;
                    try
                    {
                        current = ns[argDef.Dest];
                    }
                    catch (AttributeError)
                    {
                        // Ignore
                    }

                    int count = current is int c ? c : 0;
                    ns.Set(argDef.Dest, count + 1);
                    return i + 1;

                case "append":
                    i++;
                    if (i >= args.Length)
                    {
                        throw new ArgumentError(
                            "argument " + argDef.LongName + ": expected one argument");
                    }

                    ValidateChoice(argDef, args[i]);
                    object? existing = null;
                    try
                    {
                        existing = ns[argDef.Dest];
                    }
                    catch (AttributeError)
                    {
                        // Ignore
                    }

                    var appendList = existing as List<object?> ?? new List<object?>();
                    appendList.Append(ConvertValue(args[i], argDef.Type));
                    ns.Set(argDef.Dest, appendList);
                    return i + 1;

                case "store":
                default:
                    if (argDef.Nargs == "?")
                    {
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            i++;
                            ValidateChoice(argDef, args[i]);
                            ns.Set(argDef.Dest, ConvertValue(args[i], argDef.Type));
                        }

                        // else: use default (already set)
                        return i + 1;
                    }

                    i++;
                    if (i >= args.Length)
                    {
                        throw new ArgumentError(
                            "argument " + argDef.LongName + ": expected one argument");
                    }

                    ValidateChoice(argDef, args[i]);
                    ns.Set(argDef.Dest, ConvertValue(args[i], argDef.Type));
                    return i + 1;
            }
        }

        private static object ConvertValue(string value, string type)
        {
            switch (type)
            {
                case "int":
                    if (int.TryParse(value, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out int intVal))
                    {
                        return intVal;
                    }

                    throw new ArgumentError("argument: invalid int value: '" + value + "'");

                case "float":
                    if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
                    {
                        return dblVal;
                    }

                    throw new ArgumentError("argument: invalid float value: '" + value + "'");

                case "str":
                default:
                    return value;
            }
        }

        private static object ConvertList(List<string> values, string type)
        {
            if (type == "str")
            {
                return values;
            }

            var result = new List<object?>();
            foreach (string v in values)
            {
                result.Append(ConvertValue(v, type));
            }

            return result;
        }

        private static void ValidateChoice(ArgumentDef argDef, string value)
        {
            if (argDef.Choices != null)
            {
                bool found = false;
                foreach (string choice in argDef.Choices)
                {
                    if (choice == value)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new ArgumentError(
                        "argument " + argDef.Dest + ": invalid choice: '" + value + "'");
                }
            }
        }

        private static string NormalizeDest(string name)
        {
            // --my-flag → my_flag
            string dest = name;
            while (dest.StartsWith("-"))
            {
                dest = dest.Substring(1);
            }

            return dest.Replace('-', '_');
        }

        private static void ValidateOptionalName(string name)
        {
            if (!name.StartsWith("-"))
            {
                throw new ValueError("optional argument names must start with '-'");
            }
        }

        private static object? GetDefaultForAction(string action, object? defaultValue)
        {
            if (defaultValue != null)
                return defaultValue;

            switch (action)
            {
                case "store_true":
                    return false;
                case "store_false":
                    return true;
                case "count":
                    return 0;
                case "append":
                    return null;
                default:
                    return null;
            }
        }

        internal sealed class ArgumentDef
        {
            public string? ShortName { get; set; }
            public string LongName { get; set; } = "";
            public string Dest { get; set; } = "";
            public string Type { get; set; } = "str";
            public string Help { get; set; } = "";
            public object? Default { get; set; }
            public bool Required { get; set; }
            public string Action { get; set; } = "store";
            public string? Nargs { get; set; }
            public List<string>? Choices { get; set; }
            public bool IsOptional { get; set; }
        }
    }
}
