using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;

namespace Steeltoe.Informers.InformersBase
{
    public partial class Informable
    {
        public static IDisposable Subscribe<TKey, TSource>(this IInformable<TKey, TSource> source)
        {
            return source.ToObservable().Subscribe();
        }
    }
}