using System;
using System.Collections.Generic;
using System.Threading;

namespace Steeltoe.Informers.InformersBase
{
    internal class InformableAdapter<TKey, TResource> : IInformable<TKey, TResource>
    {
        private readonly IAsyncEnumerable<ResourceEvent<TKey, TResource>> _source;

        public InformableAdapter(IAsyncEnumerable<ResourceEvent<TKey, TResource>> source)
        {
            _source = source;
        }

        public IAsyncEnumerator<ResourceEvent<TKey, TResource>> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken()) =>
            _source.GetAsyncEnumerator(cancellationToken);
    }
}