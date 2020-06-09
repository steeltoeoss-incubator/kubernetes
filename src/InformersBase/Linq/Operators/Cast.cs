using System;
using System.Linq;
using Steeltoe.Informers.InformersBase;

namespace System.Linq
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResult> Cast<TKey, TResource, TResult>(this IInformable<TKey, TResource> source)
        {
            return source.Select(x => ResourceEvent.Create(x.EventFlags, x.Key, (TResult)(object)x.Value)).AsInformable();
        }
    }
}