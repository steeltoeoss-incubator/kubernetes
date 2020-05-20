using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using k8s;
using k8s.Models;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.KubernetesBase.Cache
{
    public class KubernetesCache<TResource> : SimpleCache<string, TResource> where TResource : IKubernetesObject<V1ObjectMeta>
    {
        public KubernetesCache()
        {
            AddIndex("namespace", resource =>  resource.Metadata?.NamespaceProperty != null ? new List<string> { resource.Metadata.NamespaceProperty } : new List<string>());
            AddIndex("label", resource =>  resource.Metadata.Labels != null ? resource.Metadata.Labels.Keys.ToList() : new List<string>());
            AddIndex("labelValue", resource =>  resource.Metadata.Labels != null ? resource.Metadata.Labels.Select(x => $"{x.Key}:{x.Value}").ToList() : new List<string>());
        }

        public List<TResource> ByNamespace(string name) => ByIndex("namespace", name);
        public List<TResource> ByLabel(string labelName) => ByIndex("label", labelName);
        public List<TResource> ByLabel(string labelName, string labelValue) => ByIndex("labelValue", $"{labelName}:{labelValue}");
        public void Add(TResource value) => Add(value.Metadata.Name, value);
        public void Set(TResource value) => this[value.Metadata.Name] = value;
        public bool Remove(TResource value) => Remove(value.Metadata.Name);
        public bool Contains(TResource value) => ContainsKey(value.Metadata.Name);
    }
}