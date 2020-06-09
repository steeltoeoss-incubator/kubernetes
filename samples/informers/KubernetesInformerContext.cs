using k8s.Models;
using Steeltoe.Informers.InformersBase;
using Steeltoe.Informers.KubernetesBase;

namespace informers
{
    public class KubernetesInformerContext
    {
        public IKubernetesInformer<V1Pod> Pods { get; set; }
        public IKubernetesInformer<V1Service> Services { get; set; }
    }
}