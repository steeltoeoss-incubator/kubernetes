using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.InformersBase
{
    /// <summary>
    ///     Wraps a single master informer (such as Kubernetes API connection) for rebroadcast to multiple internal subscribers
    ///     and is responsible for managing and synchronizing cache
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Allows rebroadcasting of single informer provided by masterInformer to multiple internal subscribers.
    ///         Lazy loading semantics apply where subscription to master informer is only established when there's at least one attached observer, and it is closed if all observers disconnect
    ///     </para>
    ///     <para>
    ///         <see cref="SharedInformer{TKey,TResource}" /> is considered the sole owner of managing the cache. Since cache is used as the source of truth for "List" operations of any downstream subscribers,
    ///         any attempt to modify cache externally will result in desynchronization. Shared informer will only start emitting events downstream after cache has been synchronized
    ///         (after first List).
    ///     </para>
    /// </remarks>
    /// <seealso cref="IInformer{TKey,TResource}" />
    public class SharedInformer<TKey, TResource> : IInformer<TKey, TResource>
    {
        private readonly IInformer<TKey, TResource> _masterInformer;

        private readonly ICache<TKey, TResource> _cache;
        // private readonly Func<TResource, TKey> _keySelector;
        private readonly object _lock = new object();
        private readonly ILogger _logger;
        private readonly CountdownEvent _waitingSubscribers = new CountdownEvent(0);
        private TaskCompletionSource<bool> _cacheSynchronized = new TaskCompletionSource<bool>();
        private readonly IConnectableObservable<CacheSynchronized<ResourceEvent<TKey, TResource>>> _masterObservable;
        private readonly IScheduler _masterScheduler;
        private IDisposable _masterSubscription;
        private int _subscribers;

        public SharedInformer(IInformer<TKey, TResource> masterInformer, ILogger logger)
            : this(masterInformer, logger, new SimpleCache<TKey, TResource>())
        {
        }

        public SharedInformer(IInformer<TKey, TResource> masterInformer, ILogger logger, ICache<TKey, TResource> cache, IScheduler scheduler = null)
        {
            _masterInformer = masterInformer;
            _cache = cache;
            _masterScheduler = scheduler ?? new EventLoopScheduler();
            _logger = logger;
            // _keySelector = keySelector;
            _masterObservable = masterInformer
                .ListWatch()
                .ObserveOn(_masterScheduler)
                .Do(x => _logger.LogTrace($"Received message from upstream {x}"))
                .Into(_cache)
                .Do(msg =>
                {
                    // cache is synchronized as soon as we get at least one message past this point
                    _logger.LogTrace($"Cache v{cache.Version} synchronized: {msg} ");
                    _cacheSynchronized.TrySetResult(true);
                    _logger.LogTrace("_cacheSynchronized.TrySetResult(true)");
                })
                .Do(_ => YieldToWaitingSubscribers())
                .ObserveOn(Scheduler.Immediate) // immediate ensures that all caches operations are done atomically
                .ObserveOn(_masterScheduler)
                .Catch<CacheSynchronized<ResourceEvent<TKey, TResource>>, Exception>(e =>
                {
                    _cacheSynchronized.TrySetException(e);
                    // _cacheSynchronized.OnError(e);
                    return Observable.Throw<CacheSynchronized<ResourceEvent<TKey, TResource>>>(e);
                })
                .Finally(() => _cacheSynchronized.TrySetResult(false))
                // .SubscribeOn(_masterScheduler)
                .Publish();
        }


        public async IAsyncEnumerable<TResource> List([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // if there's already a live query running against server (due to being non-first subscriber),
            // it's cheaper to list out of cache since we're probably already have it in memory (or in process of filling it)
            if (_subscribers > 0) 
            {
                var subscription = ListWatch().Subscribe();
                await _cacheSynchronized.Task;
                var list = _cache.Snapshot();
                subscription.Dispose();
                foreach (var item in list.Values)
                {
                    yield return item;
                }
            }

            await foreach (var item in _masterInformer.List(cancellationToken))
            {
                yield return item;
            }
        }

        public IObservable<ResourceEvent<TKey, TResource>> ListWatch() =>
            Observable.Using(() => new EventLoopScheduler(), childScheduler =>
               Observable.Defer(async () =>
               {
                   AddSubscriber();
                   _logger.LogTrace("Subscriber awaiting cache synchronization before attaching");

                   var isCacheSynchronized = await _cacheSynchronized.Task.ConfigureAwait(false);
                   if (!isCacheSynchronized) // really this only happens if the reset is the master completes before first reset, in which case the downstream subscriber gets nothing
                   {
                       return Observable.Empty<ResourceEvent<TKey, TResource>>();
                   }
                   // we use lock to pause any processing of the broadcaster while we're attaching to the stream so proper alignment can be made

                   _logger.LogTrace("Subscriber attaching to broadcaster");

                   return Observable.Create<ResourceEvent<TKey, TResource>>(observer =>
                       {
                           var broadcasterAttachment = Disposable.Empty;
                           var cacheVersion = _cache.Version;
                            _logger.LogTrace($"Flushing contents of cache version {cacheVersion}");
                            foreach (var val in _cache.ToReset(true))
                            {
                                observer.OnNext(val);
                            }

                           broadcasterAttachment = _masterObservable
                               // we could be ahead of broadcaster because we initialized from cache which gets updated before the message are sent to broadcaster
                               // this logic realigns us at the correct point with the broadcaster
                               .Do(x => _logger.LogTrace($"Received from broadcaster {x}"))
                               .SkipWhile(x => x.MessageNumber <= cacheVersion)
                               .Select(x => x.Value)
                               .Do(x => _logger.LogTrace($"Aligned with broadcaster {x}"))
                               .SubscribeOn(_masterScheduler)
                               .ObserveOn(childScheduler)
                               .Subscribe(observer, () =>
                               {
                                   _logger.LogTrace("Child OnComplete");
                                   RemoveSubscriber();
                               });

                           // let broadcaster know we're done attaching to stream so it can resume it's regular work
                           _logger.LogTrace("Finished attaching to stream - signalling to resume");
                           lock (_lock)
                           {
                               _waitingSubscribers.Signal();
                           }

                           return broadcasterAttachment;
                       })
                       .ObserveOn(childScheduler)
                       .SubscribeOn(childScheduler);
               })
               .SubscribeOn(childScheduler) // ensures that when we attach master observer it's done on child thread, as we plan on awaiting cache synchronization
               .Do(_ => _logger.LogTrace($"Shared informer out: {_}")));



        [DebuggerStepThrough]
        private void YieldToWaitingSubscribers()
        {
            _logger.LogTrace("Waiting for subscribers to attach to stream");
            while (_waitingSubscribers.CurrentCount > 0)
            {
                // give a chance to any joining subscribers to realign with the broadcast stream
                _waitingSubscribers.Wait(100);
            }

            _logger.LogTrace("Finished yielding to subscribers");
        }

        private void AddSubscriber()
        {
            // when child subscribers attach they need to be synchronized to the master stream
            // this is allowed outside of "reset" event boundary.
            // the broadcaster will yield to any _waitingSubscribers before resuming work
            var shouldConnectMaster = false;
            lock (_lock)
            {
                // need to do this under lock because we can't just increment if the lock is already set, and there's a
                // risk of collision of two threads resetting to 1 at the same time
                if (!_waitingSubscribers.TryAddCount())
                {
                    _waitingSubscribers.Reset(1);
                }

                if (_subscribers == 0)
                {
                    shouldConnectMaster = true;
                }
                _subscribers++;
            }

            if (shouldConnectMaster)
            {
                _masterSubscription = _masterObservable.Connect();
            }
        }

        private void RemoveSubscriber()
        {
            try
            {
                _logger.LogTrace("Removing Subscriber!");
            }
            catch (Exception) // given the use of Observable.Using, in unit tests this may actually get called AFTER unit test completes when logger is already gone
            {
            }

            lock (_lock)
            {
                _subscribers--;
                if (_subscribers == 0)
                {
                    _cacheSynchronized = new TaskCompletionSource<bool>(false);
                    _masterSubscription.Dispose();
                }
            }
        }
    }
}
