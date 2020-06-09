using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Steeltoe.Informers.InformersBase;
using Steeltoe.Informers.InformersBase.Cache;

namespace System.Linq
{
    public partial class Informable
    {
        public static async Task<ICache<TKey, TSource>> ToSubscribedDictionary<TKey, TSource>(this IInformable<TKey, TSource> source, CancellationToken cancellationToken = default)
        {
            var cache = new SimpleCache<TKey, TSource>(source, cancellationToken);
            await cache.Synchronized;
            return cache;
        }
    }
}