#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy;

namespace Sharpy
{
    public static partial class Program
    {
        public class Person
        {
            public string Name;
            public int Age;
            public string Greet()
            {
                return FormattableString.Invariant($"Hello, I'm {this.Name}");
            }

            public Person(string name, int age)
            {
                this.Name = name;
                this.Age = age;
            }
        }

        public static void TestFieldAccess()
        {
            Person? p = new Person("Alice", 30);
            var ageNullable = p?.Age;
            global::Sharpy.Builtins.Print(ageNullable);
        }

        public static void TestMethodCall()
        {
            Person? p = null;
            var greeting = p?.Greet();
            global::Sharpy.Builtins.Print(greeting);
        }

        public class Address
        {
            public string City;
            public Address(string city)
            {
                this.City = city;
            }
        }

        public class PersonWithAddress
        {
            public string Name;
            public Address? Address;
            public PersonWithAddress(string name, Address? address)
            {
                this.Name = name;
                this.Address = address;
            }
        }

        public static void TestChained()
        {
            PersonWithAddress? p = new PersonWithAddress("Bob", null);
            var city = p?.Address?.City;
            global::Sharpy.Builtins.Print(city);
        }

        public static void TestWithCoalesce()
        {
            Person? p = null;
            int age = p?.Age ?? 0;
            global::Sharpy.Builtins.Print(age);
        }

        public static void TestErrorNonNullable()
        {
            Person p = new Person("Charlie", 25);
        }

        public static void Main()
        {
            TestFieldAccess();
            TestMethodCall();
            TestChained();
            TestWithCoalesce();
        }
    }
}