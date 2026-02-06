namespace Sharpy
{
    using System.Globalization;

    /// <summary>
    /// Iterator for Str that yields individual characters.
    /// </summary>
    internal sealed class StrIterator : Iterator<Str>
    {
        private readonly string _str;
        private readonly StringInfo _stringInfo;
        private int _position;

        internal StrIterator(string str)
        {
            _str = str;
            _stringInfo = new StringInfo(str);
            _position = 0;
        }

        public override Str __Next__()
        {
            if (_position >= _stringInfo.LengthInTextElements)
            {
                throw new StopIteration();
            }

            var element = _stringInfo.SubstringByTextElements(_position, 1);
            _position++;

            return new Str(element);
        }
    }

    public readonly partial struct Str
    {
        /// <summary>
        /// Implements the __iter__ dunder method.
        /// Returns an iterator over the characters in the string.
        /// </summary>
        public Iterator<Str> __Iter__()
        {
            return new StrIterator(_s);
        }
    }
}
