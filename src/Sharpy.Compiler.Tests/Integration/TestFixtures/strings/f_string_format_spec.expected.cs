#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class FStringFormatSpec
{
    public static void Main()
    {
#line (4, 5) - (4, 27) 1 "f_string_format_spec.spy"
        double price = 1000.0d;
#line (5, 5) - (5, 33) 1 "f_string_format_spec.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Price: {(price):F2}"));
#line (7, 5) - (7, 25) 1 "f_string_format_spec.spy"
        double pi = 3.14159d;
#line (8, 5) - (8, 27) 1 "f_string_format_spec.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Pi: {(pi):F3}"));
#line (10, 5) - (10, 17) 1 "f_string_format_spec.spy"
        int x = 42;
#line (11, 5) - (11, 29) 1 "f_string_format_spec.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Padded: {(x):D5}"));
#line (13, 5) - (13, 27) 1 "f_string_format_spec.spy"
        double percent = 0.75d;
#line (14, 5) - (14, 37) 1 "f_string_format_spec.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"Percent: {(percent * 100):F1}%"));
#line (16, 5) - (16, 31) 1 "f_string_format_spec.spy"
        int bigNumber = 1234567;
#line (17, 5) - (17, 42) 1 "f_string_format_spec.spy"
        global::Sharpy.Builtins.Print(FormattableString.Invariant($"With commas: {(bigNumber):N0}"));
    }
}
