using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Steeltoe.Informers.InformersBase
{
    /// <summary>
    ///     Manages multiple <see cref="SharedInformer{TKey,TResource}" /> for each unique set of <typeparamref name="TOptions" /> and ensures subscriptions are attached to correct one
    /// </summary>
    /// <typeparam name="TResource"></typeparam>
    /// <typeparam name="TOptions"></typeparam>
    public class SharedOptionsInformer<TKey, TResource, TOptions> : IInformer<TKey, TResource, TOptions>
    {
        private readonly IInformer<TKey, TResource, TOptions> _masterInformer;
        private readonly Func<IInformer<TKey, TResource>, IInformer<TKey, TResource>> _sharedInformerFactory;
        private readonly ConcurrentDictionary<TOptions, IInformer<TKey, TResource>> _sharedInformers = new ConcurrentDictionary<TOptions, IInformer<TKey, TResource>>();

        public SharedOptionsInformer(
            IInformer<TKey, TResource, TOptions> masterInformer,
            Func<IInformer<TKey, TResource>, IInformer<TKey, TResource>> sharedInformerFactory)
        {
            _masterInformer = masterInformer;
            _sharedInformerFactory = sharedInformerFactory;
        }


        public IAsyncEnumerable<TResource> List(TOptions options, CancellationToken cancellationToken)
        {
            var sharedInformer = _sharedInformers.GetOrAdd(options, opt => _sharedInformerFactory(_masterInformer.WithOptions(opt)));
            return sharedInformer.List(cancellationToken);
        }

        public IObservable<ResourceEvent<TKey, TResource>> ListWatch(TOptions options)
        {
            var sharedInformer = _sharedInformers.GetOrAdd(options, opt => _sharedInformerFactory(_masterInformer.WithOptions(opt)));
            return sharedInformer.ListWatch();
        }
    }
}
