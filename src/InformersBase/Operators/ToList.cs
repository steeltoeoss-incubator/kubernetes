using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Steeltoe.Informers.InformersBase
{
    public partial class Informable
    {
        public static async Task<IList<TSource>> ToListAsync<TKey, TSource>(this IInformable<TKey, TSource> source, CancellationToken cancellationToken)
        {
            var result = await source.ToEventList(cancellationToken);
            return result.Select(x => x.Value).ToList();
        }
        public static async Task<IDictionary<TKey, TSource>> ToDictionaryAsync<TKey, TSource>(this IInformable<TKey, TSource> source, CancellationToken cancellationToken)
        {
            var result = await source.ToEventList(cancellationToken);
            return result.ToDictionary(x => x.Key, x => x.Value);
        }
        
        internal static Task<IList<ResourceEvent<TKey, TSource>>> ToEventList<TKey, TSource>(this IInformable<TKey, TSource> source, CancellationToken cancellationToken)
        {
            
            var result = new TaskCompletionSource<IList<ResourceEvent<TKey, TSource>>>();
            var cts = new CancellationTokenSource();
            
            cancellationToken.Register(() => cts.Cancel());
            Task.Run(async () =>
                {
                    try
                    {
                        IList<IList<ResourceEvent<TKey, TSource>>> resetBlock = null;
                        var firstEventPastReset = await source
                            .WithResets(list =>
                            {
                                result.SetResult(list.ToList());
                                return list;
                            })
                            .FirstAsync(cancellationToken: cts.Token);
                        if (firstEventPastReset.EventFlags.HasFlag(EventTypeFlags.Reset))
                        {
                            result.SetException(new InvalidOperationException("The source informer stream has sent invalid data. Stream must start with reset block. "));
                        }
                        
                    }
                    catch (Exception e)
                    {
                        result.TrySetException(e);
                    }
                }
            );
            return result.Task;

        }
    }
}