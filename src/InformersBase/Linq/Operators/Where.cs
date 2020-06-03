using System;
using System.Linq;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResource> Where<TKey, TResource>(this IInformable<TKey, TResource> source, Func<TResource, bool> predicate)
        {
            return source.Where(x => x.EventFlags.IsMetaEvent() || predicate(x.Value)).AsAsyncInformable();
        }
    }
}