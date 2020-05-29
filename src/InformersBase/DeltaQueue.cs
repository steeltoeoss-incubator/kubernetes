using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using k8s.Informers.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Steeltoe.Informers.InformersBase;

namespace k8s
{
    /// <summary>
    ///     A queue that enqueues ResourceEvents and dequeues List<ResourceEvent>.
    ///     It groups delta changes per key, sending each group to downstream consumer as individual messages
    ///     This allows "Per resource ID" processing semantics
    ///     Any sync events are only propagated if no other changes are queued up
    /// </summary>
    /// <typeparam name="TKey">The type of key used to identity resource</typeparam>
    /// <typeparam name="TResource">The resource type</typeparam>
    //todo: get rid of TCS and replace with IValueTaskSource 
    public class DeltaQueue<TKey, TResource> : IAsyncEnumerator<ResourceEvent<TKey, TResource>>
    {
        private readonly Dictionary<TKey, List<ResourceEvent<TKey, TResource>>> _items = new Dictionary<TKey, List<ResourceEvent<TKey, TResource>>>();
        /// <summary>
        /// Protects access to _queue, _targets and _items as well as ensuring some operations are executed in atomic way
        /// </summary>
        private readonly object _lock = new object();
        private readonly Queue<TKey> _queue = new Queue<TKey>();
        private readonly TaskCompletionSource<object> _taskCompletionSource = new TaskCompletionSource<object>();
        private TaskCompletionSource<List<ResourceEvent<TKey, TResource>>> _dequeueTask = new TaskCompletionSource<List<ResourceEvent<TKey, TResource>>>();
        private bool _isCompleting;

        /// <param name="log">Logger</param>
        public DeltaQueue()
        {

            _dequeueTask.SetResult(null);
        }

        public Task Completion => _taskCompletionSource.Task;

        public void Enqueue(ResourceEvent<TKey, TResource> resourceEvent)
        {
            lock (_lock)
            {
                if (resourceEvent.EventFlags.HasFlag(EventTypeFlags.Sync) && _items.ContainsKey(resourceEvent.Key))
                {
                    return;
                }

                // if there's someone waiting to consume from queue, give them this item and not even queue it up
                if (!_dequeueTask.Task.IsCompleted && _dequeueTask.TrySetResult(new List<ResourceEvent<TKey, TResource>> { resourceEvent }))
                {
                    return;
                }

                if (resourceEvent.EventFlags.HasFlag(EventTypeFlags.ResetEmpty))
                {
                    return;
                }

                var id = resourceEvent.Key;
                var exists = _items.TryGetValue(id, out var deltas);
                if (!exists)
                {
                    deltas = new List<ResourceEvent<TKey, TResource>>();
                    _items[id] = deltas;
                    _queue.AddLast(deltas);
                }

                deltas.Add(resourceEvent);
                CombineDeltas(deltas);
            }
        }



        public async Task<List<ResourceEvent<TKey, TResource>>> Dequeue(CancellationToken cancellationToken)
        {
            Task<List<ResourceEvent<TKey, TResource>>> result;
            lock (_lock)
            {
                while (true)
                {
                    SetCompletedIfNeeded();
                    if (Completion.IsCompleted)
                    {
                        return null;
                    }

                    if (_queue.Count == 0) // queue is empty, nothing left to do
                    {
                        _dequeueTask = new TaskCompletionSource<List<ResourceEvent<TKey, TResource>>>();
                        cancellationToken.Register(() => _dequeueTask.SetCanceled());
                        result = _dequeueTask.Task;
                        break;
                    }

                    var deltas = _queue.First.Value;
                    // this can happen if the entire lifecycle of the object started and ended before worker even touched it
                    if (!deltas.Any())
                    {
                        continue;
                    }

                    var id = deltas.First().Key;

                    // some condition caused this queued item to be expired, go to next one
                    if (!_items.Remove(id))
                    {
                        continue;
                    }

                    if (_holdResources.Contains(id))
                    {
                        continue;
                    }

                    return deltas;
                }
            }
            return await result;
        }

        public void Complete()
        {
            _isCompleting = true;
            SetCompletedIfNeeded();
        }




        private void CombineDeltas(List<ResourceEvent<TKey, TResource>> deltas)
        {
            if (deltas.Count < 2)
            {
                return;
            }

            if (deltas.First().EventFlags.HasFlag(EventTypeFlags.Sync)) // if we had a sync item queued up and got something else, get rid of sync
            {
                deltas.RemoveAt(0);
            }

            if (deltas.Count < 2)
            {
                return;
            }

            // if the entire object was created and removed before worker got a chance to touch it and worker has not chose to see these
            // types of events, we can just get rid of this "transient" object and not even notify worker of its existence
            if (_skipTransient && deltas[0].EventFlags.HasFlag(EventTypeFlags.Add) && deltas[deltas.Count - 1].EventFlags.HasFlag(EventTypeFlags.Delete))
            {
                deltas.Clear();
            }
        }

        /// <summary>
        ///     Checks if block is marked for completion and marks itself as completed after queue is drained
        /// </summary>
        private void SetCompletedIfNeeded()
        {
            lock (_lock)
            {
                if (!_isCompleting || _queue.Count != 0)
                {
                    return;
                }
            }
            _taskCompletionSource.TrySetResult(null);
            _dequeueTask.TrySetResult(null);
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> MoveNextAsync()
        {
            Task<bool> z = null;
            new ValueTask(z);
        }

        public ResourceEvent<TKey, TResource> Current { get; }

    }
}
