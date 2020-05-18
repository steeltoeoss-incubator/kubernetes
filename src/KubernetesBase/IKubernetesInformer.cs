using k8s;
using Steeltoe.Informers.InformersBase;

namespace Steeltoe.Informers.KubernetesBase
{
    /// <summary>
    ///     An informer that serves kubernetes resources
    /// </summary>
    /// <typeparam name="TResource">The type of Kubernetes resource</typeparam>
    public interface IKubernetesInformer<TResource> : 
        IInformer<string, TResource, KubernetesInformerOptions>, 
        IInformer<string, TResource> 
        where TResource : IKubernetesObject
    {
    }
}
