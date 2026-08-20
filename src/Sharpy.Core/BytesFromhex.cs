using System;

namespace Sharpy
{
    /// <summary>
    /// Static helper for bytes.fromhex(). The IntParse design (#1347): a static class
    /// alongside the struct so the emitter can route bytes.fromhex to a method group
    /// that is not a member of the struct (which would trigger CS0119 when the
    /// identifier `bytes` is resolved as the Builtins.Bytes method group).
    /// </summary>
    public static class BytesFromhex
    {
        /// <summary>Create a Bytes instance from a hex string.</summary>
        public static Bytes Fromhex(string hexString)
        {
            if (hexString == null)
            {
                throw new ValueError("non-hexadecimal number found in fromhex() arg");
            }

#pragma warning disable CA1307
            var clean = hexString.Replace(" ", "");
#pragma warning restore CA1307

            if (clean.Length % 2 != 0)
            {
                throw new ValueError("non-hexadecimal number found in fromhex() arg at position " + clean.Length);
            }

            var data = new byte[clean.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                var hexByte = clean.Substring(i * 2, 2);
                try
                {
                    data[i] = Convert.ToByte(hexByte, 16);
                }
                catch (FormatException)
                {
                    throw new ValueError("non-hexadecimal number found in fromhex() arg at position " + (i * 2));
                }
            }

            return Bytes.Wrap(data);
        }
    }
}
