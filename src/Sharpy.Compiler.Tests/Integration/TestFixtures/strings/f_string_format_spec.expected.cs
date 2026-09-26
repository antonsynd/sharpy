#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace FStringFormatSpec
{
    public static partial class FStringFormatSpecModule
    {
        public static void Main()
        {
#line (4, 5) - (4, 27) 12 "f_string_format_spec.spy"
            double price = 1000.0d;
#line (5, 5) - (5, 33) 12 "f_string_format_spec.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Price: {(global::Sharpy.PyFormat.Apply(price, ".2f"))}"));
#line (7, 5) - (7, 25) 12 "f_string_format_spec.spy"
            double pi = 3.14159d;
#line (8, 5) - (8, 27) 12 "f_string_format_spec.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Pi: {(global::Sharpy.PyFormat.Apply(pi, ".3f"))}"));
#line (10, 5) - (10, 17) 12 "f_string_format_spec.spy"
            int x = 42;
#line (11, 5) - (11, 29) 12 "f_string_format_spec.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Padded: {(global::Sharpy.PyFormat.Apply(x, "05"))}"));
#line (13, 5) - (13, 27) 12 "f_string_format_spec.spy"
            double percent = 0.75d;
#line (14, 5) - (14, 37) 12 "f_string_format_spec.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"Percent: {(global::Sharpy.PyFormat.Apply(percent, ".1%"))}"));
#line (16, 5) - (16, 31) 12 "f_string_format_spec.spy"
            int bigNumber = 1234567;
#line (17, 5) - (17, 42) 12 "f_string_format_spec.spy"
            global::Sharpy.Builtins.Print(FormattableString.Invariant($"With commas: {(global::Sharpy.PyFormat.Apply(bigNumber, ","))}"));
#line hidden
        }
    }
}
#line default
