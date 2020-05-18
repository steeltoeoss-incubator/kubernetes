using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Steeltoe.Informers.InformersBase.Cache;

namespace Steeltoe.Informers.InformersBase
{
    public static class ResourceObservableExtensions
    {


        public static IObservable<ResourceEvent<TKey, TResource>> WithResets<TKey, TResource>(this IObservable<ResourceEvent<TKey, TResource>> source, Func<List<ResourceEvent<TKey, TResource>>, IEnumerable<ResourceEvent<TKey, TResource>>> action)
        {
            return source.WithResets(action, e => e);
        }

        public static IObservable<TOut> WithResets<TKey, TResource, TOut>(this IObservable<ResourceEvent<TKey, TResource>> source, Func<List<ResourceEvent<TKey, TResource>>, IEnumerable<ResourceEvent<TKey, TResource>>> resetSelector, Func<ResourceEvent<TKey, TResource>, TOut> itemSelector)
        {
            return Observable.Create<TOut>(observer =>
                {
                    var resetBuffer = new List<ResourceEvent<TKey, TResource>>();

                    void FlushBuffer()
                    {
                        if (!resetBuffer.Any())
                        {
                            return;
                        }

                        foreach (var item in resetSelector(resetBuffer))
                        {
                            observer.OnNext(itemSelector(item));
                        }
                        resetBuffer.Clear();
                    }

                    void OnComplete()
                    {
                        FlushBuffer();
                        observer.OnCompleted();
                    }

                    void OnError(Exception e)
                    {
                        observer.OnError(e);
                    }

                    var upstreamSubscription = source
                        .Subscribe(notification =>
                        {
                            if (notification.EventFlags.HasFlag(EventTypeFlags.Reset))
                            {
                                resetBuffer.Add(notification);
                                if (!notification.EventFlags.HasFlag(EventTypeFlags.ResetEnd)) // continue buffering till we reach the end of list window
                                {
                                    return;
                                }
                            }

                            if (notification.EventFlags.HasFlag(EventTypeFlags.ResetEnd))
                            {
                                FlushBuffer();
                                return;
                            }

                            if (!notification.EventFlags.HasFlag(EventTypeFlags.Reset) && resetBuffer.Count > 0)
                            {
                                FlushBuffer();
                            }

                            observer.OnNext(itemSelector(notification));
                        }, OnError, OnComplete);
                    return StableCompositeDisposable.Create(upstreamSubscription, Disposable.Create(OnComplete));
                })
                .ObserveOn(Scheduler.Immediate);
        }

        /// <summary>
        ///     Synchronizes the specified cache with resource event stream such that cache is maintained up to date.
        /// </summary>
        /// <param name="source">The source sequence</param>
        /// <param name="cache">The cache to synchronize</param>
        /// <param name="keySelector">The key selector function</param>
        /// <typeparam name="TKey">The type of key</typeparam>
        /// <typeparam name="TResource">The type of resource</typeparam>
        /// <returns>Source sequence wrapped into <see cref="CacheSynchronized{T}" />, which allows downstream consumers to synchronize themselves with cache version</returns>
        public static IObservable<CacheSynchronized<ResourceEvent<TKey, TResource>>> Into<TKey, TResource>(
            this IObservable<ResourceEvent<TKey, TResource>> source,
            ICache<TKey, TResource> cache,
            Func<TResource, TResource, TResource> mapper = null)
        {
            if (mapper == null)
            {
                mapper = (oldResource, newResource) => newResource;
            }

            return Observable.Defer(() =>
            {
                long msgNum = 0;

                return source
                    .Do(_ => msgNum++)
                    .WithResets(events =>
                    {
                        var reset = events
                            .Where(x => x.Key != null && x.Value != null)
                            .ToDictionary(x => x.Key, x => x.Value);

                        cache.Reset(reset);
                        cache.Version += events.Count;
                        return events;
                    }, notification =>
                    {
                        if (notification.EventFlags.HasFlag(EventTypeFlags.Reset))
                        {
                        }
                        else if (!notification.EventFlags.HasFlag(EventTypeFlags.Delete))
                        {
                            cache.Version++;
                            if (notification.Value != null)
                            {
                                var key = notification.Key;
                                if (cache.TryGetValue(key, out var existing))
                                {
                                    notification = new ResourceEvent<TKey, TResource>(notification.EventFlags, notification.Key, mapper(existing, notification.Value), existing);
                                }
                                cache[notification.Key] = notification.Value;
                            }
                        }
                        else
                        {
                            cache.Remove(notification.Key);
                        }

                        return new CacheSynchronized<ResourceEvent<TKey, TResource>>(msgNum, cache.Version, notification);
                    });
            });
        }


