#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace AccessorParamModuleLevel
{
    public static partial class AccessorParamModuleLevelModule
    {
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int _Backing__field = default!;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static byte _Backing__state;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Backing__init()
        {
            _Backing__field = 0;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void _Backing__slow() => global::Sharpy.StaticInit.Read(ref __host, __Sweep, ref _Backing__state, "_backing", _Backing__init);
        [global::Sharpy.SharpyStaticStorage(global::Sharpy.StaticStorageKind.Guarded)]
        public static ref int _Backing
        {
            get
            {
                if (!__Swept.Done)
                    _Backing__slow();
                return ref _Backing__field;
            }
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static void _Backing__ensure()
        {
            if (!__Swept.Done)
                _Backing__slow();
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static int _Backing__set(int value)
        {
            if (__Swept.Done)
                _Backing__field = value;
            else
                global::Sharpy.StaticInit.Store(ref __host, __Sweep, ref _Backing__state, ref _Backing__field, value);
            return value;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static global::System.Runtime.CompilerServices.StrongBox<int>? _Backing__pick()
        {
            if (!__Swept.Done)
            {
                global::Sharpy.StaticInit.Load(ref __host, __Sweep);
                if (!global::Sharpy.StaticInit.IsReady(ref _Backing__state))
                    return new global::System.Runtime.CompilerServices.StrongBox<int>();
            }

            return null;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static ref int _Backing__out(out global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            spill = _Backing__pick();
            if (spill != null)
                return ref spill.Value!;
            return ref _Backing__field;
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        internal static R _Backing__commit<R>(R result, global::System.Runtime.CompilerServices.StrongBox<int>? spill)
        {
            if (spill != null)
                _Backing__set(spill.Value!);
            return result;
        }

        public static int Level
        {
            set
            {
#line (11, 5) - (11, 21) 16 "accessor_param_module_level.spy"
                _Backing__set(value + 1);
#line hidden
            }
        }

        public static int ReadLevel()
        {
#line (15, 5) - (15, 21) 12 "accessor_param_module_level.spy"
            return global::AccessorParamModuleLevel.AccessorParamModuleLevelModule._Backing;
#line hidden
        }

        public static void Main()
        {
            __Load();
#line (19, 5) - (19, 14) 12 "accessor_param_module_level.spy"
            Level = 6;
#line (20, 5) - (20, 24) 12 "accessor_param_module_level.spy"
            global::Sharpy.Builtins.Print(global::AccessorParamModuleLevel.AccessorParamModuleLevelModule.ReadLevel());
#line hidden
        }

        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static global::Sharpy.StaticInitHost __host;
        [global::System.Runtime.CompilerServices.CompilerGenerated]
        private static void __Sweep()
        {
            global::Sharpy.StaticInit.Run(ref _Backing__state, "_backing", _Backing__init);
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
}
#line default
