using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        // this kind of method would be highly inefficient working against dictionary cuz it would have to do a full delta on all keys for each receivied message
        // unless upstream source could provide iinformable as a property of TResult - highly unlikely
        
        // public static IInformable<TKeyResult, TResult> SelectMany<TKeySource, TSource, TKeyResult, TResult>(
        //     this IInformable<TKeySource, TSource> source,
        //     Func<TSource, IDictionary<TKeyResult, TResult>> selector) 
        // {
        //     var cache = new SimpleCache<TKeySource, TSource>();
        //     var selectorCache = new Dictionary<TKeyResult, TResult>();
        //     
        //     // source.Into(cache).Do(x => x.Value.Value.);
        //     // return source.SelectMany(x => selector(x)).AsInformable();
        // }
        
    }
    
}