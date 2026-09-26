// Snapshot: and-RHS isinstance narrowing (#1081) — receiver cast to Dog before Bark()
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NarrowingAndRhsIsinstance
{
    public static partial class NarrowingAndRhsIsinstanceModule
    {
        public static bool IsBarkingDog(global::NarrowingAndRhsIsinstance.Animal a)
        {
#line (15, 5) - (15, 54) 12 "narrowing_and_rhs_isinstance.spy"
            return (object?)a is Dog && ((global::NarrowingAndRhsIsinstance.Dog)a!).Bark() == "woof";
#line hidden
        }

        public static string Describe(global::NarrowingAndRhsIsinstance.Animal a)
        {
#line (18, 5) - (18, 58) 12 "narrowing_and_rhs_isinstance.spy"
            bool ok = (object?)a is Dog && ((global::NarrowingAndRhsIsinstance.Dog)a!).Bark() == "woof";
#line (19, 5) - (20, 30) 12 "narrowing_and_rhs_isinstance.spy"
            if (ok)
#line hidden
            {
#line (20, 9) - (20, 30) 16 "narrowing_and_rhs_isinstance.spy"
                return "barking dog";
#line hidden
            }

#line (21, 5) - (21, 20) 12 "narrowing_and_rhs_isinstance.spy"
            return "other";
#line hidden
        }

        public static void Main()
        {
#line (24, 5) - (24, 23) 12 "narrowing_and_rhs_isinstance.spy"
            global::NarrowingAndRhsIsinstance.Animal d = new global::NarrowingAndRhsIsinstance.Dog();
#line (25, 5) - (25, 26) 12 "narrowing_and_rhs_isinstance.spy"
            global::NarrowingAndRhsIsinstance.Animal a = new global::NarrowingAndRhsIsinstance.Animal();
#line (26, 5) - (26, 29) 12 "narrowing_and_rhs_isinstance.spy"
            global::Sharpy.Builtins.Print(global::NarrowingAndRhsIsinstance.NarrowingAndRhsIsinstanceModule.IsBarkingDog(d));
#line (27, 5) - (27, 29) 12 "narrowing_and_rhs_isinstance.spy"
            global::Sharpy.Builtins.Print(global::NarrowingAndRhsIsinstance.NarrowingAndRhsIsinstanceModule.IsBarkingDog(a));
#line (28, 5) - (28, 23) 12 "narrowing_and_rhs_isinstance.spy"
            global::Sharpy.Builtins.Print(global::NarrowingAndRhsIsinstance.NarrowingAndRhsIsinstanceModule.Describe(d));
#line (29, 5) - (29, 23) 12 "narrowing_and_rhs_isinstance.spy"
            global::Sharpy.Builtins.Print(global::NarrowingAndRhsIsinstance.NarrowingAndRhsIsinstanceModule.Describe(a));
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Animal")]
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

    [global::Sharpy.SharpyModuleType("__main__", "Dog")]
    public class Dog : global::NarrowingAndRhsIsinstance.Animal
    {
        public string Bark()
#line 11 "narrowing_and_rhs_isinstance.spy"
        {
#line (12, 9) - (12, 23) 12 "narrowing_and_rhs_isinstance.spy"
            return "woof";
#line hidden
        }
    }
}
#line default
