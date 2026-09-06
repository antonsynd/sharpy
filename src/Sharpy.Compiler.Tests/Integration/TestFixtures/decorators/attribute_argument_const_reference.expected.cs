#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class AttributeArgumentConstReference
{
    public const string MSG = "Use bar() instead";
    public const bool FLAG = false;
    [System.Obsolete(MSG, false)]
    public static int ConstReference()
    {
#line (14, 5) - (14, 14) 8 "attribute_argument_const_reference.spy"
        return 1;
#line hidden
    }

    [System.Obsolete("Use " + "bar()", false)]
    public static int FoldedConcat()
    {
#line (19, 5) - (19, 14) 8 "attribute_argument_const_reference.spy"
        return 2;
#line hidden
    }

    [System.Obsolete(true ? "x" : "y", false)]
    public static int ConditionalOfLiterals()
    {
#line (24, 5) - (24, 14) 8 "attribute_argument_const_reference.spy"
        return 3;
#line hidden
    }

    [System.Obsolete(("paren"), false)]
    public static int Parenthesized()
    {
#line (29, 5) - (29, 14) 8 "attribute_argument_const_reference.spy"
        return 4;
#line hidden
    }

    [System.Obsolete("neg", !true)]
    public static int Negated()
    {
#line (34, 5) - (34, 14) 8 "attribute_argument_const_reference.spy"
        return 5;
#line hidden
    }

    [System.Obsolete(MSG, FLAG)]
    public static int TwoConstReferences()
    {
#line (39, 5) - (39, 14) 8 "attribute_argument_const_reference.spy"
        return 6;
#line hidden
    }

    public static void Main()
    {
#line (43, 5) - (44, 62) 8 "attribute_argument_const_reference.spy"
        global::Sharpy.Builtins.Print(ConstReference(), FoldedConcat(), ConditionalOfLiterals(), Parenthesized(), Negated(), TwoConstReferences());
#line hidden
    }
}
#line default
