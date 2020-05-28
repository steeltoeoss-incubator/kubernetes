using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        
        public static IInformable<Tuple<TOuterKey, TInnerKey>, TResult> Join<TOuterKey, TOuter, TInnerKey, TInner, TJoinKey, TResult>(
            this IInformable<TOuterKey, TOuter> outer,
            IInformable<TInnerKey, TInner> inner,
            Func<TInner, TJoinKey> innerKeySelector,
            Func<TOuter, TJoinKey> outerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector
        )
        {
            return Join(outer, inner, innerKeySelector, outerKeySelector, resultSelector, EqualityComparer<TJoinKey>.Default);
        }
        public static IInformable<Tuple<TOuterKey, TInnerKey>, TResult> Join<TOuterKey, TOuter, TInnerKey, TInner, TJoinKey, TResult>(
            this IInformable<TOuterKey, TOuter> outer,
            IInformable<TInnerKey, TInner> inner,
            Func<TInner, TJoinKey> innerKeySelector,
            Func<TOuter, TJoinKey> outerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector,
            IEqualityComparer<TJoinKey> comparer
        )
        {
            var merged = outer // sequences need to be merged into single stream to ensure synchronized subscription to both
                .Select((ResourceEvent<TOuterKey, TOuter> x) => (object) x)
                .Merge(inner.Select((ResourceEvent<TInnerKey, TInner> x) => (object) x))
                .Publish();
            var innerCache = new SimpleCache<TInner, TJoinKey>();
            var outerCache = new SimpleCache<TOuterKey, TOuter>();
            var innerStream = merged.Where(x => x is ResourceEvent<TOuterKey, TOuter>).Select(x => (ResourceEvent<TOuterKey, TOuter>)x);
            var outerStream = merged.Where(x => x is ResourceEvent<TInner, TJoinKey>).Select(x => (ResourceEvent<TInner, TJoinKey>)x);
            var subscription = new CompositeDisposable();
            var resultPairs = new HashSet<Tuple<TOuterKey, TInnerKey>>();
            
        }
    }
}