using System;
using System.Threading;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Microsoft.Rest.TransientFaultHandling;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Steeltoe.Informers.KubernetesBase;

namespace Steeltoe.Informers.KubernetesCore
{
    public static class Extensions
    {
        public static IServiceCollection AddKubernetesClient(this IServiceCollection services, Func<KubernetesClientConfiguration> configProvider)
        {
            
            var config = configProvider();
            
            void ConfigureSerializerSettings(JsonSerializerSettings settings)
            {
                settings.ContractResolver = new ReadOnlyJsonContractResolver() {NamingStrategy = new CamelCaseNamingStrategy()};
            }

            services.AddHttpClient("DefaultName")
                .AddTypedClient<IKubernetes>((httpClient, serviceProvider) =>
                {
                    httpClient.Timeout = Timeout.InfiniteTimeSpan;
                    var kubernetes = new Kubernetes(config, httpClient);
                    httpClient.BaseAddress = kubernetes.BaseUri;
                    ConfigureSerializerSettings(kubernetes.SerializationSettings);
                    ConfigureSerializerSettings(kubernetes.DeserializationSettings);
                    return kubernetes;
                })
                .AddHttpMessageHandler(() => new KubernetesClientHandler(TimeSpan.FromSeconds(100), config))
                .AddHttpMessageHandler(() => new RetryDelegatingHandler { RetryPolicy = new RetryPolicy<HttpStatusCodeErrorDetectionStrategy>(new ExponentialBackoffRetryStrategy()) })
                .AddHttpMessageHandler(KubernetesClientConfiguration.CreateWatchHandler)
                .ConfigurePrimaryHttpMessageHandler(config.CreateDefaultHttpClientHandler);
            
            services.AddSingleton<IKubernetesGenericClient>(container =>
            {
                var kubernetesClient = (Kubernetes) container.GetRequiredService<IKubernetes>();
                return new KubernetesGenericClient(kubernetesClient.HttpClient, kubernetesClient.SerializationSettings, kubernetesClient.DeserializationSettings);
            }); 

            return services;
        }

        public static IServiceCollection AddKubernetesInformers(this IServiceCollection services)
        {
            services.AddTransient(typeof(KubernetesInformer<>));
            services.AddSingleton(typeof(IKubernetesInformer<>), typeof(KubernetesInformer<>));
            return services;
        }
    }
}
