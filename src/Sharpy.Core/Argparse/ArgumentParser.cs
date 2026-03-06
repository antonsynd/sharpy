using System;
using System.Collections.Generic;
using System.IO;

namespace Sharpy
{
    /// <summary>
    /// Python-like argument parser for command-line arguments.
    /// </summary>
    public class ArgumentParser
    {
        private readonly string _description;
        private readonly string _prog;
        private readonly System.Collections.Generic.List<ArgDef> _args = new System.Collections.Generic.List<ArgDef>();

        public ArgumentParser(string description = "", string prog = "")
        {
            _description = description;
            _prog = string.IsNullOrEmpty(prog) ? "prog" : prog;
            // Auto-add --help / -h
            _args.Add(new ArgDef
            {
                Name = "help",
                ShortFlag = "-h",
                LongFlag = "--help",
                Action = "help",
                Help = "show this help message and exit",
                IsPositional = false,
            });
        }

        /// <summary>
        /// Add a positional or optional argument.
        /// If nameOrFlag starts with '-', it's treated as an optional argument.
        /// Otherwise, it's a positional argument.
        /// </summary>
        public void AddArgument(string nameOrFlag, string help = "", object? @default = null, Func<string, object>? type = null, string action = "store", bool required = false, string? nargs = null, List<string>? choices = null)
        {
            if (!nameOrFlag.StartsWith("-"))
            {
                // Positional argument
                _args.Add(new ArgDef
                {
                    Name = nameOrFlag,
                    IsPositional = true,
                    Help = help,
                    Default = @default,
                    TypeConverter = type,
                    Nargs = nargs,
                    Choices = choices,
                });
                return;
            }

            string name = nameOrFlag.TrimStart('-').Replace("-", "_");
            _args.Add(new ArgDef
            {
                Name = name,
                LongFlag = nameOrFlag.StartsWith("--") ? nameOrFlag : null,
                ShortFlag = nameOrFlag.StartsWith("--") ? null : nameOrFlag,
                IsPositional = false,
                Help = help,
                Default = @default,
                TypeConverter = type,
                Action = action,
                Required = required,
                Nargs = nargs,
                Choices = choices,
            });
        }

        /// <summary>
        /// Add an optional argument with short and long flags.
        /// </summary>
        public void AddArgument(string shortFlag, string longFlag, string help = "", object? @default = null, Func<string, object>? type = null, string action = "store", bool required = false, string? nargs = null, List<string>? choices = null)
        {
            string name = longFlag.TrimStart('-').Replace("-", "_");
            _args.Add(new ArgDef
            {
                Name = name,
                ShortFlag = shortFlag,
                LongFlag = longFlag,
                IsPositional = false,
                Help = help,
                Default = @default,
                TypeConverter = type,
                Action = action,
                Required = required,
                Nargs = nargs,
                Choices = choices,
            });
        }

        /// <summary>
        /// Parse command-line arguments from sys.argv (skipping first element).
        /// </summary>
        public Namespace ParseArgs()
        {
            var argv = Sys.Argv;
            var args = new System.Collections.Generic.List<string>();
            // Skip first element (program name)
            for (int i = 1; i < argv.Length; i++)
            {
                args.Add(argv[i]);
            }
            return ParseArgs(args);
        }

        /// <summary>
        /// Parse the given list of arguments.
        /// </summary>
        public Namespace ParseArgs(System.Collections.Generic.List<string> args)
        {
            return ParseArgs(args.ToArray());
        }

