namespace Sharpy
{
    public sealed partial class List<T>
    {
        public void __IAdd__(List<T> other)
        {
            if (other is null)
            {
                throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
            }

            Extend(other);
        }
    }
}
