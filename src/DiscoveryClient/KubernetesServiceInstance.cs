using System;
using System.Collections.Generic;
using System.Linq;
using k8s.Models;
using Newtonsoft.Json;
using Steeltoe.Common.Discovery;

namespace Steeltoe.Discovery.KubernetesBase
{
    public class KubernetesServiceInstance : IServiceInstance
    {
        public KubernetesServiceInstance(V1Service service, V1Pod pod)
        {
            KubernetsService = service;
            Pod = pod;
        }

        [JsonIgnore]
        public V1Service KubernetsService { get; }
        [JsonIgnore]
        public V1Pod Pod { get; }

        public string ServiceId => KubernetsService.Metadata.Name;
        public string Host => KubernetsService.Status?.LoadBalancer?.Ingress?.Select(x => x.Hostname ?? x.Ip).FirstOrDefault();
        public int Port => KubernetsService.Spec?.Ports?.Select(x => x.Port).FirstOrDefault() ?? 0;
        public bool IsSecure => Port == 443;
        public Uri Uri => !string.IsNullOrEmpty(Host) ? new Uri($"http://{Host}:{Port}") : null;
        public IDictionary<string, string> Metadata => KubernetsService.Metadata.Annotations;
    }
}