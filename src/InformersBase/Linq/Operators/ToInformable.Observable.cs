﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

 using System;
 using System.Collections.Concurrent;
using System.Collections.Generic;
 using System.Diagnostics;
 using System.Linq;
 using System.Threading;
using System.Threading.Tasks;
using Steeltoe.Informers.InformersBase;

 namespace Steeltoe.Informers.InformersBase
{
    public static partial class Informable
    {
        
        /// <summary>
        /// Converts an observable sequence to an async-enumerable sequence.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements in the source sequence.</typeparam>
        /// <param name="source">Observable sequence to convert to an async-enumerable sequence.</param>
        /// <returns>The async-enumerable sequence whose elements are pulled from the given observable sequence.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        public static IInformable<TKey, TSource> ToInformable<TKey, TSource>(this IObservable<ResourceEvent<TKey, TSource>> source)
        {
            if (source == null)
                throw Error.ArgumentNull(nameof(source));

            return new InformableEnumerable<TKey, TSource>(source);
        }

        private class HashQueue<T>
        {
            private readonly Queue<T> _queue = new Queue<T>();
            private readonly HashSet<T> _set = new HashSet<T>();
            public T Dequeue()
            {
                var item = _queue.Dequeue();
                _set.Remove(item);
                return item;
            }

            public bool Enqueue(T item)
            {
                if (_set.Add(item))
                {
                    _queue.Enqueue(item);
                    return true;
                }

                return false;
            }

            public int Count => _queue.Count;
            public void Clear()
            {
                _set.Clear();
                _queue.Clear();
            }
        }
        private sealed class InformableEnumerable<TKey, TSource> : AsyncIterator<ResourceEvent<TKey, TSource>>, IObserver<ResourceEvent<TKey, TSource>>, IInformable<TKey, TSource>
        {
            private class StateTracker
            {
                public bool HasSent { get; set; }
                private ResourceEvent<TKey, TSource> _lastSent;

                public ResourceEvent<TKey, TSource> LastSent
                {
                    get => _lastSent;
                    set
                    {
                        _lastSent = value;
                        HasSent = true;
                    }
                }

                public ResourceEvent<TKey, TSource> LastReceived { get; set; }

                
            }
            
            private readonly IObservable<ResourceEvent<TKey, TSource>> _source;

            private HashQueue<TKey> _changedItems = new HashQueue<TKey>();
            
            // private Queue<TKey> _resetQueue = new Queue<TKey>();
            // private HashSet<TKey> _sentResetKeys = new HashSet<TKey>();
            private Dictionary<TKey, StateTracker> _knownItems = new Dictionary<TKey, StateTracker>();
            private readonly object _lock = new object();

            private Exception? _error;
            private bool _completed;
            private TaskCompletionSource<bool>? _signal;
            private IDisposable? _subscription;
            private CancellationTokenRegistration _ctr;
            private ResetExtractor<TKey, TSource> _resetExtractor = new ResetExtractor<TKey, TSource>();
            /// <summary>
            /// true if there was at least one completed reset from upstream
            /// </summary>
            private bool _isSynchronized;
            /// <summary>
            /// number of items needed to be taken off the queue before we reach end of reset window
            /// </summary>
            private int _pendingResetItems = 0;
            
            // private bool _isResetting;

            public InformableEnumerable(IObservable<ResourceEvent<TKey, TSource>> source) => _source = source;

            public override AsyncIteratorBase<ResourceEvent<TKey, TSource>> Clone() => new InformableEnumerable<TKey, TSource>(_source);

            public override ValueTask DisposeAsync()
            {
                Dispose();

                return base.DisposeAsync();
            }

            private bool CheckIsCompleted()
            {
                if (!_completed) return false;
                var error = _error;

                if (error != null)
                {
                    throw error;
                }

                return true;

            }

