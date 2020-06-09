using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Steeltoe.Informers.InformersBase;

namespace System.Linq
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResource> Where<TKey, TResource>(this IInformable<TKey, TResource> source, Func<TResource, bool> predicate)
        {
            return new WhereInformable<TKey, TResource>(source, predicate);
        }
        internal sealed class WhereInformable<TKey, TSource> : TransformingInformableIterator<TKey, TSource>
        {
            private IInformable<TKey, TSource> _source;
            private readonly Func<TSource,bool> _predicate;
            private IAsyncEnumerator<ResourceEvent<TKey, TSource>> _enumerator;

            public WhereInformable(IInformable<TKey, TSource> source, Func<TSource,bool> predicate)
            {
                _source = source;
                _predicate = predicate;
            }
            public override AsyncIteratorBase<ResourceEvent<TKey, TSource>> Clone() => new WhereInformable<TKey, TSource>(this, _predicate);
            protected override async ValueTask<bool> MoveNextCore()
        {
               
                _cancellationToken.ThrowIfCancellationRequested();
                switch (_state)
                {
                    case InformableIteratorState.Allocated:
                        _state = InformableIteratorState.ResetStart;
                        _enumerator = _source.GetAsyncEnumerator();
                        goto case InformableIteratorState.Iterating;
                    case InformableIteratorState.ResetStart:
                    case InformableIteratorState.Reseting:
                    case InformableIteratorState.Watching:
                    case InformableIteratorState.Iterating:
                        while (await _enumerator.MoveNextAsync().ConfigureAwait(false))
                        {
                            
                            var item = _enumerator.Current;
                            if (!item.EventFlags.IsMeta() && !_predicate(item.Value))
                            {
                                // we might have yielded last item as regular reset, but all the items that followed were filtered out by the predicate
                                // it should have been a reset end but we didn't know what items were following it
                                // we need to issue an empty reset end message to mark end of reset window 
                                if (_state == InformableIteratorState.Reseting && item.EventFlags.HasFlag(EventTypeFlags.ResetEnd))
                                {
                                    _current = ResourceEvent<TKey, TSource>.ResetEnd;
                                    _state = InformableIteratorState.Watching;
                                    return true;
                                }
                                continue;
                            }

                            AcceptNext(item, out var state);
                            if(TrySetCurrent(state))
                            {
                                return true;
                            }
                        }

                        break;
                }
                
                await DisposeAsync().ConfigureAwait(false);
                return false;
            }
        }
    }
}