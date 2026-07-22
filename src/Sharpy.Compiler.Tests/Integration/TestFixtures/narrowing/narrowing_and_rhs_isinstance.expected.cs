// Snapshot: and-RHS isinstance narrowing (#1081) — receiver cast to Dog before Bark()
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NarrowingAndRhsIsinstance
{
    public class Animal
    {
        public Animal()
#line 7 "narrowing_and_rhs_isinstance.spy"
        {
#line (8, 9) - (8, 14) 12 "narrowing_and_rhs_isinstance.spy"
            ;
#line hidden
        }
    }

    public class Dog : Animal
    {
        public string Bark()
#line 11 "narrowing_and_rhs_isinstance.spy"
        {
#line (12, 9) - (12, 23) 12 "narrowing_and_rhs_isinstance.spy"
            return "woof";
#line hidden
        }
    }

    public static bool IsBarkingDog(Animal a)
    {
#line (15, 5) - (15, 54) 8 "narrowing_and_rhs_isinstance.spy"
        return a is Dog && ((Dog)a).Bark() == "woof";
#line hidden
    }

    public static string Describe(Animal a)
    {
#line (18, 5) - (18, 58) 8 "narrowing_and_rhs_isinstance.spy"
        bool ok = a is Dog && ((Dog)a).Bark() == "woof";
#line (19, 5) - (21, 1) 8 "narrowing_and_rhs_isinstance.spy"
        if (ok)
#line hidden
        {
#line (20, 9) - (20, 30) 12 "narrowing_and_rhs_isinstance.spy"
            return "barking dog";
#line hidden
        }

#line (21, 5) - (21, 20) 8 "narrowing_and_rhs_isinstance.spy"
        return "other";
#line hidden
    }

    public static void Main()
    {
#line (24, 5) - (24, 23) 8 "narrowing_and_rhs_isinstance.spy"
        Animal d = new Dog();
#line (25, 5) - (25, 26) 8 "narrowing_and_rhs_isinstance.spy"
        Animal a = new Animal();
#line (26, 5) - (26, 29) 8 "narrowing_and_rhs_isinstance.spy"
        global::Sharpy.Builtins.Print(IsBarkingDog(d));
#line (27, 5) - (27, 29) 8 "narrowing_and_rhs_isinstance.spy"
        global::Sharpy.Builtins.Print(IsBarkingDog(a));
#line (28, 5) - (28, 23) 8 "narrowing_and_rhs_isinstance.spy"
        global::Sharpy.Builtins.Print(Describe(d));
#line (29, 5) - (29, 23) 8 "narrowing_and_rhs_isinstance.spy"
        global::Sharpy.Builtins.Print(Describe(a));
#line hidden
    }
}
#line default
