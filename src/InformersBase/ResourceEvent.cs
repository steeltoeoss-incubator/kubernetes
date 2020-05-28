using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Steeltoe.Informers.InformersBase
{
    public struct ResourceEvent
    {
        public static ResourceEvent<TKey, TResource> Create<TKey, TResource>(EventTypeFlags eventFlags, TKey key, TResource value = default, TResource oldValue = default) =>
            new ResourceEvent<TKey, TResource>(eventFlags, key, value, oldValue);
    }
    /// <summary>
    /// </summary>
    /// <typeparam name="TResource"></typeparam>
    [DebuggerStepThrough]
    public struct ResourceEvent<TKey, TResource>
    {
        

        public static ResourceEvent<TKey, TResource> EmptyReset { get; } = new ResourceEvent<TKey, TResource>(EventTypeFlags.ResetEmpty);

        public ResourceEvent(EventTypeFlags eventFlags, TKey key = default, TResource value = default, TResource oldValue = default)
        {
            if (eventFlags.HasFlag(EventTypeFlags.ResetEmpty) || eventFlags.HasFlag(EventTypeFlags.ResetEmpty))
            {
                eventFlags |= EventTypeFlags.ResetStart | EventTypeFlags.ResetEnd;
            }

            if (eventFlags.HasFlag(EventTypeFlags.ResetEnd) || eventFlags.HasFlag(EventTypeFlags.ResetStart))
            {
                eventFlags |= EventTypeFlags.Reset;
            }

            if (eventFlags.HasFlag(EventTypeFlags.Reset) || eventFlags.HasFlag(EventTypeFlags.Sync))
            {
                eventFlags |= EventTypeFlags.Current;
            }

            Key = key;
            Value = value;
            OldValue = oldValue;
            EventFlags = eventFlags;
        }

        public EventTypeFlags EventFlags { get; }
        public TKey Key { get; set; }
        public TResource Value { get; }
        public TResource OldValue { get; }
        public static ResourceEvent<TKey, TResource> ResetEmpty { get; } = new ResourceEvent<TKey, TResource>(EventTypeFlags.ResetEmpty, default, default);

        public override string ToString()
        {
            var includePrefix = Value != null && OldValue != null;

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.Append("   ");
            sb.Append(EventFlags);
            sb.Append($": [Key={Key} ");
            
            if (Value != null)
            {
                if (includePrefix)
                {
                    sb.Append(nameof(Value));
                    sb.Append("{ ");
                }

                sb.Append(Value);
                if (includePrefix)
                {
                    sb.Append("} ");
                }
            }

            if (OldValue != null)
            {
                if (includePrefix)
                {
                    sb.Append(nameof(OldValue));
                    sb.Append("{ ");
                }

                sb.Append(OldValue);
                if (includePrefix)
                {
                    sb.Append("} ");
                }
            }

            sb.Append("]");
            return sb.ToString();
        }
    }

    public static class ResourceEventExtensions
    {
        
        public static ResourceEvent<TKey, TResource> With<TKey, TResource>(this ResourceEvent<TKey, TResource> obj, EventTypeFlags typeFlags = default, TKey key = default, TResource value = default, TResource oldValue = default)
        {
            if (EqualityComparer<EventTypeFlags>.Default.Equals(typeFlags, default))
                typeFlags = obj.EventFlags;
            if (EqualityComparer<TKey>.Default.Equals(key, default))
                key = obj.Key;
            if (EqualityComparer<TResource>.Default.Equals(value, default))
                value = obj.Value;
            if (EqualityComparer<TResource>.Default.Equals(oldValue, default))
                oldValue = obj.OldValue;
            
            return new ResourceEvent<TKey, TResource>(typeFlags, key, value, oldValue);
        }

        

        public static ResourceEvent<TKey, TResource> ToResourceEvent<TKey, TResource>(this TResource obj, EventTypeFlags typeFlags = default, TKey key = default, TResource oldValue = default)
        {
            if (typeFlags.HasFlag(EventTypeFlags.Delete) && EqualityComparer<TResource>.Default.Equals(oldValue, default))
            {
                oldValue = obj;
            }
            return new ResourceEvent<TKey, TResource>(typeFlags, key, obj, oldValue);
        }

        public static IEnumerable<ResourceEvent<TKey, TResource>> ToReset<TKey, TResource>(this IEnumerable<ResourceEvent<TKey, TResource>> source, bool emitEmpty = false)
        {
            return ToReset(source, x => x.Key,  x => x.Value, emitEmpty);
        }

        /// <summary>
        ///     Converts a list of objects to a resource reset list event block. Every item is of type <see cref="EventTypeFlags.Reset" />,
        ///     with first and last elements also having <see cref="EventTypeFlags.ResetStart" /> and <see cref="EventTypeFlags.ResetEnd" />
        ///     set respectively. If <paramref name="source" /> is empty and <paramref name="emitEmpty" /> is set,
        /// </summary>
        /// <param name="source">The source enumerable</param>
        /// <param name="emitEmpty">
        ///     If <see langword="true" /> the resulting <see cref="IEnumerable{T}" /> will contain a single
        ///     <see cref="ResourceEvent{TResource}" /> with no object value and <see cref="EventTypeFlags.ResetEmpty" /> flag set
        /// </param>
        /// <typeparam name="TResource">The type of resource</typeparam>
        /// <returns>The resulting enumerable of reset events</returns>

        public static IEnumerable<ResourceEvent<TKey, TResource>> ToReset<TKey, TResource>(this IEnumerable<TResource> source, Func<TResource, TKey> keySelector, bool emitEmpty = false)
        {
            return ToReset(source, keySelector, x => x, emitEmpty);
        }
        public static IEnumerable<ResourceEvent<TKey, TResource>> ToReset<TKey, TResource>(this IDictionary<TKey, TResource> source,  bool emitEmpty = false)
        {
            return ToReset(source, pair => pair.Key, x => x.Value, false);
        }

        public static IEnumerable<ResourceEvent<TKey, TResource>> ToReset<TSource, TKey, TResource>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TResource> valueSelector,
            bool emitEmpty = false)
        {
            var enumerator = ToReset(source.AsAsyncEnumerable(), keySelector, valueSelector, emitEmpty).GetAsyncEnumerator();
            do
            {
                if (!enumerator.MoveNextAsync().Result)
                    yield break;
                yield return enumerator.Current;
            } while (true);
        }
        public static async IAsyncEnumerable<ResourceEvent<TKey, TResource>> ToReset<TSource, TKey, TResource>(this IAsyncEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TResource> valueSelector,
            bool emitEmpty = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var i = 0;
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            
            if (!await enumerator.MoveNextAsync())
            {
                if (emitEmpty)
                {
                    yield return new ResourceEvent<TKey, TResource>(EventTypeFlags.ResetEmpty, default);
                }
                yield break;
            }

            var current = enumerator.Current;
            var value = valueSelector(current);
            var key = keySelector(current);
            while (await enumerator.MoveNextAsync())
            {
                if (i == 0)
                {
                    yield return value.ToResourceEvent(EventTypeFlags.ResetStart, key);
                }
                else
                {
                    yield return value.ToResourceEvent(EventTypeFlags.Reset, key);
                }
                current = enumerator.Current;
                value = valueSelector(current);
                key = keySelector(current);
                i++;
            }

            if (i == 0)
            {
                yield return value.ToResourceEvent(EventTypeFlags.ResetStart | EventTypeFlags.ResetEnd, key);
            }
            else
            {
                yield return value.ToResourceEvent(EventTypeFlags.ResetEnd, key);
            }

            await enumerator.DisposeAsync();
        }

        
    }
}
