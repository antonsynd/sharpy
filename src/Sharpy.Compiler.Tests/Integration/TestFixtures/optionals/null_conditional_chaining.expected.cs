#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NullConditionalChaining
{
    public class City
    {
        public string Name;
        public string GetName()
#line 5 "null_conditional_chaining.spy"
        {
#line (6, 9) - (6, 26) 1 "null_conditional_chaining.spy"
            return this.Name;
        }

        public City(string name)
#line 3 "null_conditional_chaining.spy"
        {
#line (4, 9) - (4, 25) 1 "null_conditional_chaining.spy"
            this.Name = name;
        }
    }

    public class Address
    {
        public Optional<City> City = Optional<City>.None;
        public Optional<City> GetCity()
#line 12 "null_conditional_chaining.spy"
        {
#line (13, 9) - (13, 26) 1 "null_conditional_chaining.spy"
            return this.City;
        }

        public Address(Optional<City> city)
#line 10 "null_conditional_chaining.spy"
        {
#line (11, 9) - (11, 25) 1 "null_conditional_chaining.spy"
            this.City = city;
        }
    }

    public class Person
    {
        public Optional<Address> Address = Optional<Address>.None;
        public Optional<Address> GetAddress()
#line 19 "null_conditional_chaining.spy"
        {
#line (20, 9) - (20, 29) 1 "null_conditional_chaining.spy"
            return this.Address;
        }

        public Person(Optional<Address> address)
#line 17 "null_conditional_chaining.spy"
        {
#line (18, 9) - (18, 31) 1 "null_conditional_chaining.spy"
            this.Address = address;
        }
    }

    public static void Main()
    {
#line (24, 5) - (24, 32) 1 "null_conditional_chaining.spy"
        City city = new City("Tokyo");
#line (25, 5) - (25, 41) 1 "null_conditional_chaining.spy"
        Address addr = new Address(Optional<City>.Some(city));
#line (26, 5) - (26, 37) 1 "null_conditional_chaining.spy"
        Person p1 = new Person(Optional<Address>.Some(addr));
#line (27, 5) - (27, 57) 1 "null_conditional_chaining.spy"
        Optional<string> r1 = (p1.GetAddress() is var __opt_0 && (__opt_0).IsSome ? __opt_0.Unwrap().GetCity() : Optional<City>.None) is var __opt_1 && (__opt_1).IsSome ? __opt_1.Unwrap().GetName() : Optional<string>.None;
#line (28, 5) - (28, 14) 1 "null_conditional_chaining.spy"
        global::Sharpy.Builtins.Print(r1);
#line (31, 5) - (31, 33) 1 "null_conditional_chaining.spy"
        Person p2 = new Person(Optional<Address>.None);
#line (32, 5) - (32, 57) 1 "null_conditional_chaining.spy"
        Optional<string> r2 = (p2.GetAddress() is var __opt_2 && (__opt_2).IsSome ? __opt_2.Unwrap().GetCity() : Optional<City>.None) is var __opt_3 && (__opt_3).IsSome ? __opt_3.Unwrap().GetName() : Optional<string>.None;
#line (33, 5) - (33, 14) 1 "null_conditional_chaining.spy"
        global::Sharpy.Builtins.Print(r2);
#line (36, 5) - (36, 38) 1 "null_conditional_chaining.spy"
        Address addr2 = new Address(Optional<City>.None);
#line (37, 5) - (37, 38) 1 "null_conditional_chaining.spy"
        Person p3 = new Person(Optional<Address>.Some(addr2));
#line (38, 5) - (38, 57) 1 "null_conditional_chaining.spy"
        Optional<string> r3 = (p3.GetAddress() is var __opt_4 && (__opt_4).IsSome ? __opt_4.Unwrap().GetCity() : Optional<City>.None) is var __opt_5 && (__opt_5).IsSome ? __opt_5.Unwrap().GetName() : Optional<string>.None;
#line (39, 5) - (39, 14) 1 "null_conditional_chaining.spy"
        global::Sharpy.Builtins.Print(r3);
    }
}
