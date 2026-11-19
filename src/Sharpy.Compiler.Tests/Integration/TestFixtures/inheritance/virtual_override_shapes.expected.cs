// Snapshot: Virtual method override polymorphism
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace VirtualOverrideShapes
{
    public static partial class VirtualOverrideShapesModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::VirtualOverrideShapes.Rectangle Rect__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Rect__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Rect__init()
        {
            Rect__field = new global::VirtualOverrideShapes.Rectangle(5.0d, 3.0d);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Rect__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Rect__state, "rect", Rect__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::VirtualOverrideShapes.Rectangle Rect
        {
            get
            {
                if (!__Swept.Done)
                    Rect__slow();
                return ref Rect__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Rect__ensure()
        {
            if (!__Swept.Done)
                Rect__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Rect__storeSlow(global::VirtualOverrideShapes.Rectangle value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Rect__state, ref Rect__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::VirtualOverrideShapes.Rectangle Rect__set(global::VirtualOverrideShapes.Rectangle value)
        {
            if (__Swept.Done)
                Rect__field = value;
            else
                Rect__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Rectangle>? Rect__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Rect__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Rectangle>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::VirtualOverrideShapes.Rectangle Rect__out(out global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Rectangle>? spill)
        {
            spill = Rect__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Rect__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Rect__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Rectangle>? spill)
        {
            if (spill != null)
                Rect__set(spill.Value!);
            return result;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::VirtualOverrideShapes.Circle Circ__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte Circ__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Circ__init()
        {
            Circ__field = new global::VirtualOverrideShapes.Circle(4.0d);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Circ__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref Circ__state, "circ", Circ__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref global::VirtualOverrideShapes.Circle Circ
        {
            get
            {
                if (!__Swept.Done)
                    Circ__slow();
                return ref Circ__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void Circ__ensure()
        {
            if (!__Swept.Done)
                Circ__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void Circ__storeSlow(global::VirtualOverrideShapes.Circle value) => global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref Circ__state, ref Circ__field, value);
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::VirtualOverrideShapes.Circle Circ__set(global::VirtualOverrideShapes.Circle value)
        {
            if (__Swept.Done)
                Circ__field = value;
            else
                Circ__storeSlow(value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Circle>? Circ__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref Circ__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Circle>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref global::VirtualOverrideShapes.Circle Circ__out(out global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Circle>? spill)
        {
            spill = Circ__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref Circ__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R Circ__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<global::VirtualOverrideShapes.Circle>? spill)
        {
            if (spill != null)
                Circ__set(spill.Value!);
            return result;
        }

        public static void Main()
        {
            __Load();
#line (59, 5) - (59, 20) 12 "virtual_override_shapes.spy"
            global::VirtualOverrideShapes.VirtualOverrideShapesModule.Rect.Describe();
#line (61, 5) - (61, 20) 12 "virtual_override_shapes.spy"
            global::VirtualOverrideShapes.VirtualOverrideShapesModule.Circ.Describe();
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref Rect__state, "rect", Rect__init);
            global::Sharpy.StaticInit.Run(ref Circ__state, "circ", Circ__init);
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static class __Swept
        {
            internal static readonly bool Done;
            static __Swept()
            {
                Done = global::Sharpy.StaticInit.TryLoad(ref __host, __Sweep);
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        [global::Sharpy.SharpyLoadEntry(true)]
        public static void __Load()
        {
            if (!__Swept.Done)
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Shape")]
    public abstract class Shape
    {
        public string Name;
        public virtual double Area()
#line 11 "virtual_override_shapes.spy"
        {
#line (12, 9) - (12, 20) 12 "virtual_override_shapes.spy"
            return 0.0d;
#line hidden
        }

        public virtual double Perimeter()
#line 15 "virtual_override_shapes.spy"
        {
#line (16, 9) - (16, 20) 12 "virtual_override_shapes.spy"
            return 0.0d;
#line hidden
        }

        public void Describe()
#line 18 "virtual_override_shapes.spy"
        {
#line (19, 9) - (19, 25) 12 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Name);
#line (20, 9) - (20, 27) 12 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Area());
#line (21, 9) - (21, 32) 12 "virtual_override_shapes.spy"
            global::Sharpy.Builtins.Print(this.Perimeter());
#line hidden
        }

        public Shape(string name)
#line 7 "virtual_override_shapes.spy"
        {
#line (8, 9) - (8, 25) 12 "virtual_override_shapes.spy"
            this.Name = name;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Rectangle")]
    public class Rectangle : global::VirtualOverrideShapes.Shape
    {
        public double Width;
        public double Height;
        public override double Area()
#line 33 "virtual_override_shapes.spy"
        {
#line (34, 9) - (34, 41) 12 "virtual_override_shapes.spy"
            return this.Width * this.Height;
#line hidden
        }

        public override double Perimeter()
#line 37 "virtual_override_shapes.spy"
        {
#line (38, 9) - (38, 49) 12 "virtual_override_shapes.spy"
            return 2.0d * (this.Width + this.Height);
#line hidden
        }

        public Rectangle(double width, double height) : base("Rectangle")
#line 27 "virtual_override_shapes.spy"
        {
#line (29, 9) - (29, 27) 12 "virtual_override_shapes.spy"
            this.Width = width;
#line (30, 9) - (30, 29) 12 "virtual_override_shapes.spy"
            this.Height = height;
#line hidden
        }
    }

    [global::Sharpy.SharpyModuleType("__main__", "Circle")]
    public class Circle : global::VirtualOverrideShapes.Shape
    {
        public double Radius;
        public override double Area()
#line 48 "virtual_override_shapes.spy"
        {
#line (49, 9) - (49, 49) 12 "virtual_override_shapes.spy"
            return 3.14d * this.Radius * this.Radius;
#line hidden
        }

        public override double Perimeter()
#line 52 "virtual_override_shapes.spy"
        {
#line (53, 9) - (53, 41) 12 "virtual_override_shapes.spy"
            return 2.0d * 3.14d * this.Radius;
#line hidden
        }

        public Circle(double radius) : base("Circle")
#line 43 "virtual_override_shapes.spy"
        {
#line (45, 9) - (45, 29) 12 "virtual_override_shapes.spy"
            this.Radius = radius;
#line hidden
        }
    }
}
#line default
