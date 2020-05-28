using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        
        public static IAsyncInformable<Tuple<TOuterKey, TInnerKey>, TResult> Join<TOuterKey, TOuter, TInnerKey, TInner, TJoinKey, TResult>(
            this IAsyncInformable<TOuterKey, TOuter> outer,
            IAsyncInformable<TInnerKey, TInner> inner,
            Func<TInner, TJoinKey> innerKeySelector,
            Func<TOuter, TJoinKey> outerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector
        )
        {
            return Join(outer, inner, innerKeySelector, outerKeySelector, resultSelector, EqualityComparer<TJoinKey>.Default);
        }
        public static IAsyncInformable<Tuple<TOuterKey, TInnerKey>, TResult> Join<TOuterKey, TOuter, TInnerKey, TInner, TJoinKey, TResult>(
            this IAsyncInformable<TOuterKey, TOuter> outer,
            IAsyncInformable<TInnerKey, TInner> inner,
            Func<TInner, TJoinKey> innerKeySelector,
            Func<TOuter, TJoinKey> outerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector,
            IEqualityComparer<TJoinKey> comparer
        )
        {
            return Create(Core);


            async IAsyncEnumerator<ResourceEvent<Tuple<TOuterKey, TInnerKey>, TResult>> Core(CancellationToken cancellationToken)
            {
                await using var e = outer.GetConfiguredAsyncEnumerator(cancellationToken, false);

                if (await e.MoveNextAsync())
                {
                    var lookup = await Internal.Lookup<TJoinKey, TInnerKey, TInner>.CreateForJoinAsync(inner, innerKeySelector, comparer, cancellationToken).ConfigureAwait(false);

                    if (lookup.Count != 0)
                    {
                        do
                        {
                            var item = e.Current;

                            var outerKey = outerKeySelector(item);

                            var g = lookup.GetGrouping(outerKey);

                            if (g != null)
                            {
                                var count = g._count;
                                var elements = g._elements;

                                for (var i = 0; i != count; ++i)
                                {
                                    yield return resultSelector(item, elements[i]);
                                }
                            }
                        }
                        while (await e.MoveNextAsync());
                    }
                }
            }
            // var merged = outer // sequences need to be merged into single stream to ensure synchronized subscription to both
            //     .Select((ResourceEvent<TOuterKey, TOuter> x) => (object) x)
            //     .Merge(inner.Select((ResourceEvent<TInnerKey, TInner> x) => (object) x))
            //     .Publish();
            // var innerCache = new SimpleCache<TInner, TJoinKey>();
            // var outerCache = new SimpleCache<TOuterKey, TOuter>();
            // var innerStream = merged.Where(x => x is ResourceEvent<TOuterKey, TOuter>).Select(x => (ResourceEvent<TOuterKey, TOuter>)x);
            // var outerStream = merged.Where(x => x is ResourceEvent<TInner, TJoinKey>).Select(x => (ResourceEvent<TInner, TJoinKey>)x);
            // var subscription = new CompositeDisposable();
            // var resultPairs = new HashSet<Tuple<TOuterKey, TInnerKey>>();
            
        }
    }
}