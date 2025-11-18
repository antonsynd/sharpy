using Sharpy.Core;

namespace Sharpy.Collections.Interfaces;

/// <summary>
/// Interface for classes that provide the <see cref="__Iter__()"/> method.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IEnumerable{T}"/> to bridge Sharpy's iterator
/// protocol with .NET's enumeration pattern. Types implementing this interface can
/// be used in both Sharpy for-loops and C# foreach loops.
/// </para>
/// <para>
/// <strong>Implementation Guidelines:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// The <see cref="IEnumerable{T}.GetEnumerator"/> method should delegate to
/// <see cref="__Iter__()"/> to ensure consistent iteration behavior and avoid
/// maintaining duplicate iteration logic.
/// </description>
/// </item>
/// <item>
/// <description>
/// Each call to <see cref="__Iter__()"/> or <see cref="IEnumerable{T}.GetEnumerator"/>
/// should return a fresh iterator that starts from the beginning of the collection.
/// </description>
/// </item>
/// <item>
/// <description>
/// For collection types (like List, Set, Dict), this typically means creating a new
/// iterator object. For iterator types themselves, this typically means returning
/// <c>this</c> since iterators are their own iterators.
/// </description>
/// </item>
/// <item>
/// <description>
/// Iterators should be single-pass and should not support reset operations. Once
/// exhausted, they should consistently raise <see cref="StopIteration"/>.
/// </description>
/// </item>
/// </list>
/// <para>
/// <strong>Interoperability with C#:</strong>
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// Any C# <see cref="IEnumerable{T}"/> can be converted to a Sharpy iterator using
/// the <c>Iter()</c> builtin or the <c>ToIterator()</c> extension method.
/// </description>
/// </item>
/// <item>
/// <description>
/// Sharpy iterables can be used with C# LINQ operators through the extension methods
/// provided in <see cref="IterableLinqExtensions"/>.
/// </description>
/// </item>
/// </list>
/// </remarks>
public interface IIterable<T> : IEnumerable<T>
{
    /// <summary>
    /// Return an <see cref="Iterator{T}"/> object. The object is required to
    /// support the iterator protocol described below.
    /// </summary>
    /// <returns>
    /// A fresh iterator that starts from the beginning of the collection.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If a container supports different types of iteration, additional
    /// methods can be provided to specifically request iterators for
    /// those iteration types. (An example of an object supporting multiple
    /// forms of iteration would be a tree structure which supports both
    /// breadth-first and depth-first traversal).
    /// </para>
    /// <para>
    /// The returned iterator should implement the iterator protocol:
    /// calling <c>__Next__()</c> should return the next item from the collection,
    /// and raise <see cref="StopIteration"/> when no more items are available.
    /// </para>
    /// </remarks>
    Iterator<T> __Iter__();
}
