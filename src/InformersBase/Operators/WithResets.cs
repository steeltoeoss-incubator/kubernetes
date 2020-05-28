using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Steeltoe.Informers.InformersBase
{
public static partial class Informable
    {
        public static IInformable<TKey, TResource> WithResets<TKey, TResource>(this IInformable<TKey, TResource> source, Func<List<ResourceEvent<TKey, TResource>>, IEnumerable<ResourceEvent<TKey, TResource>>> action)
        {
            return source.WithResets(action, e => e).AsInformable();
        }

        public static IAsyncEnumerable<TOut> WithResets<TKey, TResource, TOut>(
            this IInformable<TKey, TResource> source,
            Func<List<ResourceEvent<TKey, TResource>>, IEnumerable<ResourceEvent<TKey, TResource>>> resetSelector,
            Func<ResourceEvent<TKey, TResource>, TOut> itemSelector)
        {
            var resetBuffer = new List<ResourceEvent<TKey, TResource>>();

            
            async IAsyncEnumerator<TOut> SourceEnumerator(CancellationToken token)
            {
                IEnumerable<TOut> FlushBuffer()
                {
                    if (!resetBuffer.Any())
                    {
                        yield break;
                    }

                    foreach (var item in resetSelector(resetBuffer))
                    {
                        if(token.IsCancellationRequested)
                            yield break;
                        yield return itemSelector(item);
                    }
                    resetBuffer.Clear();
                }
                await foreach (var notification in source.WithCancellation(token))
                {
                    var isExplicitResetEnd = notification.EventFlags.HasFlag(EventTypeFlags.ResetEnd);

                    if (notification.EventFlags.HasFlag(EventTypeFlags.Reset))
                    {
                        resetBuffer.Add(notification);
                        if (!notification.EventFlags.HasFlag(EventTypeFlags.ResetEnd)) // continue buffering till we reach the end of list window
                        {
                            continue;
                        }
                    }

                    var isImplicitResetEnd = !notification.EventFlags.HasFlag(EventTypeFlags.Reset) && resetBuffer.Count > 0;
                    if (isExplicitResetEnd || isImplicitResetEnd)
                    {
                        foreach (var bufferItem in FlushBuffer())
                        {
                            yield return bufferItem;
                        }

                        if (isExplicitResetEnd)
                        {
                            // item was already flushed as part of reset buffer
                            continue;
                        }
                    }

                    yield return itemSelector(notification);

                }
            }
            return AsyncEnumerable.Create(x => SourceEnumerator(x));
        }
        
    }
}