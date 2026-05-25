// Generated from src/Sharpy.Stdlib/spy/hashlib_module.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/hashlib_module.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    public static partial class HashlibModule
    {
        public static global::Sharpy.HashObject Md5(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("md5", 16);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        public static global::Sharpy.HashObject Sha1(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha1", 20);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        public static global::Sharpy.HashObject Sha256(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha256", 32);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        public static global::Sharpy.HashObject Sha384(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha384", 48);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }

        public static global::Sharpy.HashObject Sha512(string data = "")
        {
            global::Sharpy.HashObject obj = new global::Sharpy.HashObject("sha512", 64);
            if (data.Length > 0)
            {
                obj.Update(data);
            }

            return obj;
        }
    }
}
