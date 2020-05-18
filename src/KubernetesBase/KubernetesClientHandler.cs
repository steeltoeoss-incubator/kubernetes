using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s;

namespace Steeltoe.Informers.KubernetesBase
{
    internal class KubernetesClientHandler : DelegatingHandler
    {
        private readonly TimeSpan _timeout;
        private readonly KubernetesClientConfiguration _configuration;

        public KubernetesClientHandler(TimeSpan timeout, KubernetesClientConfiguration configuration)
        {
            _timeout = timeout;
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri.Query;
            var index = query.IndexOf("watch=true");
            var isWatch = index > 0 && (query[index - 1] == '&' || query[index - 1] == '?');

            if (!isWatch)
            {
                var cts = new CancellationTokenSource(_timeout);
                cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token).Token;
            }

            var credentials = k8s.Kubernetes.CreateCredentials(_configuration);
            if (credentials != null)
            {
                await credentials.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(false);
            }

            var originResponse = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return originResponse;
        }
    }
}