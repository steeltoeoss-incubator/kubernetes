using System;
using System.Collections.Generic;

namespace Steeltoe.Informers.InformersBase
{
    public interface IInformable<TKey, TResource> : IAsyncEnumerable<ResourceEvent<TKey, TResource>>
    {
        
    }
}