namespace Sharpy.Core
{
    public sealed partial class List<T>
    {
        /// <inheritdoc/>
        public void __IMul__(int i)
        {
            if (i <= 0)
            {
                Clear();

                return;
            }

            var originalLength = _list.Count;

            --i;

            for (; i > 0; --i)
            {
                for (uint j = 0; j < originalLength; ++j)
                {
                    _list.Add(_list[(int)j]);
                }
            }
        }
    }
}
