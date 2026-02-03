#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.EmptyListTypedField
{
    public static class Program
    {
        public static void Main()
        {
#line 16 "empty_list_typed_field.spy"
            TodoList todos = new TodoList();
#line 17 "empty_list_typed_field.spy"
            todos.Add("Write code");
#line 18 "empty_list_typed_field.spy"
            todos.Add("Run tests");
#line 19 "empty_list_typed_field.spy"
            global::Sharpy.Core.Exports.Print(todos.Count());
#line 20 "empty_list_typed_field.spy"
            global::Sharpy.Core.Exports.Print(todos.Items[0]);
        }
    }

    public class TodoList
    {
        public System.Collections.Generic.List<string> Items;
        public void Add(string task)
        {
#line 10 "empty_list_typed_field.spy"
            this.Items.Add(task);
        }

        public int Count()
        {
#line 13 "empty_list_typed_field.spy"
            return global::Sharpy.Core.Exports.Len(this.Items);
        }

        public TodoList()
        {
#line 7 "empty_list_typed_field.spy"
            this.Items = new System.Collections.Generic.List<string>()
            {
            };
        }
    }
}
