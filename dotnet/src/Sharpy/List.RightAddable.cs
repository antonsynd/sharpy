namespace Sharpy
{
    public sealed partial class List<T>
    {
        public List<T> __RAdd__(List<T> other)
        {
            if (other is null)
            {
                throw new TypeError($"can only concatenate List<${typeof(T).Name}> (not \"NoneType\") to List<${typeof(T).Name}");
            }

            var res = other.Copy();
            res.Extend(this);

            return res;
        }
    }
}
