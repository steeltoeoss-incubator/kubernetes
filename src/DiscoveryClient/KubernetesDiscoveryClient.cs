using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Reactive.Linq;
using k8s.Models;
using Steeltoe.Common.Discovery;
using Steeltoe.Kubernetes.InformersBase;
using Steeltoe.Kubernetes.InformersBase.Cache;

namespace Steeltoe.Discovery.KubernetesBase
{
    public class KubernetesDiscoveryClient : IDiscoveryClient
    {
        private readonly ICache<string, V1Service> _services = new SimpleCache<string, V1Service>();
        private readonly ICache<string, V1Pod> _pods = new SimpleCache<string, V1Pod>();
        private readonly CompositeDisposable _subscription = new CompositeDisposable();
        public KubernetesDiscoveryClient(IKubernetesInformer<V1Service> serviceInformer, IKubernetesInformer<V1Pod> podInformer)
        {
            var options = KubernetesInformerOptions.Builder.NamespaceEquals("default").Build();
            serviceInformer.ListWatch(options)
                .Where(x => x.Value == null || x.Value.Spec.Type == "LoadBalancer")
                .Into(_services)
                .Subscribe()
                .DisposeWith(_subscription);

            podInformer.ListWatch(options)
                .Into(_pods)
                .Subscribe()
                .DisposeWith(_subscription);
        }

        public string Description => "Kubernetes Client";
        public IList<string> Services => _services.Values.Select(x => x.Metadata.Name).Where(x => GetInstances(x).Any()).ToList();
        public IList<IServiceInstance> GetInstances(string serviceId)
        {
            _services.TryGetValue(serviceId, out var service);
            if(service == null) return Array.Empty<IServiceInstance>();

            var readyPods = _pods.Values
                .Where(pod => IsSelectedBy(pod, service) && IsPodReady(pod))
                .Select(pod => new KubernetesServiceInstance(service, pod))
                .Cast<IServiceInstance>()
                .ToList();
            
            return readyPods;
        }

        private static bool IsSelectedBy(V1Pod pod, V1Service service)
        {
            return service.Spec.Selector.Any(s => pod.Metadata.Labels.TryGetValue(s.Key, out var value) && value == s.Value);
        }

        private static bool IsPodReady(V1Pod pod) => pod.Status.Conditions.All(c => c.Status == "True");
        
        public IServiceInstance GetLocalServiceInstance()
        {
            return null;
        }

        public Task ShutdownAsync()
        {
            _subscription.Dispose();
            return Task.CompletedTask;
        }
    }
}