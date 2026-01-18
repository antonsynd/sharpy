#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.Source
{
    public static class Program
    {
        public interface IDisplayable
        {
            void Display();
        }

        public interface ICalculable
        {
            int Calculate();
        }

        public abstract class Shape : IDisplayable
        {
            public string Name;
            public abstract int Area();
            public virtual int Describe()
            {
                return 1;
            }

            public Shape(string name)
            {
                this.Name = name;
            }
        }

        public class Rectangle : Shape, ICalculable
        {
            public int Width;
            public int Height;
            public override int Area()
            {
                return this.Width * this.Height;
            }

            public override int Describe()
            {
                return 2;
            }

            public void Display()
            {
                global::Sharpy.Core.Exports.Print(this.Name);
                global::Sharpy.Core.Exports.Print(this.Area());
            }

            public int Calculate()
            {
                return this.Width + this.Height;
            }

            public void Scale(int factor)
            {
                this.Width = this.Width * factor;
                this.Height = this.Height * factor;
            }

            public Rectangle(int w, int h) : base("Rectangle")
            {
                this.Width = w;
                this.Height = h;
            }
        }

        public class Square : Rectangle
        {
            public override int Describe()
            {
                int baseVal = base.Describe();
                return baseVal + 1;
            }

            public Square(int side) : base(side, side)
            {
                this.Name = "Square";
            }
        }

        public static Rectangle Rect = new Rectangle(5, 3);
        public static Square Sq = new Square(4);
        public static int Count = 0;
        public static void Main()
        {
            Rect.Display();
            global::Sharpy.Core.Exports.Print(Rect.Calculate());
            global::Sharpy.Core.Exports.Print(Rect.Describe());
            Sq.Display();
            global::Sharpy.Core.Exports.Print(Sq.Calculate());
            global::Sharpy.Core.Exports.Print(Sq.Describe());
            Sq.Scale(2);
            global::Sharpy.Core.Exports.Print(Sq.Area());
            foreach (var __loopVar_0 in global::Sharpy.Core.Exports.Range(3))
            {
                var i = __loopVar_0;
                if (Rect.Width > i)
                {
                    Count = Count + 1;
                }
            }

            global::Sharpy.Core.Exports.Print(Count);
        }
    }
}