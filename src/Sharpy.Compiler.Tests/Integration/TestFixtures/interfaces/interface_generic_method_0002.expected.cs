#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.InterfaceGenericMethod0002
{
    public static class Program
    {
        public static int RunIntProcessor(IProcessor proc, int value)
        {
#line 30 "interface_generic_method_0002.spy"
            return proc.ProcessInt(value);
        }

        public static string RunStrProcessor(IProcessor proc, string value)
        {
#line 33 "interface_generic_method_0002.spy"
            return proc.ProcessStr(value);
        }

        public static void Main()
        {
#line 36 "interface_generic_method_0002.spy"
            IProcessor proc = new MyProcessor("test");
#line 39 "interface_generic_method_0002.spy"
            int result1 = RunIntProcessor(proc, 5);
#line 40 "interface_generic_method_0002.spy"
            global::Sharpy.Core.Exports.Print(result1);
#line 43 "interface_generic_method_0002.spy"
            string result2 = RunStrProcessor(proc, "hello");
#line 44 "interface_generic_method_0002.spy"
            global::Sharpy.Core.Exports.Print(result2);
#line 47 "interface_generic_method_0002.spy"
            string name = proc.GetName();
#line 48 "interface_generic_method_0002.spy"
            global::Sharpy.Core.Exports.Print(name);
        }
    }

    public interface IProcessor
    {
        int ProcessInt(int x);
        string ProcessStr(string s);
        string GetName();
    }

    public class MyProcessor : IProcessor
    {
        public string Name;
        public int ProcessInt(int x)
        {
#line 21 "interface_generic_method_0002.spy"
            return x * 2;
        }

        public string ProcessStr(string s)
        {
#line 24 "interface_generic_method_0002.spy"
            return s + s;
        }

        public string GetName()
        {
#line 27 "interface_generic_method_0002.spy"
            return this.Name;
        }

        public MyProcessor(string n)
        {
#line 18 "interface_generic_method_0002.spy"
            this.Name = n;
        }
    }
}
