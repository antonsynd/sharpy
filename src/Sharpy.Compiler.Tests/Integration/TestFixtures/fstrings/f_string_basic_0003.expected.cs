#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.FStringBasic0003
{
    public static class Program
    {
        public static void Main()
        {
#line 4 "f_string_basic_0003.spy"
            string name = "Alice";
#line 5 "f_string_basic_0003.spy"
            int age = 25;
#line 6 "f_string_basic_0003.spy"
            int score = 87;
#line 8 "f_string_basic_0003.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Name: {name}"));
#line 9 "f_string_basic_0003.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Age: {age}"));
#line 10 "f_string_basic_0003.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Score: {score}"));
#line 11 "f_string_basic_0003.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"{name} is {age} years old"));
#line 12 "f_string_basic_0003.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Next year: {age + 1}"));
#line 13 "f_string_basic_0003.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Double score: {score * 2}"));
        }
    }
}