        public static IObservable<ResourceEvent<TKey, TResource>> ComputeMissedEventsBetweenResets<TKey, TResource>(this IObservable<ResourceEvent<TKey, TResource>> source, IEqualityComparer<TResource> comparer)
        {
            return Observable.Create<ResourceEvent<TKey, TResource>>(observer =>
            {
                var cache = new SimpleCache<TKey, TResource>();
                var cacheSynchronized = false;
                return source
                    .WithResets(resetBuffer =>
                    {
                        if (!cacheSynchronized)
                        {
                            return resetBuffer;
                        }

                        var cacheSnapshot = cache.Snapshot();
                        
                        var newKeys = resetBuffer
                            .Where(x => x.Key != null)
                            .Select(x => x.Key)
                            .ToHashSet();

                        var addedEntities = resetBuffer
                            .Where(x => x.Key != null && !cacheSnapshot.ContainsKey(x.Key))
                            .Select(x => x.ToResourceEvent(EventTypeFlags.Add | EventTypeFlags.Computed))
                            .ToList();
                        
                        var addedKeys = addedEntities
                            .Select(x => x.Key)
                            .ToHashSet();

                        var deletedEntities = cacheSnapshot
                            .Where(x => !newKeys.Contains(x.Key))
                            .Select(x => x.Value.ToResourceEvent(EventTypeFlags.Delete | EventTypeFlags.Computed, x.Key))
                            .ToList();
                        var deletedKeys = deletedEntities
                            .Select(x => x.Key)
                            .ToHashSet();

                        // we can only compute updates if we are given a proper comparer to determine equality between objects
                        // if not provided, will be sent downstream as just part of reset
                        var updatedEntities = new List<ResourceEvent<TKey, TResource>>();
                        if (comparer != null)
                        {
                            updatedEntities = resetBuffer
                                .Where(x => cacheSnapshot.TryGetValue(x.Key, out var resource) && !comparer.Equals(resource, x.Value))
                                .Select(x => x.ToResourceEvent(EventTypeFlags.Modify | EventTypeFlags.Computed))
                                .ToList();
                        }

                        var updatedKeys = updatedEntities
                            .Select(x => x.Key)
                            .ToHashSet();

                        var resetEntities = resetBuffer
                            .Where(x => x.Key != null &&
                                        !addedKeys.Contains(x.Key) &&
                                        !deletedKeys.Contains(x.Key) &&
                                        !updatedKeys.Contains(x.Key))
                            .ToReset()
                            .ToList();

                        return deletedEntities
                            .Union(addedEntities)
                            .Union(updatedEntities)
                            .Union(resetEntities);
                    })
                    .Into(cache)
                    .Do(msg => { cacheSynchronized = true; })
                    .Select(x => x.Value)
                    .ObserveOn(Scheduler.Immediate)
                    .Subscribe(observer);
            });
        }

        /// <summary>
        ///     Injects a <see cref="ResourceEvent{TKey, TResource}" /> of type <see cref="EventTypeFlags.Sync" /> into the observable for each item produced
        ///     by the <see cref="ResourceStreamType.List" /> operation from <paramref name="source" />
        /// </summary>
        /// <param name="source">The source sequence that will have sync messages appended</param>
        /// <param name="timeSpan">The timespan interval at which the messages should be produced</param>
        /// <typeparam name="TResource">The type of resource</typeparam>
        /// <returns>Original sequence with resync applied</returns>
        public static IObservable<ResourceEvent<TKey, TResource>> Resync<TKey, TResource>(this IObservable<ResourceEvent<TKey, TResource>> source, TimeSpan timeSpan, IScheduler scheduler = null)
        {
            scheduler ??= DefaultScheduler.Instance;
            return Observable.Create<ResourceEvent<TKey, TResource>>(observer =>
            {
                var timerSubscription = Observable
                    .Interval(timeSpan, scheduler)
                    .SelectMany(_ => source
                        .TakeUntil(x => x.EventFlags.HasFlag(EventTypeFlags.ResetEnd))
                        .Do(x =>
                        {
                            if (!x.EventFlags.HasFlag(EventTypeFlags.Reset))
                            {
                                throw new InvalidOperationException("Resync was applied to an observable sequence that does not issue a valid List event block when subscribed to");
                            }
                        })
                        .Select(x => x.Value.ToResourceEvent(EventTypeFlags.Sync, x.Key)))
                    .Subscribe(observer);
                // this ensures that both timer and upstream subscription is closed when subscriber disconnects
                var sourceSubscription = source.Subscribe(
                    observer.OnNext,
                    observer.OnError,
                    () =>
                    {
                        observer.OnCompleted();
                        timerSubscription.Dispose();
                    });
                return StableCompositeDisposable.Create(timerSubscription, sourceSubscription);
            });
        }

        /// <summary>
        ///     Wraps an instance of <see cref="IInformer{TResource,TOptions}" /> as <see cref="IInformer{TResource}" /> by using the same
        ///     set of <see cref="TOptions" /> for every subscription
        /// </summary>
        /// <param name="optionedInformer">The original instance of <see cref="IInformer{TResource,TOptions}" /></param>
        /// <param name="options">The options to use</param>
        /// <typeparam name="TResource">The type of resource</typeparam>
        /// <typeparam name="TOptions"></typeparam>
        /// <returns></returns>
        public static IInformer<TKey, TResource> WithOptions<TKey, TResource, TOptions>(this IInformer<TKey, TResource, TOptions> optionedInformer, TOptions options) =>
            new WrappedOptionsInformer<TKey, TResource, TOptions>(optionedInformer, options);

        private class WrappedOptionsInformer<TKey, TResource, TOptions> : IInformer<TKey, TResource>
        {
            private readonly IInformer<TKey, TResource, TOptions> _informer;
            private readonly TOptions _options;

            public WrappedOptionsInformer(IInformer<TKey, TResource, TOptions> informer, TOptions options)
            {
                _informer = informer;
                _options = options;
            }

            // public IObservable<ResourceEvent<TResource>> GetResource(ResourceStreamType type) => _informer.GetResource(type, _options);
            public IAsyncEnumerable<TResource> List(CancellationToken cancellationToken) => _informer.List(_options, cancellationToken);

            public IObservable<ResourceEvent<TKey, TResource>> ListWatch() => _informer.ListWatch(_options);
        }
    }
}
