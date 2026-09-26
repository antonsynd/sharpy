#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace NullConditionalChaining
{
    public static partial class NullConditionalChainingModule
    {
        public static void Main()
        {
#line (24, 5) - (24, 32) 12 "null_conditional_chaining.spy"
            global::NullConditionalChaining.City city = new global::NullConditionalChaining.City("Tokyo");
#line (25, 5) - (25, 41) 12 "null_conditional_chaining.spy"
            global::NullConditionalChaining.Address addr = new global::NullConditionalChaining.Address(Optional<global::NullConditionalChaining.City>.Some(city));
#line (26, 5) - (26, 37) 12 "null_conditional_chaining.spy"
            global::NullConditionalChaining.Person p1 = new global::NullConditionalChaining.Person(Optional<global::NullConditionalChaining.Address>.Some(addr));
#line (27, 5) - (27, 57) 12 "null_conditional_chaining.spy"
            Optional<string> r1 = (p1.GetAddress() is var __opt_0 && (__opt_0).IsSome ? __opt_0.Unwrap().GetCity() : Optional<global::NullConditionalChaining.City>.None) is var __opt_1 && (__opt_1).IsSome ? Optional<string>.Some(__opt_1.Unwrap().GetName()) : Optional<string>.None;
#line (28, 5) - (28, 14) 12 "null_conditional_chaining.spy"
            global::Sharpy.Builtins.Print(r1);
#line (31, 5) - (31, 33) 12 "null_conditional_chaining.spy"
            global::NullConditionalChaining.Person p2 = new global::NullConditionalChaining.Person(Optional<global::NullConditionalChaining.Address>.None);
#line (32, 5) - (32, 57) 12 "null_conditional_chaining.spy"
            Optional<string> r2 = (p2.GetAddress() is var __opt_2 && (__opt_2).IsSome ? __opt_2.Unwrap().GetCity() : Optional<global::NullConditionalChaining.City>.None) is var __opt_3 && (__opt_3).IsSome ? Optional<string>.Some(__opt_3.Unwrap().GetName()) : Optional<string>.None;
#line (33, 5) - (33, 14) 12 "null_conditional_chaining.spy"
            global::Sharpy.Builtins.Print(r2);
#line (36, 5) - (36, 38) 12 "null_conditional_chaining.spy"
            global::NullConditionalChaining.Address addr2 = new global::NullConditionalChaining.Address(Optional<global::NullConditionalChaining.City>.None);
#line (37, 5) - (37, 38) 12 "null_conditional_chaining.spy"
            global::NullConditionalChaining.Person p3 = new global::NullConditionalChaining.Person(Optional<global::NullConditionalChaining.Address>.Some(addr2));
#line (38, 5) - (38, 57) 12 "null_conditional_chaining.spy"
            Optional<string> r3 = (p3.GetAddress() is var __opt_4 && (__opt_4).IsSome ? __opt_4.Unwrap().GetCity() : Optional<global::NullConditionalChaining.City>.None) is var __opt_5 && (__opt_5).IsSome ? Optional<string>.Some(__opt_5.Unwrap().GetName()) : Optional<string>.None;
#line (39, 5) - (39, 14) 12 "null_conditional_chaining.spy"
            global::Sharpy.Builtins.Print(r3);
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "City")]
    public class City
    {
        public string Name;
        public string GetName()
#line 5 "null_conditional_chaining.spy"
        {
#line (6, 9) - (6, 26) 12 "null_conditional_chaining.spy"
            return this.Name;
#line hidden
        }

        public City(string name)
#line 3 "null_conditional_chaining.spy"
        {
#line (4, 9) - (4, 25) 12 "null_conditional_chaining.spy"
            this.Name = name;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Address")]
    public class Address
    {
        public Optional<global::NullConditionalChaining.City> City = Optional<global::NullConditionalChaining.City>.None;
        public Optional<global::NullConditionalChaining.City> GetCity()
#line 12 "null_conditional_chaining.spy"
        {
#line (13, 9) - (13, 26) 12 "null_conditional_chaining.spy"
            return this.City;
#line hidden
        }

        public Address(Optional<global::NullConditionalChaining.City> city)
#line 10 "null_conditional_chaining.spy"
        {
#line (11, 9) - (11, 25) 12 "null_conditional_chaining.spy"
            this.City = city;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Person")]
    public class Person
    {
        public Optional<global::NullConditionalChaining.Address> Address = Optional<global::NullConditionalChaining.Address>.None;
        public Optional<global::NullConditionalChaining.Address> GetAddress()
#line 19 "null_conditional_chaining.spy"
        {
#line (20, 9) - (20, 29) 12 "null_conditional_chaining.spy"
            return this.Address;
#line hidden
        }

        public Person(Optional<global::NullConditionalChaining.Address> address)
#line 17 "null_conditional_chaining.spy"
        {
#line (18, 9) - (18, 31) 12 "null_conditional_chaining.spy"
            this.Address = address;
#line hidden
        }
    }
}
#line default
