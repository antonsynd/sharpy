namespace Sharpy
{
    internal static class Index
    {
        internal static uint Normalize(int i, uint max, bool forSlice, bool forInsertion)
        {
            if (forSlice || forInsertion)
            {
                if (i < 0)
                {
                    i = (int)max + i;
                }

                return (uint)Math.Clamp(i, 0, max);
            }

            if (i >= max || i < -max)
            {
                throw new IndexError($"list index {i} out of range");
            }

            if (i < 0)
            {
                return (uint)(max + i);
            }

            return (uint)i;
        }
    }
}
