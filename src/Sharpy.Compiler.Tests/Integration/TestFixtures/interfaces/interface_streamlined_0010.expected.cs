#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.InterfaceStreamlined0010
{
    public static class Program
    {
        public static void Main()
        {
#line 28 "interface_streamlined_0010.spy"
            var w = new Widget("Button", 42);
#line 29 "interface_streamlined_0010.spy"
            global::Sharpy.Core.Exports.Print(w.Draw());
#line 30 "interface_streamlined_0010.spy"
            global::Sharpy.Core.Exports.Print(w.GetName());
#line 31 "interface_streamlined_0010.spy"
            global::Sharpy.Core.Exports.Print(w.GetSize());
        }
    }

    public interface IDrawable
    {
        string Draw();
        string GetName();
    }

    public interface ISizable
    {
        int GetSize();
    }

    public class Widget : IDrawable, ISizable
    {
        public string Name;
        public int Size;
        public string Draw()
        {
#line 19 "interface_streamlined_0010.spy"
            return "Drawing " + this.Name;
        }

        public string GetName()
        {
#line 22 "interface_streamlined_0010.spy"
            return this.Name;
        }

        public int GetSize()
        {
#line 25 "interface_streamlined_0010.spy"
            return this.Size;
        }

        public Widget(string name, int size)
        {
#line 15 "interface_streamlined_0010.spy"
            this.Name = name;
#line 16 "interface_streamlined_0010.spy"
            this.Size = size;
        }
    }
}
