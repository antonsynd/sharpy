#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using global::Sharpy.Core;

namespace Sharpy.GenericDemo
{
    public static class Exports
    {
        public class Box<T>
        {
            public T Value = null;
            public void Set(T newValue)
            {
                this.Value = newValue;
            }

            public T Get()
            {
                return this.Value;
            }
        }

        public static T Identity<T>(T value)
        {
            return value;
        }

        public static string Combine<T, U>(T first, U second)
        {
            return $"{first} {second}";
        }

        public static T GetFirst<T>(global::Sharpy.Core.List<T> items)
        {
            return items[0];
        }

        public struct Point<T>
        {
            public T X = null;
            public T Y = null;
            public void SetCoords(T x, T y)
            {
                this.X = x;
                this.Y = y;
            }
        }

        public static void Main()
        {
            global::Sharpy.Core.Exports.Print("Generic code generation is working!");
        }
    }
}
