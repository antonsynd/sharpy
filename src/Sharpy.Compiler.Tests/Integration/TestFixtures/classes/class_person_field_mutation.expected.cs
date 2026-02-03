#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassPersonFieldMutation
{
    public static class Program
    {
        public static void Main()
        {
#line 13 "class_person_field_mutation.spy"
            Person p = new Person("Alice", 25, true);
#line 14 "class_person_field_mutation.spy"
            global::Sharpy.Core.Exports.Print(p.Name);
#line 15 "class_person_field_mutation.spy"
            global::Sharpy.Core.Exports.Print(p.Age);
#line 16 "class_person_field_mutation.spy"
            global::Sharpy.Core.Exports.Print(p.IsStudent);
#line 18 "class_person_field_mutation.spy"
            p.Age = 26;
#line 19 "class_person_field_mutation.spy"
            p.IsStudent = false;
#line 20 "class_person_field_mutation.spy"
            global::Sharpy.Core.Exports.Print(p.Age);
#line 21 "class_person_field_mutation.spy"
            global::Sharpy.Core.Exports.Print(p.IsStudent);
        }
    }

    public class Person
    {
        public string Name;
        public int Age;
        public bool IsStudent;
        public Person(string name, int age, bool student)
        {
#line 8 "class_person_field_mutation.spy"
            this.Name = name;
#line 9 "class_person_field_mutation.spy"
            this.Age = age;
#line 10 "class_person_field_mutation.spy"
            this.IsStudent = student;
        }
    }
}
