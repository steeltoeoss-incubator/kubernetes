using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using Steeltoe.Informers.InformersBase;

namespace Steeltoe.Informers.KubernetesBase
{
    public static class KubernetesExtensions
    {
        public static KubernetesEntityAttribute GetKubernetesTypeMetadata<T>(this T obj) where T : IKubernetesObject
        {
            return obj.GetType().GetKubernetesTypeMetadata();
        }

        public static KubernetesEntityAttribute GetKubernetesTypeMetadata(this Type currentType)
        {
            var attr = currentType.GetCustomAttribute<KubernetesEntityAttribute>();
            if (attr == null)
            {
                throw new InvalidOperationException($"Custom resource must have {nameof(KubernetesEntityAttribute)} applied to it");
            }

            return attr;
        }

        public static T Initialize<T>(this T obj) where T : IKubernetesObject
        {
            var metadata = obj.GetKubernetesTypeMetadata();

            obj.ApiVersion = !string.IsNullOrEmpty(metadata.Group) ? $"{metadata.Group}/{metadata.ApiVersion}" : metadata.ApiVersion;
            obj.Kind = metadata.Kind ?? obj.GetType().Name;
            if (obj is IMetadata<V1ObjectMeta> withMetadata && withMetadata.Metadata == null)
            {
                withMetadata.Metadata = new V1ObjectMeta();
            }

            return obj;
        }
        public static IObservable<Watcher<T>.WatchEvent> Watch<T>(this Task<HttpOperationResponse<KubernetesList<T>>> responseTask) where T : IKubernetesObject
        {
            return Observable.Create<k8s.Watcher<T>.WatchEvent>(observer =>
            {
                void OnNext(WatchEventType type, T item) => observer.OnNext(new k8s.Watcher<T>.WatchEvent { Type = type, Object = item });
                var watcher = responseTask.Watch<T, KubernetesList<T>>(OnNext, observer.OnError, observer.OnCompleted);
                var eventSubscription = Disposable.Create(() =>
                {
                    watcher.OnEvent -= OnNext;
                    watcher.OnError -= observer.OnError;
                    watcher.OnClosed -= observer.OnCompleted;
                });
                return new CompositeDisposable(watcher, eventSubscription);
            });
        }
        
        public static ResourceEvent<string, TResource> ToResourceEvent<TResource>(this TResource obj, EventTypeFlags typeFlags, TResource oldValue = default) 
            where TResource : IKubernetesObject<V1ObjectMeta>
        {
            if (typeFlags.HasFlag(EventTypeFlags.Delete) && oldValue == null)
            {
                oldValue = obj;
            }
            return new ResourceEvent<string, TResource>(typeFlags, obj.Metadata.Name, obj, oldValue);
        }
        internal static bool IsValidKubernetesName(this string value)
        {
            return !Regex.IsMatch(value, "^[a-z0-9-]+$");
        }
    }
}