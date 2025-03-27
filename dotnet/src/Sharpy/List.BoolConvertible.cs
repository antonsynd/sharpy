using System.Text;

namespace Sharpy
{
    public sealed partial class List<T>
    {
        public override bool __Bool__()
        {
            return _list.Count > 0;
        }
    }
}