        /// <summary>
        /// Parse the given list of arguments.
        /// </summary>
        public Namespace ParseArgs(List<string> args)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var a in args)
            {
                list.Add(a);
            }
            return ParseArgs(list.ToArray());
        }

        private Namespace ParseArgs(string[] args)
        {
            var ns = new Namespace();

            // Set defaults
            foreach (var def in _args)
            {
                if (def.Action == "help")
                    continue;
                if (def.Action == "store_true")
                    ns.Set(def.Name, def.Default ?? false);
                else if (def.Action == "store_false")
                    ns.Set(def.Name, def.Default ?? true);
                else if (def.Action == "count")
                    ns.Set(def.Name, def.Default ?? 0);
                else if (def.Action == "append")
                    ns.Set(def.Name, new List<object?>());
                else
                    ns.Set(def.Name, def.Default);
            }

            int positionalIndex = 0;
            var positionals = GetPositionals();

            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];

                if (arg == "-h" || arg == "--help")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }

                if (arg.StartsWith("-"))
                {
                    var def = FindOptional(arg);
                    if (def == null)
                    {
                        Error("unrecognized arguments: " + arg);
                        return ns; // unreachable, Error exits
                    }

                    switch (def.Action)
                    {
                        case "store_true":
                            ns.Set(def.Name, true);
                            break;
                        case "store_false":
                            ns.Set(def.Name, false);
                            break;
                        case "count":
                            ns.Set(def.Name, (int)ns.Get(def.Name)! + 1);
                            break;
                        case "append":
                            i++;
                            if (i >= args.Length)
                                Error("argument " + arg + ": expected one argument");
                            var appendList = (List<object?>)ns.Get(def.Name)!;
                            appendList.Append(ConvertValue(def, args[i]));
                            break;
                        default: // "store"
                            if (def.Nargs != null)
                            {
                                var collected = CollectNargs(def, args, ref i);
                                ns.Set(def.Name, collected);
                            }
                            else
                            {
                                i++;
                                if (i >= args.Length)
                                    Error("argument " + arg + ": expected one argument");
                                ns.Set(def.Name, ConvertValue(def, args[i]));
                            }
                            break;
                    }
                }
                else
                {
                    // Positional argument
                    if (positionalIndex >= positionals.Count)
                    {
                        Error("unrecognized arguments: " + arg);
                    }

                    var def = positionals[positionalIndex];
                    if (def.Nargs != null)
                    {
                        var collected = new List<object?>();
                        while (i < args.Length && !args[i].StartsWith("-"))
                        {
                            collected.Append(ConvertValue(def, args[i]));
                            i++;
                        }
                        ns.Set(def.Name, collected);
                        positionalIndex++;
                        continue; // don't increment i again
                    }
                    else
                    {
                        ns.Set(def.Name, ConvertValue(def, arg));
                        positionalIndex++;
                    }
                }

                i++;
            }

            // Check required positionals
            foreach (var def in positionals)
            {
                if (def.Nargs == null || def.Nargs == "+")
                {
                    if (ns.Get(def.Name) == null || (def.Nargs == "+" && ns.Get(def.Name) is List<object?> l && Builtins.Len(l) == 0))
                    {
                        Error("the following arguments are required: " + def.Name);
                    }
                }
            }

            // Check required optionals
            foreach (var def in _args)
            {
                if (!def.IsPositional && def.Required && def.Action != "help")
                {
                    if (ns.Get(def.Name) == null)
                    {
                        string flag = def.LongFlag ?? def.ShortFlag ?? def.Name;
                        Error("the following arguments are required: " + flag);
                    }
                }
            }

            return ns;
        }

        private object? ConvertValue(ArgDef def, string value)
        {
            if (def.Choices != null)
            {
                bool found = false;
                foreach (var c in def.Choices)
                {
                    if (c == value)
                    { found = true; break; }
                }
                if (!found)
                {
                    Error("argument " + def.Name + ": invalid choice: '" + value + "'");
                }
            }

            if (def.TypeConverter != null)
            {
                try
                {
                    return def.TypeConverter(value);
                }
                catch (Exception)
                {
                    string flag = def.LongFlag ?? def.ShortFlag ?? def.Name;
                    Error("argument " + flag + ": invalid value: '" + value + "'");
                    return null; // unreachable
                }
            }

            return value;
        }

        private List<object?> CollectNargs(ArgDef def, string[] args, ref int i)
        {
            var result = new List<object?>();
            string nargs = def.Nargs!;

            if (nargs == "?")
            {
                i++;
                if (i < args.Length && !args[i].StartsWith("-"))
                {
                    result.Append(ConvertValue(def, args[i]));
                }
                else
                {
                    i--; // didn't consume
                }
                return result;
            }

            if (nargs == "*" || nargs == "+")
            {
                i++;
                while (i < args.Length && !args[i].StartsWith("-"))
                {
                    result.Append(ConvertValue(def, args[i]));
                    i++;
                }
                i--; // back up one since outer loop will increment

                if (nargs == "+" && Builtins.Len(result) == 0)
                {
                    string flag = def.LongFlag ?? def.ShortFlag ?? def.Name;
                    Error("argument " + flag + ": expected at least one argument");
                }
                return result;
            }

            // Numeric nargs
            if (int.TryParse(nargs, out int count))
            {
                for (int n = 0; n < count; n++)
                {
                    i++;
                    if (i >= args.Length)
                    {
                        string flag = def.LongFlag ?? def.ShortFlag ?? def.Name;
                        Error("argument " + flag + ": expected " + count + " argument(s)");
                    }
                    result.Append(ConvertValue(def, args[i]));
                }
                return result;
            }

            return result;
        }

        private System.Collections.Generic.List<ArgDef> GetPositionals()
        {
            var result = new System.Collections.Generic.List<ArgDef>();
            foreach (var def in _args)
            {
                if (def.IsPositional)
                    result.Add(def);
            }
            return result;
        }

        private ArgDef? FindOptional(string flag)
        {
            foreach (var def in _args)
            {
                if (!def.IsPositional && (def.ShortFlag == flag || def.LongFlag == flag))
                    return def;
            }
            return null;
        }

        private void Error(string message)
        {
            Console.Error.WriteLine("usage: " + _prog);
            Console.Error.WriteLine(_prog + ": error: " + message);
            Environment.Exit(2);
        }

        /// <summary>
        /// Print help message to stdout.
        /// </summary>
        public void PrintHelp()
        {
            var positionals = GetPositionals();
            Console.Write("usage: " + _prog);

            // Show optional flags in usage
            foreach (var def in _args)
            {
                if (!def.IsPositional && def.Action != "help")
                {
                    string flag = def.ShortFlag ?? def.LongFlag ?? "";
                    if (def.Required)
                        Console.Write(" " + flag + " " + def.Name.ToUpper());
                    else
                        Console.Write(" [" + flag + " " + def.Name.ToUpper() + "]");
                }
            }

            // Show help flag
            Console.Write(" [-h]");

            // Show positional args
            foreach (var def in positionals)
            {
                Console.Write(" " + def.Name);
            }
            Console.WriteLine();

            if (!string.IsNullOrEmpty(_description))
            {
                Console.WriteLine();
                Console.WriteLine(_description);
            }

            // Positional arguments
            if (positionals.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("positional arguments:");
                foreach (var def in positionals)
                {
                    Console.Write("  " + def.Name);
                    if (!string.IsNullOrEmpty(def.Help))
                    {
                        Console.Write(new string(' ', System.Math.Max(1, 22 - def.Name.Length - 2)));
                        Console.Write(def.Help);
                    }
                    Console.WriteLine();
                }
            }

            // Optional arguments
            Console.WriteLine();
            Console.WriteLine("options:");
            foreach (var def in _args)
            {
                if (def.IsPositional)
                    continue;

                string flags = "";
                if (def.ShortFlag != null && def.LongFlag != null)
                    flags = def.ShortFlag + ", " + def.LongFlag;
                else if (def.LongFlag != null)
                    flags = def.LongFlag;
                else if (def.ShortFlag != null)
                    flags = def.ShortFlag;

                Console.Write("  " + flags);
                if (!string.IsNullOrEmpty(def.Help))
                {
                    Console.Write(new string(' ', System.Math.Max(1, 22 - flags.Length - 2)));
                    Console.Write(def.Help);
                }
                Console.WriteLine();
            }
        }

        private class ArgDef
        {
            public string Name { get; set; } = "";
            public string? ShortFlag { get; set; }
            public string? LongFlag { get; set; }
            public bool IsPositional { get; set; }
            public string Help { get; set; } = "";
            public object? Default { get; set; }
            public Func<string, object>? TypeConverter { get; set; }
            public string Action { get; set; } = "store";
            public bool Required { get; set; }
            public string? Nargs { get; set; }
            public List<string>? Choices { get; set; }
        }
    }
}
