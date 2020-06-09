using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Steeltoe.Informers.InformersBase;

namespace System.Linq
{
    internal abstract class TransformingInformableIterator<TKey, TSource> : AsyncIterator<ResourceEvent<TKey, TSource>>, IInformable<TKey, TSource>
    {
        protected Dictionary<TKey, StateTracker<TKey, TSource>> _knownItems = new Dictionary<TKey, StateTracker<TKey, TSource>>();
        protected bool _isSynchronized;
        protected TaskCompletionSource<bool> _signal;

        protected readonly Informable.ResetExtractor<TKey, TSource> _resetExtractor = new Informable.ResetExtractor<TKey, TSource>();
        protected readonly object _lock = new object();
        protected bool _completed;
        protected Exception _error;
        protected CancellationTokenRegistration _ctr;

        protected bool AcceptNext(ResourceEvent<TKey, TSource> value, out StateTracker<TKey, TSource> stateTracker)
        {
            if (_state == InformableIteratorState.Disposed)
            {
                stateTracker = default;
                return false;
            }
            
            _resetExtractor.ApplyEvent(value, out _);
            if (!_isSynchronized && !_resetExtractor.IsResetting)
                _isSynchronized = true;

            if (value.EventFlags.HasFlag(EventTypeFlags.ResetStart))
            {
                if(!value.EventFlags.HasFlag(EventTypeFlags.ResetEnd))
                    _isSynchronized = false;
                if(_state > InformableIteratorState.Allocated)
                    _state = InformableIteratorState.ResetStart;
                _knownItems.Clear();
                if (value.EventFlags.HasFlag(EventTypeFlags.ResetEmpty))
                {
                    stateTracker = default;
                    return false;
                }
            }

            if (!_knownItems.TryGetValue(value.Key, out var state))
            {
                // there's an attempt to accept a delete for item we're not tracking - don't accept it
                if (value.EventFlags.HasFlag(EventTypeFlags.Delete))
                {
                    stateTracker = default;
                    return false;
                }

                state = new StateTracker<TKey, TSource>();
                _knownItems.Add(value.Key, state);
            }
            state.LastReceived = value;
            stateTracker = state;
            return true;
        }

        /// <summary>
        /// Attempts to set current to the specified item in the state tracker. This method sets the correct outgoing flags or skips sending entirely if downstream
        /// does not need to be notified of this state. 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        protected bool TrySetCurrent(StateTracker<TKey, TSource> state, EventTypeFlags flags = 0)
        {
            if (_state == InformableIteratorState.ResetStart)
            {
                flags |= EventTypeFlags.ResetStart;
            }

            //flags |= state.LastReceived.EventFlags;
            if (state.LastReceived.EventFlags.HasFlag(EventTypeFlags.Delete))
            {
                if (!state.HasSent)
                {
                    _knownItems.Remove(state.LastReceived.Key);
                    if (flags.HasFlag(EventTypeFlags.ResetEnd) && flags.HasFlag(EventTypeFlags.ResetStart))
                    {
                        _state = InformableIteratorState.Watching;
                        _current = ResourceEvent<TKey, TSource>.EmptyReset;
                        return true;
                    }
                    return false;
                }
            }
            
            if (_state == InformableIteratorState.ResetStart || _state == InformableIteratorState.Reseting)
            {
                // we're at the end of the reset if we're not expecting reset from upstream
                if (state.LastReceived.EventFlags.HasFlag(EventTypeFlags.ResetEnd))
                    flags |= EventTypeFlags.ResetEnd;
                if (flags.HasFlag(EventTypeFlags.ResetEnd))
                {
                    _state = InformableIteratorState.Watching;
                }
                else if (flags.HasFlag(EventTypeFlags.ResetStart))
                {
                    _state = InformableIteratorState.Reseting;
                }

                state.LastSent = state.LastReceived.With(flags);
                _current = state.LastSent;
                return true;
            }
            if (_state == InformableIteratorState.Watching)
            {
                if (state.LastReceived.EventFlags.HasFlag(EventTypeFlags.Delete))
                {
                    if (!state.HasSent)
                    {
                        _knownItems.Remove(state.LastReceived.Key);
                        return false;
                    }

                    flags = EventTypeFlags.Delete;
                }
                else if (EqualityComparer<TSource>.Default.Equals(state.LastSent.Value, state.LastReceived.Value))
                {
                    return false;
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

            return false;
        }

        // protected virtual void OnCanceled(object obj)
        // {
        // }

        // protected abstract bool IsCompleted();

       
    }
}