#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.ClassWithInit
{
    public static class Program
    {
        public static void Main()
        {
#line 71 "class_with_init.spy"
            Student s1 = new Student("Alice", 15, 1001, 9);
#line 72 "class_with_init.spy"
            Teacher t1 = new Teacher("Dr Smith", 45, "Math", 20);
#line 73 "class_with_init.spy"
            Classroom room = new Classroom(101, 30);
#line 75 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(s1.GetInfo());
#line 76 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(s1.GradeLevel);
#line 77 "class_with_init.spy"
            s1.AdvanceGrade();
#line 78 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(s1.GradeLevel);
#line 80 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(t1.GetInfo());
#line 81 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(t1.YearsExperience);
#line 82 "class_with_init.spy"
            t1.GainExperience(3);
#line 83 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(t1.YearsExperience);
#line 85 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(room.GetAvailableSeats());
#line 86 "class_with_init.spy"
            bool enrolled1 = room.EnrollStudent();
#line 87 "class_with_init.spy"
            bool enrolled2 = room.EnrollStudent();
#line 88 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(room.Enrolled);
#line 89 "class_with_init.spy"
            global::Sharpy.Core.Exports.Print(room.GetAvailableSeats());
        }
    }

    public abstract class Person
    {
        public string Name;
        public int Age;
        public abstract string GetRole();
        public int GetInfo()
        {
#line 17 "class_with_init.spy"
            return this.Age;
        }

        public Person(string name, int age)
        {
#line 9 "class_with_init.spy"
            this.Name = name;
#line 10 "class_with_init.spy"
            this.Age = age;
        }
    }

    public class Student : Person
    {
        public int StudentId;
        public int GradeLevel;
        public override string GetRole()
        {
#line 30 "class_with_init.spy"
            return "Student";
        }

        public void AdvanceGrade()
        {
#line 33 "class_with_init.spy"
            this.GradeLevel = this.GradeLevel + 1;
        }

        public Student(string name, int age, int sid, int level) : base(name, age)
        {
#line 25 "class_with_init.spy"
            this.StudentId = sid;
#line 26 "class_with_init.spy"
            this.GradeLevel = level;
        }
    }

    public class Teacher : Person
    {
        public string Subject;
        public int YearsExperience;
        public override string GetRole()
        {
#line 46 "class_with_init.spy"
            return "Teacher";
        }

        public void GainExperience(int years)
        {
#line 49 "class_with_init.spy"
            this.YearsExperience = this.YearsExperience + years;
        }

        public Teacher(string name, int age, string subj, int exp) : base(name, age)
        {
#line 41 "class_with_init.spy"
            this.Subject = subj;
#line 42 "class_with_init.spy"
            this.YearsExperience = exp;
        }
    }

    public class Classroom
    {
        public int RoomNumber;
        public int Capacity;
        public int Enrolled;
        public bool EnrollStudent()
        {
#line 62 "class_with_init.spy"
            if (this.Enrolled < this.Capacity)
            {
#line 63 "class_with_init.spy"
                this.Enrolled = this.Enrolled + 1;
#line 64 "class_with_init.spy"
                return true;
            }

#line 65 "class_with_init.spy"
            return false;
        }

        public int GetAvailableSeats()
        {
#line 68 "class_with_init.spy"
            return this.Capacity - this.Enrolled;
        }

        public Classroom(int room, int cap)
        {
#line 57 "class_with_init.spy"
            this.RoomNumber = room;
#line 58 "class_with_init.spy"
            this.Capacity = cap;
#line 59 "class_with_init.spy"
            this.Enrolled = 0;
        }
    }
}
