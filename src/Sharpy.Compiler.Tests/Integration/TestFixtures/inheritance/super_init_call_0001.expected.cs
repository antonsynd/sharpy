#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.SuperInitCall0001
{
    public static class Program
    {
        public static void Main()
        {
#line 17 "super_init_call_0001.spy"
            var dog = new Dog("Buddy", 5);
#line 18 "super_init_call_0001.spy"
            global::Sharpy.Core.Exports.Print(dog.Age);
#line 19 "super_init_call_0001.spy"
            global::Sharpy.Core.Exports.Print(dog.Name);
        }
    }

    public class Animal
    {
        public int Age;
        public Animal(int age)
        {
#line 7 "super_init_call_0001.spy"
            this.Age = age;
        }
    }

    public class Dog : Animal
    {
        public string Name;
        public Dog(string name, int age) : base(age)
        {
#line 14 "super_init_call_0001.spy"
            this.Name = name;
        }
    }
}
