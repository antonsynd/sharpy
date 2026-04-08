using System;

namespace Sharpy
{
    /// <summary>
    /// Slicing operations for Bytes.
    /// </summary>
    public readonly partial struct Bytes
    {
        /// <summary>
        /// Returns a slice of the bytes using Python-style slice semantics.
        /// </summary>
        public Bytes Slice(int? start, int? stop, int? step)
        {
            var s = new Slice(start, stop, step);

            if (s.step == 0)
            {
                throw new ValueError("slice step cannot be zero");
            }

            if (s.step < 0)
            {
                return SliceNegativeStep(s);
            }

            return SlicePositiveStep(s);
        }

        private Bytes SlicePositiveStep(Slice s)
        {
            int nStart = s.start;
            int nEnd = s.end;

            if (nStart < 0)
                nStart = _data.Length + nStart;
            if (nStart < 0)
                nStart = 0;
            if (nStart > _data.Length)
                nStart = _data.Length;

            if (nEnd < 0)
                nEnd = _data.Length + nEnd;
            if (nEnd < 0)
                nEnd = 0;
            if (nEnd > _data.Length)
                nEnd = _data.Length;

            if (nStart >= nEnd)
            {
                return Bytes.Wrap(Array.Empty<byte>());
            }

            if (s.step == 1)
            {
                var result = new byte[nEnd - nStart];
                Array.Copy(_data, nStart, result, 0, result.Length);
                return Bytes.Wrap(result);
            }

            int count = 0;
            for (int i = nStart; i < nEnd; i += s.step)
            {
                count++;
            }

            var data = new byte[count];
            int idx = 0;
            for (int i = nStart; i < nEnd; i += s.step)
            {
                data[idx++] = _data[i];
            }

            return Bytes.Wrap(data);
        }

        private Bytes SliceNegativeStep(Slice s)
        {
            int nStart = s.start;
            int nEnd = s.end;

            if (nStart < 0)
                nStart = _data.Length + nStart;
            if (nStart >= _data.Length)
                nStart = _data.Length - 1;
            if (nStart < -1)
                nStart = -1;

            if (nEnd < -_data.Length)
                nEnd = -1;
            else if (nEnd < 0)
                nEnd = _data.Length + nEnd;
            if (nEnd >= _data.Length)
                nEnd = _data.Length - 1;

            if (nStart <= nEnd)
            {
                return Bytes.Wrap(Array.Empty<byte>());
            }

            int count = 0;
            for (int i = nStart; i > nEnd; i += s.step)
            {
                count++;
            }

            var data = new byte[count];
            int idx = 0;
            for (int i = nStart; i > nEnd; i += s.step)
            {
                data[idx++] = _data[i];
            }

            return Bytes.Wrap(data);
        }
    }
}
