using System;
using System.Collections.Generic;
using System.Threading;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Steeltoe.Informers.InformersBase;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.KubernetesBase
{
    /// <summary>
    ///     Opens a single connection to API server with per unique <see cref="KubernetesInformerOptions" />
    ///     and attaches 1 or more internal subscriber to it. The connection is automatically opened if there is
    ///     at least one subscriber and closes if there are none
    /// </summary>
    /// <typeparam name="TResource">The type of resource to monitor</typeparam>
    internal class SharedKubernetesInformer<TResource> :
        SharedOptionsInformer<string, TResource, KubernetesInformerOptions>,
        IKubernetesInformer<TResource>
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        public SharedKubernetesInformer(KubernetesInformer<TResource> masterInformer, ILoggerFactory loggerFactory)
            : base(masterInformer, SharedKubernetesInformerFactory(loggerFactory, GetVersionPartitionedCacheFactory()))
        {
        }

        public SharedKubernetesInformer(KubernetesInformer<TResource> masterInformer, Func<ICache<string, TResource>> cacheFactory, ILoggerFactory loggerFactory)
            : base(masterInformer, SharedKubernetesInformerFactory(loggerFactory, cacheFactory))
        {
        }

        public IAsyncEnumerable<TResource> List(CancellationToken cancellationToken) => base.List(KubernetesInformerOptions.Default, cancellationToken);
        
        /// <inheritdoc cref="IKubernetesInformer{TResource}" />
        public IObservable<ResourceEvent<string, TResource>> ListWatch() => base.ListWatch(KubernetesInformerOptions.Default);

        private static Func<ICache<string, TResource>> GetSimpleCacheFactory()
        {
            return () => new SimpleCache<string, TResource>();
        }
        private static Func<ICache<string, TResource>> GetVersionPartitionedCacheFactory()
        {
            var partitionedSharedCache = new VersionPartitionedSharedCache<string, TResource, string>(x => x.Metadata.Name, x => x.Metadata.ResourceVersion);
            return () => partitionedSharedCache.CreatePartition();
        }

        private static Func<IInformer<string, TResource>, IInformer<string, TResource>> SharedKubernetesInformerFactory(ILoggerFactory loggerFactory, Func<ICache<string, TResource>> cacheFactory) =>
            masterInformer => new SharedInformer<string, TResource>(
                masterInformer,
                loggerFactory.CreateLogger<ILogger<SharedInformer<string, TResource>>>(),
                cacheFactory());
    }
}
