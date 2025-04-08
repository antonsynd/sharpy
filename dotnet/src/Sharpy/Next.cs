namespace Sharpy
{
    public static partial class __Exports
    {
        public static T Next<T>(Iterator<T> iterator)
        {
            if (iterator is null)
            {
                throw new TypeError("Next() iterator argument cannot be None");
            }

            return iterator.__Next__();
        }
    }
}
