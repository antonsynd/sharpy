using System.Collections.Generic;

// NOTE: Inside the `Sharpy` namespace, an unqualified `List<T>` binds to `Sharpy.List<T>`
// (current-namespace precedence). `_items`/`Seq` intentionally use the Sharpy collection;
// internal helper lists are fully qualified to System.Collections.Generic.List.
namespace Sharpy
{
    /// <summary>
    /// An ordered, comment-aware sequence used for YAML roundtrip operations, analogous to
    /// ruamel.yaml's <c>CommentedSeq</c>. Wraps a <see cref="List{T}"/> internally
    /// (composition — <see cref="List{T}"/> is sealed and cannot be inherited) and tracks
    /// the comments associated with each item by index.
    /// </summary>
    [SharpyModuleType("yaml")]
    public class CommentedSeq
    {
        private readonly List<object?> _items;
        private readonly Dictionary<int, CommentInfo> _comments;

        /// <summary>Create an empty commented sequence.</summary>
        public CommentedSeq()
        {
            _items = new List<object?>();
            _comments = new Dictionary<int, CommentInfo>();
        }

        /// <summary>
        /// The underlying <see cref="List{T}"/> backing this sequence, exposed for
        /// serialization access.
        /// </summary>
        public List<object?> Seq => _items;

        /// <summary>The number of items in this sequence.</summary>
        public int Count => ((IReadOnlyList<object?>)_items).Count;

        /// <summary>
        /// The comments associated with this sequence's items, keyed by item index.
        /// </summary>
        public IReadOnlyDictionary<int, CommentInfo> Comments => _comments;

        /// <summary>Gets or sets the item at the specified (Python-style) index.</summary>
        public object? this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        /// <summary>Appends an item to the end of the sequence.</summary>
        public void Add(object? item) => _items.Add(item);

        /// <summary>Inserts an item at the specified index, shifting comments after it.</summary>
        public void Insert(int index, object? item)
        {
            _items.Insert(index, item);
            ShiftComments(index, 1);
        }

        /// <summary>Removes the item at the specified index, shifting comments after it.</summary>
        public void RemoveAt(int index)
        {
            _items.DeleteAt(index);
            _comments.Remove(index);
            ShiftComments(index + 1, -1);
        }

        /// <summary>
        /// Associates the given comment information with an item index, replacing any
        /// existing entry.
        /// </summary>
        public void SetComment(int index, CommentInfo comment) => _comments[index] = comment;

        /// <summary>
        /// Gets the comment associated with an item index, or returns an existing/new
        /// mutable <see cref="CommentInfo"/> so callers can attach comments incrementally.
        /// </summary>
        public CommentInfo GetOrAddComment(int index)
        {
            if (!_comments.TryGetValue(index, out var comment))
            {
                comment = new CommentInfo();
                _comments[index] = comment;
            }
            return comment;
        }

        /// <summary>
        /// Gets the comment associated with an item index, or <c>null</c> if none exists.
        /// </summary>
        public CommentInfo? GetComment(int index) =>
            _comments.TryGetValue(index, out var comment) ? comment : null;

        /// <summary>
        /// Re-indexes comment entries at or after <paramref name="from"/> by
        /// <paramref name="delta"/> after an insertion or removal.
        /// </summary>
        private void ShiftComments(int from, int delta)
        {
            if (_comments.Count == 0)
            {
                return;
            }

            var affected = new System.Collections.Generic.List<int>();
            foreach (var index in _comments.Keys)
            {
                if (index >= from)
                {
                    affected.Add(index);
                }
            }

            // Reassign in an order that avoids transient key collisions.
            affected.Sort();
            if (delta > 0)
            {
                affected.Reverse();
            }

            foreach (var index in affected)
            {
                var comment = _comments[index];
                _comments.Remove(index);
                _comments[index + delta] = comment;
            }
        }
    }
}
