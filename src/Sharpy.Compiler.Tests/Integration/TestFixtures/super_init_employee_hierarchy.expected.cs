#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.SuperInitEmployeeHierarchy
{
    public static class Program
    {
        public static void Main()
        {
#line 35 "super_init_employee_hierarchy.spy"
            var emp = new Employee("Alice", 30, 12345, 75000.5);
#line 36 "super_init_employee_hierarchy.spy"
            emp.Describe();
#line 38 "super_init_employee_hierarchy.spy"
            if (emp.Salary > 70000)
            {
#line 39 "super_init_employee_hierarchy.spy"
                global::Sharpy.Core.Exports.Print("High earner");
            }
            else
            {
#line 41 "super_init_employee_hierarchy.spy"
                global::Sharpy.Core.Exports.Print("Standard earner");
            }
        }
    }

    public class Person
    {
        public string Name;
        public int Age;
        public virtual void Describe()
        {
#line 14 "super_init_employee_hierarchy.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Name: {this.Name}"));
#line 15 "super_init_employee_hierarchy.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Age: {this.Age}"));
        }

        public Person(string name, int age)
        {
#line 9 "super_init_employee_hierarchy.spy"
            this.Name = name;
#line 10 "super_init_employee_hierarchy.spy"
            this.Age = age;
        }
    }

    public class Employee : Person
    {
        public int EmployeeId;
        public double Salary;
        public override void Describe()
        {
#line 29 "super_init_employee_hierarchy.spy"
            base.Describe();
#line 30 "super_init_employee_hierarchy.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"ID: {this.EmployeeId}"));
#line 31 "super_init_employee_hierarchy.spy"
            global::Sharpy.Core.Exports.Print(FormattableString.Invariant($"Salary: {this.Salary}"));
        }

        public Employee(string name, int age, int empId, double salary) : base(name, age)
        {
#line 24 "super_init_employee_hierarchy.spy"
            this.EmployeeId = empId;
#line 25 "super_init_employee_hierarchy.spy"
            this.Salary = salary;
        }
    }
}
