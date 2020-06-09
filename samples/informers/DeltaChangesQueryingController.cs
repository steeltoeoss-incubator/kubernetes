using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using KellermanSoftware.CompareNetObjects;
using Microsoft.Extensions.Logging;
using Steeltoe.Informers.InformersBase;
using Steeltoe.Informers.KubernetesBase;

namespace informers
{
    // this sample demos of informer in a basic controller context.
    // there are two loggers:
    //   _informerLogger lets you see raw data coming out of informer stream

    // try creating and deleting some pods in "default" namespace and watch the output
    // current code is not production grade and lacks concurrency guards against modifying same resource
    public class DeltaChangesQueryingController : IController
    {
        private readonly IKubernetesInformer<V1Pod> _podInformer;
        private readonly CompositeDisposable _subscription = new CompositeDisposable();
        private readonly ILogger _informerLogger;
        private readonly CompareLogic _objectCompare = new CompareLogic();

        public DeltaChangesQueryingController(IKubernetesInformer<V1Pod> podInformer, ILoggerFactory loggerFactory)
        {
            _podInformer = podInformer;
            _informerLogger = loggerFactory.CreateLogger("Informer");
            _objectCompare.Config.MaxDifferences = 100;
        }


        public Task Initialize(CancellationToken cancellationToken)
        {
            _podInformer
                .ListWatch(KubernetesInformerOptions.Builder.NamespaceEquals("default").Build())
                .Do(PrintChanges)
                .Subscribe()
                .DisposeWith(_subscription);
            return Task.CompletedTask;
        }

        private void PrintChanges(ResourceEvent<string, V1Pod> changes, V1Pod previousValue)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"==={changes.EventFlags}===");
            sb.AppendLine($"Name: {changes.Value.Metadata.Name}");
            sb.AppendLine($"Version: {changes.Value.Metadata.ResourceVersion}");
            if (changes.EventFlags.HasFlag(EventTypeFlags.Modify))
            {
                var updateDelta = _objectCompare.Compare(previousValue, changes.Value);
                foreach (var difference in updateDelta.Differences)
                {
                    sb.AppendLine($"{difference.PropertyName}: {difference.Object1} -> {difference.Object2}");
                }
            }
            
            _informerLogger.LogInformation(sb.ToString());

        }
    }
}
