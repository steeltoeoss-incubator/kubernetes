using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using Steeltoe.Informers.InformersBase.Tests.Utils;

namespace Steeltoe.Informers.KubernetesBase.Tests
{
    public static class KubernetesTestExtensions
    {
        public static Watcher<T>.WatchEvent ToWatchEvent<T>(this T obj, WatchEventType eventType)
        {
            return new Watcher<T>.WatchEvent { Type = eventType, Object = obj };
        }

        public static HttpOperationResponse<KubernetesList<TV>> ToHttpOperationResponse<TL, TV>(this TL obj) where TL : IItems<TV> where TV : IKubernetesObject
        {
            return new HttpOperationResponse<KubernetesList<TV>>()
            {
                Body = JsonConvert.DeserializeObject<KubernetesList<TV>>(obj.ToJson()),
                Response = new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(obj.ToJson())

                }
            };
        }

        public static HttpOperationResponse<KubernetesList<T>> ToHttpOperationResponse<T>(this Watcher<T>.WatchEvent obj) where T : IKubernetesObject
        {
            return new[] { obj }.ToHttpOperationResponse();
        }

        public static HttpOperationResponse<KubernetesList<T>> ToHttpOperationResponse<T>(this IEnumerable<Watcher<T>.WatchEvent> obj) where T : IKubernetesObject
        {
            var stringContent = new StringContent(string.Join("\n", obj.Select(x => x.ToJson())));
            var contentType = Type.GetType("k8s.WatcherDelegatingHandler+LineSeparatedHttpContent, KubernetesClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=a0f90e8c9af122de");
            dynamic lineContent = Activator.CreateInstance(contentType, stringContent, CancellationToken.None);
            lineContent.LoadIntoBufferAsync().Wait();
            var httpResponse = new HttpOperationResponse<KubernetesList<T>>()
            {
                Response = new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = lineContent
                }
            };
            return httpResponse;
        }
    }
}