            protected override async ValueTask<bool> MoveNextCore()
            {
               
                _cancellationToken.ThrowIfCancellationRequested();

                bool TryGetNextQeueuedResourceEvent(out StateTracker stateTracker)
                {
                    if (_changedItems.Count > 0)
                    {
                        var key = _changedItems.Dequeue();
                        if (_knownItems.TryGetValue(key, out stateTracker))
                        {
                            return true;
                        }
                    }
                
                    stateTracker = default;
                    return false;
                }


                EventTypeFlags flags = 0;
                switch (_state)
                {
                    case InformableIteratorState.Allocated:
                        lock (_lock)
                        {
                            _subscription = _source.Subscribe(this);
                            foreach (var key in _knownItems.Keys.ToList())
                            {
                                _changedItems.Enqueue(key);
                                _pendingResetItems++;
                            }

                            _ctr = _cancellationToken.Register(OnCanceled, state: null);
                            _state = InformableIteratorState.ResetStart;
                        }

                        goto case InformableIteratorState.ResetStart;
                    case InformableIteratorState.ResetStart:
                        flags = EventTypeFlags.ResetStart;
                        _state = InformableIteratorState.Reset;
                        goto case InformableIteratorState.Reset;
                    case InformableIteratorState.Reset:
                        flags |= EventTypeFlags.Reset;
                        while (true)
                        {
                            lock (_lock)
                            {
                                if (_isSynchronized && _knownItems.Count == 0)
                                {
                                    _current = ResourceEvent<TKey, TSource>.EmptyReset;
                                    _state = InformableIteratorState.Watching;
                                    return true;
                                }
                                
                                if (TryGetNextQeueuedResourceEvent(out var state))
                                {
                                    _pendingResetItems--;
                                    if (state.LastReceived.EventFlags == EventTypeFlags.Delete)
                                    {
                                        _knownItems.Remove(state.LastReceived.Key);
                                        continue;
                                    }
                                    // we're at the end of the reset if we're not expecting reset from upstream
                                    var isResetEnd = !_resetExtractor.IsResetting && _pendingResetItems == 0;
                                    if (isResetEnd)
                                    {
                                        flags |= EventTypeFlags.ResetEnd;
                                        _state = InformableIteratorState.Watching;
                                    }

                                    state.LastSent = state.LastReceived.With(flags);
                                    _current = state.LastSent;
                                    return true;
                                }

                                if (CheckIsCompleted())
                                    return false;
                                _signal ??= new TaskCompletionSource<bool>();
                            }

                            await _signal.Task.ConfigureAwait(false);;
                            lock (_lock)
                            {
                                _signal = null;
                            }
                        }
                    case InformableIteratorState.Watching:
                        while (true)
                        {
                            lock (_lock)
                            {
                              
                                if (TryGetNextQeueuedResourceEvent(out var state))
                                {
                                    if (state.LastReceived.EventFlags.HasFlag(EventTypeFlags.Delete))
                                    {
                                        if (!state.HasSent)
                                        {
                                            _knownItems.Remove(state.LastReceived.Key);
                                            continue;
                                        }
                                        flags = EventTypeFlags.Delete;
                                    }
                                    else if (EqualityComparer<TSource>.Default.Equals(state.LastSent.Value, state.LastReceived.Value))
                                    {
                                        continue;
                                    }
                                    else if (!state.HasSent)
                                    {
                                        flags = EventTypeFlags.Add;
                                    }
                                    else
                                    {
                                        flags = EventTypeFlags.Modify;
                                    }
                                    state.LastSent = state.LastReceived.With(flags);
                                    _current = state.LastSent;
                                    return true;
                                }
                                if (CheckIsCompleted())
                                    return false;
                                _signal ??= new TaskCompletionSource<bool>();
                            }

                            await _signal.Task.ConfigureAwait(false);;
                            lock (_lock)
                            {
                                _signal = null;
                            }
                        }
                }
                
                await DisposeAsync().ConfigureAwait(false);
                return false;
            }


            public void OnCompleted()
            {
                lock (_lock)
                {
                    _completed = true;
                    DisposeSubscriptionLocked();
                    OnNotificationLocked();
                }
            }

            public void OnError(Exception error)
            {
                lock (_lock)
                {
                    _error = error;
                    _completed = true;
                    DisposeSubscriptionLocked();
                    OnNotificationLocked();
                }
            }

            public void OnNext(ResourceEvent<TKey, TSource> value)
            {
                lock (_lock)
                {
                    if (_state == InformableIteratorState.Disposed)
                    {
                        return;
                    }
                    _resetExtractor.ApplyEvent(value, out _);
                    if (!_isSynchronized && !_resetExtractor.IsResetting)
                        _isSynchronized = true;

                    if (value.EventFlags.HasFlag(EventTypeFlags.ResetStart))
                    {
                        _pendingResetItems = 0;
                        if(!value.EventFlags.HasFlag(EventTypeFlags.ResetEnd))
                            _isSynchronized = false;
                        if(_state > InformableIteratorState.Allocated)
                            _state = InformableIteratorState.ResetStart;
                        _knownItems.Clear();
                        _changedItems.Clear();
                        if(value.EventFlags.HasFlag(EventTypeFlags.ResetEmpty))
                            return;
                    }
                    

                    if (!_knownItems.TryGetValue(value.Key, out var state))
                    {
                        state = new StateTracker();
                        _knownItems.Add(value.Key, state);
                    }
                    state.LastReceived = value;
                    var isScheduled = _changedItems.Enqueue(value.Key);
                    if(isScheduled && _state > InformableIteratorState.Allocated && value.EventFlags.HasFlag(EventTypeFlags.Reset))
                        _pendingResetItems++;
                    OnNotificationLocked();
                }
            }

            private void OnNotificationLocked()
            {
                if (_signal != null)
                    _signal.TrySetResult(true);
                else
                    _signal = TaskExt.True;
            }
            
            private void Dispose()
            {
                lock (_lock)
                {
                    _ctr.Dispose();
                    DisposeSubscriptionLocked();
                    _changedItems = null;
                    _error = null;
                    _knownItems = null;
                }
            }

            private void DisposeSubscriptionLocked()
            {
                _subscription?.Dispose();
                _subscription = null;
            }

            private void OnCanceled(object? state)
            {
                Dispose();
                if (_signal?.TrySetCanceled(_cancellationToken) ?? false)
                    return;
                _signal = new TaskCompletionSource<bool>();
                _signal.TrySetCanceled(_cancellationToken);
            }
        }
    }
}
