// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace System.Linq
{
    public static partial class Enumerable
    {
        public static int Count<TSource>(this IEnumerable<TSource> source)
        {
            if (source is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            if (source is IReadOnlyCollection<TSource> collectionoft)
            {
                return collectionoft.Count;
            }

            if (!IsSizeOptimized && source is Iterator<TSource> iterator)
            {
                return iterator.GetCount(onlyIfCheap: false);
            }

            if (source is ICollection collection)
            {
                return collection.Count;
            }

            int count = 0;
            using IEnumerator<TSource> e = source.GetEnumerator();
            checked
            {
                while (e.MoveNext())
                {
                    count++;
                }
            }

            return count;
        }

        public static int Count<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            if (predicate is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.predicate);
            }

            int count = 0;
            if (source.TryGetSpan(out ReadOnlySpan<TSource> span))
            {
                foreach (TSource element in span)
                {
                    if (predicate(element))
                    {
                        count++;
                    }
                }
            }
            else
            {
                foreach (TSource element in source)
                {
                    if (predicate(element))
                    {
                        checked { count++; }
                    }
                }
            }

            return count;
        }

        /// <summary>
        ///   Attempts to determine the number of elements in a sequence without forcing an enumeration.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of <paramref name="source" />.</typeparam>
        /// <param name="source">A sequence that contains elements to be counted.</param>
        /// <param name="count">
        ///     When this method returns, contains the count of <paramref name="source" /> if successful,
        ///     or zero if the method failed to determine the count.</param>
        /// <returns>
        ///   <see langword="true" /> if the count of <paramref name="source"/> can be determined without enumeration;
        ///   otherwise, <see langword="false" />.
        /// </returns>
        /// <remarks>
        ///   The method performs a series of type tests, identifying common subtypes whose
        ///   count can be determined without enumerating; this includes <see cref="ICollection{T}"/>,
        ///   <see cref="ICollection"/> as well as internal types used in the LINQ implementation.
        ///
        ///   The method is typically a constant-time operation, but ultimately this depends on the complexity
        ///   characteristics of the underlying collection implementation.
        /// </remarks>
        public static bool TryGetNonEnumeratedCount<TSource>(this IEnumerable<TSource> source, out int count)
        {
            if (source is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            if (source is IReadOnlyCollection<TSource> collectionoft)
            {
                count = collectionoft.Count;
                return true;
            }

            if (!IsSizeOptimized && source is Iterator<TSource> iterator)
            {
                int c = iterator.GetCount(onlyIfCheap: true);
                if (c >= 0)
                {
                    count = c;
                    return true;
                }
            }

            if (source is ICollection collection)
            {
                count = collection.Count;
                return true;
            }

            count = 0;
            return false;
        }

        public static long LongCount<TSource>(this IEnumerable<TSource> source)
        {
            if (source is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            // TryGetSpan isn't used here because if it's expected that there are more than int.MaxValue elements,
            // the source can't possibly be something from which we can extract a span.

            long count = 0;
            using IEnumerator<TSource> e = source.GetEnumerator();
            while (e.MoveNext())
            {
                checked { count++; }
            }

            return count;
        }

        public static long LongCount<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            if (source is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source);
            }

            if (predicate is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.predicate);
            }

            // TryGetSpan isn't used here because if it's expected that there are more than int.MaxValue elements,
            // the source can't possibly be something from which we can extract a span.

            long count = 0;
            foreach (TSource element in source)
            {
                if (predicate(element))
                {
                    checked { count++; }
                }
            }

            return count;
        }
    }
}
