using System.Globalization;

namespace Sharpy
{
    public readonly partial struct Str
    {
        /// <summary>
        /// Returns the number of (Unicode) characters in the string.
        /// </summary>
        public int __Len__()
        {
            StringInfo stringInfo = new(_s);

            return stringInfo.LengthInTextElements;
        }
    }
}
