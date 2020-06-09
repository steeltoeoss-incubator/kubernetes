using System.Collections.Generic;
using k8s.Models;

namespace Steeltoe.Informers.KubernetesBase.Tests
{
    public static class KubernetesTestData
    {
 
        public static V1Pod TestPod1ResourceVersion1 => new V1Pod()
        {
            Kind = V1Pod.KubeKind,
            ApiVersion = V1Pod.KubeApiVersion,
            Metadata = new V1ObjectMeta()
            {
                Name = "pod1",
                ResourceVersion = "pod1V1"
            }
        };


        public static V1Pod TestPod1ResourceVersion2 => new V1Pod()
        {
            Kind = V1Pod.KubeKind,
            ApiVersion = V1Pod.KubeApiVersion,
            Metadata = new V1ObjectMeta()
            {
                Name = "pod1",
                ResourceVersion = "pod1V2"
            }
        };
        public static V1Pod TestPod2ResourceVersion1 => new V1Pod()
        {
            Kind = V1Pod.KubeKind,
            ApiVersion = V1Pod.KubeApiVersion,
            Metadata = new V1ObjectMeta()
            {
                Name = "pod2",
                ResourceVersion = "pod2V1"
            }
        };

        public static V1Pod TestPod2ResourceVersion2 => new V1Pod()
        {
            Kind = V1Pod.KubeKind,
            ApiVersion = V1Pod.KubeApiVersion,
            Metadata = new V1ObjectMeta()
            {
                Name = "pod2",
                ResourceVersion = "pod2V2"
            }
        };

        public static V1PodList ListPodEmpty => new V1PodList()
        {
            Kind = V1PodList.KubeKind,
            ApiVersion = V1PodList.KubeApiVersion,
            Metadata = new V1ListMeta()
            {
                ResourceVersion = "podlistV1"
            }
        };
        public static V1PodList ListPodOneItem => new V1PodList()
        {
            Kind = V1PodList.KubeKind,
            ApiVersion = V1PodList.KubeApiVersion,
            Metadata = new V1ListMeta()
            {
                ResourceVersion = "podlistV2"
            },
            Items = new List<V1Pod>()
            {
                TestPod1ResourceVersion1
            }
        };
        public static V1PodList ListPodsTwoItems => new V1PodList()
        {
            Kind = V1PodList.KubeKind,
            ApiVersion = V1PodList.KubeApiVersion,
            Metadata = new V1ListMeta()
            {
                ResourceVersion = "podlistV3"
            },
            Items = new List<V1Pod>()
            {
                TestPod1ResourceVersion1,
                TestPod2ResourceVersion1
            }
        };
    }
}
