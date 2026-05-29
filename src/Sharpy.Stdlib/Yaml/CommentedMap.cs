using System.Collections.Generic;

// NOTE: Inside the `Sharpy` namespace, an unqualified `List<T>` binds to `Sharpy.List<T>`
// (current-namespace precedence over the `using` above). For BCL list semantics
// (Count property, capacity ctor, Sort/Reverse) we fully qualify System.Collections.Generic.List.
// `Dict` and `Dictionary` are unambiguous (no `Sharpy.Dictionary` exists).
namespace Sharpy
{
    /// <summary>
    /// An ordered, comment-aware mapping used for YAML roundtrip operations, analogous to
    /// ruamel.yaml's <c>CommentedMap</c>. Wraps a <see cref="Dict{K, V}"/> internally
    /// (composition — <see cref="Dict{K, V}"/> is sealed and cannot be inherited) and
    /// tracks insertion order plus the comments associated with each key.
    /// </summary>
    [SharpyModuleType("yaml")]
    public class CommentedMap
    {
        private readonly Dict<string, object?> _items;
        private readonly System.Collections.Generic.List<string> _order;
        private readonly Dictionary<string, CommentInfo> _comments;

        /// <summary>Create an empty commented mapping.</summary>
        public CommentedMap()
        {
            _items = new Dict<string, object?>();
            _order = new System.Collections.Generic.List<string>();
            _comments = new Dictionary<string, CommentInfo>();
        }

        /// <summary>
        /// The underlying <see cref="Dict{K, V}"/> backing this mapping, exposed for
        /// serialization access.
        /// </summary>
        public Dict<string, object?> Map => _items;

        /// <summary>The keys of this mapping, in insertion order.</summary>
        public IReadOnlyList<string> Keys => _order;

        /// <summary>The values of this mapping, in key insertion order.</summary>
        public IReadOnlyList<object?> Values
        {
            get
            {
                var values = new System.Collections.Generic.List<object?>(_order.Count);
                foreach (var key in _order)
                {
                    values.Add(_items[key]);
                }
                return values;
            }
        }

        /// <summary>The number of key/value pairs in this mapping.</summary>
        public int Count => _order.Count;

        /// <summary>
        /// The comments associated with this mapping's keys, keyed by key name.
        /// </summary>
        public IReadOnlyDictionary<string, CommentInfo> Comments => _comments;

        /// <summary>
        /// Gets or sets the value associated with the specified key. Setting a new key
        /// appends it to the insertion order.
        /// </summary>
        public object? this[string key]
        {
            get => _items[key];
            set
            {
                if (!_items.ContainsKey(key))
                {
                    _order.Add(key);
                }
                _items[key] = value;
            }
        }

        /// <summary>Determines whether the mapping contains the specified key.</summary>
        public bool ContainsKey(string key) => _items.ContainsKey(key);

        /// <summary>
        /// Adds a key/value pair to the mapping, preserving insertion order.
        /// </summary>
        public void Add(string key, object? value)
        {
            if (!_items.ContainsKey(key))
            {
                _order.Add(key);
            }
            _items[key] = value;
        }

        /// <summary>
        /// Removes the specified key (and any associated comment) from the mapping.
        /// </summary>
        /// <returns><c>true</c> if the key was present and removed; otherwise <c>false</c>.</returns>
        public bool Remove(string key)
        {
            if (!_items.ContainsKey(key))
            {
                return false;
            }
            _items.Remove(key);
            _order.Remove(key);
            _comments.Remove(key);
            return true;
        }

        /// <summary>
        /// Gets the value associated with the specified key, if present.
        /// </summary>
        public bool TryGetValue(string key, out object? value) =>
            _items.TryGetValue(key, out value);

        /// <summary>
        /// Associates the given comment information with a key, replacing any existing entry.
        /// </summary>
        public void SetComment(string key, CommentInfo comment) => _comments[key] = comment;

        /// <summary>
        /// Gets the comment associated with a key, or returns an existing/new mutable
        /// <see cref="CommentInfo"/> so callers can attach comments incrementally.
        /// </summary>
        public CommentInfo GetOrAddComment(string key)
        {
            if (!_comments.TryGetValue(key, out var comment))
            {
                comment = new CommentInfo();
                _comments[key] = comment;
            }
            return comment;
        }

        /// <summary>
        /// Gets the comment associated with a key, or <c>null</c> if none exists.
        /// </summary>
        public CommentInfo? GetComment(string key) =>
            _comments.TryGetValue(key, out var comment) ? comment : null;
    }
}
