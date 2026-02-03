#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.DefaultParamsCombinations
{
    public static class Program
    {
        public static void Greet(string name, string greeting = "Hello", string punctuation = "!")
        {
#line 4 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(greeting);
#line 5 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(name);
#line 6 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(punctuation);
        }

        public static int Calculate(int @base, int multiplier = 2, int offset = 0)
        {
#line 9 "default_params_combinations.spy"
            return @base * multiplier + offset;
        }

        public static void Main()
        {
#line 12 "default_params_combinations.spy"
            Greet("Alice");
#line 13 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(42);
#line 14 "default_params_combinations.spy"
            Greet("Bob", "Hi");
#line 15 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(42);
#line 16 "default_params_combinations.spy"
            Greet("Charlie", "Hey", ".");
#line 17 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(42);
#line 19 "default_params_combinations.spy"
            var result1 = Calculate(5);
#line 20 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 22 "default_params_combinations.spy"
            var result2 = Calculate(5, 3);
#line 23 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 25 "default_params_combinations.spy"
            var result3 = Calculate(5, 3, 10);
#line 26 "default_params_combinations.spy"
            global::Sharpy.Core.Exports.Print(result3);
        }
    }
}
