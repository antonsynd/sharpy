#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy._20260118184613SuccessSuperInitCall0002
{
    public static class Program
    {
        public static void Main()
        {
#line 20 "20260118_184613_success_super_init_call_0002.spy"
            var d = new Dog("Buddy", 3);
#line 21 "20260118_184613_success_super_init_call_0002.spy"
            global::Sharpy.Core.Exports.Print(d.GetAge());
        }
    }

    public class Animal
    {
        public string Name;
        public Animal(string name)
        {
#line 7 "20260118_184613_success_super_init_call_0002.spy"
            this.Name = name;
        }
    }

    public class Dog : Animal
    {
        public int Age;
        public int GetAge()
        {
#line 17 "20260118_184613_success_super_init_call_0002.spy"
            return this.Age;
        }

        public Dog(string name, int age) : base(name)
        {
#line 14 "20260118_184613_success_super_init_call_0002.spy"
            this.Age = age;
        }
    }
}
