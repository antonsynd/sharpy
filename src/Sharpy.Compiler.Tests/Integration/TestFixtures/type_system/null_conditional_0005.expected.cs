// Snapshot: Null conditional operator (?.)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

public static partial class NullConditional0005
{
    public class Address
    {
        public Optional<Sharpy.Str> Street;
        public Optional<Sharpy.Str> City;
        public Optional<Sharpy.Str> GetCity()
        {
#line 12 "null_conditional_0005.spy"
            return this.City;
        }

        public Optional<Sharpy.Str> GetStreet()
        {
#line 15 "null_conditional_0005.spy"
            return this.Street;
        }

        public Address(Optional<Sharpy.Str> street, Optional<Sharpy.Str> city)
        {
#line 8 "null_conditional_0005.spy"
            this.Street = street;
#line 9 "null_conditional_0005.spy"
            this.City = city;
        }
    }

    public class Person
    {
        public Sharpy.Str Name;
        public Optional<Address> Address;
        public Optional<Sharpy.Str> GetCityName()
        {
#line 26 "null_conditional_0005.spy"
            return this.Address is var __opt_0 && (__opt_0).IsSome ? __opt_0.Unwrap().GetCity() : Optional<Sharpy.Str>.None;
        }

        public Optional<Sharpy.Str> GetStreetName()
        {
#line 29 "null_conditional_0005.spy"
            return this.Address is var __opt_1 && (__opt_1).IsSome ? __opt_1.Unwrap().GetStreet() : Optional<Sharpy.Str>.None;
        }

        public Person(Sharpy.Str name, Optional<Address> address)
        {
#line 22 "null_conditional_0005.spy"
            this.Name = name;
#line 23 "null_conditional_0005.spy"
            this.Address = address;
        }
    }

    public static void Main()
    {
#line 33 "null_conditional_0005.spy"
        var addr1 = new Address(((Sharpy.Str)"Main Street"), ((Sharpy.Str)"Springfield"));
#line 34 "null_conditional_0005.spy"
        var person1 = new Person(((Sharpy.Str)"Alice"), addr1);
#line 36 "null_conditional_0005.spy"
        Optional<Sharpy.Str> city1 = person1.GetCityName();
#line 37 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city1);
#line 39 "null_conditional_0005.spy"
        Optional<Sharpy.Str> street1 = person1.GetStreetName();
#line 40 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street1);
#line 43 "null_conditional_0005.spy"
        var person2 = new Person(((Sharpy.Str)"Bob"), default);
#line 45 "null_conditional_0005.spy"
        Optional<Sharpy.Str> city2 = person2.GetCityName();
#line 46 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city2);
#line 48 "null_conditional_0005.spy"
        Optional<Sharpy.Str> street2 = person2.GetStreetName();
#line 49 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street2);
#line 52 "null_conditional_0005.spy"
        var addr3 = new Address(default, ((Sharpy.Str)"Boston"));
#line 53 "null_conditional_0005.spy"
        var person3 = new Person(((Sharpy.Str)"Charlie"), addr3);
#line 55 "null_conditional_0005.spy"
        Optional<Sharpy.Str> city3 = person3.GetCityName();
#line 56 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city3);
#line 58 "null_conditional_0005.spy"
        Optional<Sharpy.Str> street3 = person3.GetStreetName();
#line 59 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street3);
    }
}
