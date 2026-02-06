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
            public Person(string name, int age)
            {
                this.Name = name;
                this.Age = age;
            }
        }

        public static void TestError()
        {
            Person p = new Person("Alice", 30);
            var age = p?.Age;
            global::Sharpy.Builtins.Print(age);
        }

        public static void Main()
        {
            TestError();
        }
    }
}
