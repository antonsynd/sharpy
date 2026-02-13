// Snapshot: Null conditional operator (?.)
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

public static partial class NullConditional0005
{
    public class Address
    {
        public Optional<string> Street;
        public Optional<string> City;
        public Optional<string> GetCity()
        {
#line 12 "null_conditional_0005.spy"
            return this.City;
        }

        public Optional<string> GetStreet()
        {
#line 15 "null_conditional_0005.spy"
            return this.Street;
        }

        public Address(Optional<string> street, Optional<string> city)
        {
#line 8 "null_conditional_0005.spy"
            this.Street = street;
#line 9 "null_conditional_0005.spy"
            this.City = city;
        }
    }

    public class Person
    {
        public string Name;
        public Optional<Address> Address;
        public Optional<string> GetCityName()
        {
#line 26 "null_conditional_0005.spy"
            return (this.Address).IsSome ? this.Address.Unwrap().GetCity() : default;
        }

        public Optional<string> GetStreetName()
        {
#line 29 "null_conditional_0005.spy"
            return (this.Address).IsSome ? this.Address.Unwrap().GetStreet() : default;
        }

        public Person(string name, Optional<Address> address)
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
        var addr1 = new Address("Main Street", "Springfield");
#line 34 "null_conditional_0005.spy"
        var person1 = new Person("Alice", addr1);
#line 36 "null_conditional_0005.spy"
        Optional<string> city1 = person1.GetCityName();
#line 37 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city1);
#line 39 "null_conditional_0005.spy"
        Optional<string> street1 = person1.GetStreetName();
#line 40 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street1);
#line 43 "null_conditional_0005.spy"
        var person2 = new Person("Bob", null);
#line 45 "null_conditional_0005.spy"
        Optional<string> city2 = person2.GetCityName();
#line 46 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city2);
#line 48 "null_conditional_0005.spy"
        Optional<string> street2 = person2.GetStreetName();
#line 49 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street2);
#line 52 "null_conditional_0005.spy"
        var addr3 = new Address(null, "Boston");
#line 53 "null_conditional_0005.spy"
        var person3 = new Person("Charlie", addr3);
#line 55 "null_conditional_0005.spy"
        Optional<string> city3 = person3.GetCityName();
#line 56 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city3);
#line 58 "null_conditional_0005.spy"
        Optional<string> street3 = person3.GetStreetName();
#line 59 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street3);
    }
}
