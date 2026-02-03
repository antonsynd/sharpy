#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FStringFormatSpec
{
    public static class Program
    {
        public static void Main()
        {
#line 4 "f_string_format_spec.spy"
            double price = 1000;
#line 5 "f_string_format_spec.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Price: {price:F2}"));
#line 7 "f_string_format_spec.spy"
            double pi = 3.14159;
#line 8 "f_string_format_spec.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Pi: {pi:F3}"));
#line 10 "f_string_format_spec.spy"
            int x = 42;
#line 11 "f_string_format_spec.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Padded: {x:D5}"));
#line 13 "f_string_format_spec.spy"
            double percent = 0.75;
#line 14 "f_string_format_spec.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Percent: {percent * 100:F1}%"));
#line 16 "f_string_format_spec.spy"
            int bigNumber = 1234567;
#line 17 "f_string_format_spec.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"With commas: {bigNumber:N0}"));
        }
    }
}
