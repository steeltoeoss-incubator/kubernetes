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
        internal static async Task<IList<ResourceEvent<TKey, TSource>>> ToEventList<TKey, TSource>(this IInformable<TKey, TSource> source, CancellationToken cancellationToken = default)
        {
            var resetDetector = new ResetExtractor<TKey,TSource>();
            await foreach (var item in source)
            {
                if (resetDetector.ApplyEvent(item, out var reset))
                    return reset;
            }
            return new List<ResourceEvent<TKey, TSource>>();
        }
    }
}