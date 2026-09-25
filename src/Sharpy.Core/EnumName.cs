using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Sharpy
{
    public static partial class Builtins
    {
        // Per (enum type, member name): the python name recorded on the field, else the field name.
        private static readonly ConcurrentDictionary<(Type, string), string> _enumMemberPythonNames =
            new ConcurrentDictionary<(Type, string), string>();

        /// <summary>
        /// The python name of an enum member — what <c>Color.red.name</c> is (#2007, #2069). The ONE
        /// channel both <c>.name</c> and <see cref="Str(object)"/>/<see cref="Repr(object)"/> read:
        /// the <see cref="SharpyFieldNameAttribute"/> the compiler stamps on an int-enum field whose
        /// emitted C# identifier differs from the declared name (<c>red</c> → field <c>Red</c>), else
        /// the field name itself (<c>RED</c>, and every CLR interop enum's .NET name,
        /// <c>DayOfWeek.Monday</c>). A value with no member (an undeclared bit pattern) prints its
        /// .NET text.
        /// </summary>
        /// <remarks>
        /// A lowering target, not a python builtin: the compiler emits it for <c>e.name</c> on an
        /// int-backed enum. Hidden from the builtin surface like the other lowering targets.
        /// </remarks>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static string EnumName(Enum e)
        {
            var type = e.GetType();
            var clrName = Enum.GetName(type, e);
            if (clrName == null)
            {
                return e.ToString();
            }

            return _enumMemberPythonNames.GetOrAdd((type, clrName), key =>
            {
                var field = key.Item1.GetField(key.Item2, BindingFlags.Public | BindingFlags.Static);
                var recorded = field?.GetCustomAttribute<SharpyFieldNameAttribute>();
                return recorded?.PythonName ?? key.Item2;
            });
        }

        /// <summary>
        /// Python's <c>str</c> of an enum member, <c>Color.RED</c>: the type's python name, a dot,
        /// the member's python name (#2007). Every <see cref="Enum"/> — a Sharpy int enum and a CLR
        /// interop enum alike (owner ruling, "all").
        /// </summary>
        internal static string EnumStr(Enum e) => PyFormat.PyTypeName(e.GetType()) + "." + EnumName(e);

        /// <summary>
        /// Python's <c>repr</c> of an enum member, <c>&lt;Color.RED: 1&gt;</c>: the <c>str</c> and the
        /// repr of the underlying integer (#2007).
        /// </summary>
        internal static string EnumRepr(Enum e)
        {
            var underlying = Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()),
                System.Globalization.CultureInfo.InvariantCulture);
            return "<" + EnumStr(e) + ": " + Repr(underlying) + ">";
        }
    }
}
