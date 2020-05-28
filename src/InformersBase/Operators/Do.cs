using System;
using System.Linq;
using System.Threading.Tasks;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResource> Do<TKey, TResource>(this IInformable<TKey, TResource> source, Action<ResourceEvent<TKey,TResource>> onChange)
        {
            var result = source.Select(x =>
            {
                onChange(x);
                return x;
            }).AsInformable();
            return result;
        }
        public static IInformable<TKey, TResource> Do<TKey, TResource>(this IInformable<TKey, TResource> source, Func<ResourceEvent<TKey,TResource>,Task> onChange)
        {
            var result = source.SelectAwait(async (ResourceEvent<TKey, TResource> x) =>
            {
                await onChange(x);
                return x;
            }).AsInformable();
            return result;
        }
    }
}