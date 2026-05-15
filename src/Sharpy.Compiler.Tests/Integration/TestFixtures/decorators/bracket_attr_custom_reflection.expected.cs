#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;
using Attribute = global::System.Attribute;

public static partial class BracketAttrCustomReflection
{
    public class AuthorAttribute : System.Attribute
    {
        public string Name;
        public int Year;
        public AuthorAttribute(string name, int year) : base()
#line 8 "bracket_attr_custom_reflection.spy"
        {
#line (10, 9) - (10, 25) 1 "bracket_attr_custom_reflection.spy"
            this.Name = name;
#line (11, 9) - (11, 25) 1 "bracket_attr_custom_reflection.spy"
            this.Year = year;
        }
    }

    [AuthorAttribute("Alice", 2026)]
    public class Library
    {
    }

    public static void Main()
    {
#line (18, 5) - (18, 20) 1 "bracket_attr_custom_reflection.spy"
        var lib = new Library();
#line (19, 5) - (19, 23) 1 "bracket_attr_custom_reflection.spy"
        object obj = lib;
#line (20, 5) - (20, 23) 1 "bracket_attr_custom_reflection.spy"
        var t = obj.GetType();
#line (21, 5) - (21, 47) 1 "bracket_attr_custom_reflection.spy"
        var attrs = Attribute.GetCustomAttributes(t);
#line (22, 5) - (27, 1) 1 "bracket_attr_custom_reflection.spy"
        foreach (var __loopVar_0 in attrs)
        {
            var a = __loopVar_0;
#line (23, 9) - (23, 39) 1 "bracket_attr_custom_reflection.spy"
            var author = a is AuthorAttribute __coerce_temp_1 ? Optional<AuthorAttribute>.Some(__coerce_temp_1) : default;
#line (24, 9) - (27, 1) 1 "bracket_attr_custom_reflection.spy"
            if (author.IsSome)
            {
#line (25, 13) - (25, 31) 1 "bracket_attr_custom_reflection.spy"
                global::Sharpy.Builtins.Print(author.Unwrap().Name);
#line (26, 13) - (26, 31) 1 "bracket_attr_custom_reflection.spy"
                global::Sharpy.Builtins.Print(author.Unwrap().Year);
            }
        }
    }
}
