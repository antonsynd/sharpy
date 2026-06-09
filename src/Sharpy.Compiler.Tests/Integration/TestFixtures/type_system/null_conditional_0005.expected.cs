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
        public Optional<string> Street;
        public Optional<string> City;
        public Optional<string> GetCity()
#line 11 "null_conditional_0005.spy"
        {
#line (12, 9) - (12, 26) 1 "null_conditional_0005.spy"
            return this.City;
        }

        public Optional<string> GetStreet()
#line 14 "null_conditional_0005.spy"
        {
#line (15, 9) - (15, 28) 1 "null_conditional_0005.spy"
            return this.Street;
        }

        public Address(Optional<string> street, Optional<string> city)
#line 7 "null_conditional_0005.spy"
        {
#line (8, 9) - (8, 29) 1 "null_conditional_0005.spy"
            this.Street = street;
#line (9, 9) - (9, 25) 1 "null_conditional_0005.spy"
            this.City = city;
        }
    }

    public class Person
    {
        public string Name;
        public Optional<Address> Address;
        public Optional<string> GetCityName()
#line 25 "null_conditional_0005.spy"
        {
#line (26, 9) - (26, 41) 1 "null_conditional_0005.spy"
            return this.Address is var __opt_0 && (__opt_0).IsSome ? __opt_0.Unwrap().GetCity() : Optional<string>.None;
        }

        public Optional<string> GetStreetName()
#line 28 "null_conditional_0005.spy"
        {
#line (29, 9) - (29, 43) 1 "null_conditional_0005.spy"
            return this.Address is var __opt_1 && (__opt_1).IsSome ? __opt_1.Unwrap().GetStreet() : Optional<string>.None;
        }

        public Person(string name, Optional<Address> address)
#line 21 "null_conditional_0005.spy"
        {
#line (22, 9) - (22, 25) 1 "null_conditional_0005.spy"
            this.Name = name;
#line (23, 9) - (23, 31) 1 "null_conditional_0005.spy"
            this.Address = address;
        }
    }

    public static void Main()
    {
#line (33, 5) - (33, 50) 1 "null_conditional_0005.spy"
        var addr1 = new Address("Main Street", "Springfield");
#line (34, 5) - (34, 37) 1 "null_conditional_0005.spy"
        var person1 = new Person("Alice", addr1);
#line (36, 5) - (36, 43) 1 "null_conditional_0005.spy"
        Optional<string> city1 = person1.GetCityName();
#line (37, 5) - (37, 17) 1 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city1);
#line (39, 5) - (39, 47) 1 "null_conditional_0005.spy"
        Optional<string> street1 = person1.GetStreetName();
#line (40, 5) - (40, 19) 1 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street1);
#line (43, 5) - (43, 34) 1 "null_conditional_0005.spy"
        var person2 = new Person("Bob", null);
#line (45, 5) - (45, 43) 1 "null_conditional_0005.spy"
        Optional<string> city2 = person2.GetCityName();
#line (46, 5) - (46, 17) 1 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city2);
#line (48, 5) - (48, 47) 1 "null_conditional_0005.spy"
        Optional<string> street2 = person2.GetStreetName();
#line (49, 5) - (49, 19) 1 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street2);
#line (52, 5) - (52, 36) 1 "null_conditional_0005.spy"
        var addr3 = new Address(null, "Boston");
#line (53, 5) - (53, 39) 1 "null_conditional_0005.spy"
        var person3 = new Person("Charlie", addr3);
#line (55, 5) - (55, 43) 1 "null_conditional_0005.spy"
        Optional<string> city3 = person3.GetCityName();
#line (56, 5) - (56, 17) 1 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(city3);
#line (58, 5) - (58, 47) 1 "null_conditional_0005.spy"
        Optional<string> street3 = person3.GetStreetName();
#line (59, 5) - (59, 19) 1 "null_conditional_0005.spy"
        global::Sharpy.Builtins.Print(street3);
    }
}
