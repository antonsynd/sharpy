namespace Sharpy
{
    public interface ItemsView<K, V> : Set<(K, V)>, MappingView
    {
    }
}
