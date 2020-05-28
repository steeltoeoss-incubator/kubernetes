using System;
using System.Linq;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResult> Select<TKey, TSource, TResult>(this IInformable<TKey, TSource> source, Func<TSource, TResult> selector)
        {
            return source.Select(x => ResourceEvent.Create(x.EventFlags, x.Key, SafeSelector(x.Value, selector), SafeSelector(x.OldValue, selector)))
                .AsInformable();
        }

        
    }
}