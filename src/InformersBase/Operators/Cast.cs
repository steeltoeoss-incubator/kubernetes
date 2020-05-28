using System;
using System.Linq;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResult> Cast<TKey, TResource, TResult>(this IInformable<TKey, TResource> source) where TResource : class where TResult : class
        {
            return source.Select(x => ResourceEvent.Create(x.EventFlags, x.Key, x.Value as TResult, x.OldValue as TResult)).AsInformable();
        }
    }
}