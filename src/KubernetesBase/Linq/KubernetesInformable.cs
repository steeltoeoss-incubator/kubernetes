// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reactive.Disposables;
// using System.Reactive.Linq;
// using System.Threading;
// using k8s;
// using k8s.Models;
// using Steeltoe.Informers.InformersBase;
// using Steeltoe.Informers.InformersBase.Cache;
//
// namespace Steeltoe.Informers.KubernetesBase.Linq
// {
//     public partial class KubernetesInformable
//     {
//         
//         /// <summary>
//         /// Joins informer stream with another which yields values where resources in current informer are owned by values in the other.
//         /// In essence this is an "inner join" on owner reference 
//         /// </summary>
//         /// <param name="sourceStream"></param>
//         /// <param name="ownerStream"></param>
//         /// <param name="selector"></param>
//         /// <typeparam name="TSource"></typeparam>
//         /// <typeparam name="TOther"></typeparam>
//         /// <typeparam name="TResult"></typeparam>
//         /// <returns></returns>
//         public static IObservable<ResourceEvent<Tuple<string, string>, TResult>> JoinOwner<TSource, TOther, TResult>(
// 	        this IObservable<ResourceEvent<string, TSource>> sourceStream,
// 	        IObservable<ResourceEvent<string, TOther>> ownerStream,
// 	        Func<TSource, TOther, TResult> selector)
// 	        where TSource : IKubernetesObject<V1ObjectMeta>
// 	        where TOther : IKubernetesObject<V1ObjectMeta>
//         {
// 	        return Observable.Create<ResourceEvent<Tuple<string, string>, TResult>>(observer =>
// 	        {
// 		        var merged = sourceStream // sequences need to be merged into single stream to ensure synchronized subscription to both
// 			        .Select(x => (object) x)
// 			        .Merge(ownerStream.Select(x => (object) x))
// 			        .Publish();
// 		        
// 		        var sourceStreamConnected = merged.Where(x => x is ResourceEvent<string, TSource>).Select(x => (ResourceEvent<string, TSource>)x);
// 		        var ownerStreamConnected = merged.Where(x => x is ResourceEvent<string, TOther>).Select(x => (ResourceEvent<string, TOther>)x);
// 		        var subscription = new CompositeDisposable();
// 		        var children = new SimpleCache<string, TSource>();
// 		        var owners = new SimpleCache<string, TOther>();
// 		        var resultPairs = new HashSet<Tuple<string, string>>();
// 		        var childrenByOwnerName = new Dictionary<string, HashSet<string>>();
// 		        var isSourceSynchronized = false;
// 		        var isOwnerSynchronized = false;
// 		        var isSynchronized = false;
// 				var semaphore = new SemaphoreSlim(1);
// 		        sourceStreamConnected
// 			        .Do(x => semaphore.Wait())
// 			        .AsInformable()
// 			        .Into(children)
// 			        .Select(x => x.Value)
// 			        .Do(childItem =>
// 			        {
// 				        isSourceSynchronized = true;
// 				        if (!isOwnerSynchronized)
// 					        return;
// 				        if (!isSynchronized)
// 				        {
// 					        InitialJoin();
// 					        return;
// 				        }
//
// 				        IEnumerable<string> addedReferences = Enumerable.Empty<string>();
// 				        IEnumerable<string> removedReferences = Enumerable.Empty<string>();
// 				        IEnumerable<string> modifiedReferences = Enumerable.Empty<string>();
// 				        if (childItem.EventFlags.HasFlag(EventTypeFlags.Add))
// 				        {
// 					        addedReferences = childItem.Value.OwnerReferencesSafe().Select(x => x.Name);
// 				        }
// 				        else if (childItem.EventFlags.HasFlag(EventTypeFlags.Modify))
// 				        {
// 					        addedReferences = childItem.Value.OwnerReferencesSafe().Select(x => x.Name).Except(childItem.OldValue.OwnerReferencesSafe().Select(x => x.Name));
// 					        removedReferences = childItem.OldValue.OwnerReferencesSafe().Select(x => x.Name).Except(childItem.Value.OwnerReferencesSafe().Select(x => x.Name));
// 					        modifiedReferences = childItem.Value.OwnerReferencesSafe().Join(childItem.OldValue.OwnerReferencesSafe(), x => x.Name, x => x.Name, (value, oldValue) => value).Select(x => x.Name);
// 				        }
// 				        else if (childItem.EventFlags.HasFlag(EventTypeFlags.Delete))
// 				        {
// 					        removedReferences = childItem.Value.OwnerReferencesSafe().Select(x => x.Name);
// 				        }
// 						
// 				        void Notify(string reference, EventTypeFlags type)
// 				        {
// 					        if (!owners.TryGetValue(reference, out var owner)) return;
// 					        var pairKey = Tuple.Create(childItem.Key, reference);
// 					        var value = selector(childItem.Value, owner);
// 					        var oldValue = childItem.OldValue != null ? selector(childItem.OldValue, owner) : default;
// 					        if (type.HasFlag(EventTypeFlags.Add) && !resultPairs.Contains(pairKey))
// 					        {
// 						        observer.OnNext(value.ToResourceEvent(EventTypeFlags.Add, pairKey, oldValue));
// 						        resultPairs.Add(pairKey);
// 					        }
// 					        if (type.HasFlag(EventTypeFlags.Delete) && resultPairs.Contains(pairKey))
// 					        {
// 						        observer.OnNext(value.ToResourceEvent(EventTypeFlags.Delete, pairKey, oldValue));
// 						        resultPairs.Remove(pairKey);
// 					        }
//
// 					        if (type.HasFlag(EventTypeFlags.Modify) && resultPairs.Contains(pairKey))
// 					        {
// 						        observer.OnNext(value.ToResourceEvent(EventTypeFlags.Modify, pairKey, oldValue));
// 					        }
// 				        }
//
// 				        foreach (var addedReference in addedReferences)
// 				        {
// 					        if (!childrenByOwnerName.TryGetValue(addedReference, out var childrenOfOwner))
// 					        {
// 						        childrenOfOwner = new HashSet<string>();
// 						        childrenByOwnerName[addedReference] = childrenOfOwner;
// 					        }
// 					        childrenOfOwner.Add(childItem.Key);
// 					        Notify(addedReference, EventTypeFlags.Add);
// 				        }
//
// 				        foreach (var modifiedReference in modifiedReferences)
// 				        {
// 					        Notify(modifiedReference, EventTypeFlags.Modify);
// 				        }
//
// 				        foreach (var deletedReference in removedReferences)
// 				        {
// 					        if (childrenByOwnerName.TryGetValue(deletedReference, out var childrenOfOwner))
// 					        {
// 						        childrenOfOwner.Remove(deletedReference);
// 						        if (childrenOfOwner.Count == 0)
// 						        {
// 							        childrenByOwnerName.Remove(deletedReference);
// 						        }
// 					        }
// 					        Notify(deletedReference, EventTypeFlags.Delete);
// 					        
// 				        }
// 				        //
// 				        // foreach (var ownerRefName in childItem.Value.OwnerReferencesSafe().Select(x => x.Name))
// 				        // {
// 					       //  if (!childrenByOwnerName.TryGetValue(ownerRefName, out var childrenOfOwner) && !childItem.EventFlags.HasFlag(EventTypeFlags.Delete))
// 					       //  {
// 						      //   childrenOfOwner = new HashSet<string>();
// 						      //   childrenByOwnerName[ownerRefName] = childrenOfOwner;
// 						      //   childrenOfOwner.Add(childItem.Key);
// 					       //  }
// 					       //  if (childItem.EventFlags.HasFlag(EventTypeFlags.Delete) && childrenOfOwner != null)
// 					       //  {
// 						      //   childrenOfOwner.Remove(childItem.Key);
// 						      //   if (childrenOfOwner.Count == 0)
// 						      //   {
// 							     //    childrenByOwnerName.Remove(ownerRefName);
// 						      //   }
// 					       //  }
// 				        //
// 					       //  if (owners.TryGetValue(ownerRefName, out var owner))
// 					       //  {
// 						      //   var pairKey = Tuple.Create(childItem.Key, ownerRefName);
// 						      //   if (childItem.EventFlags.HasFlag(EventTypeFlags.Add) && !resultPairs.Contains(pairKey))
// 						      //   {
// 							     //    observer.OnNext(new ResourceEvent<Tuple<string, string>, TResult>(EventTypeFlags.Add, pairKey, selector(childItem.Value, owner)));
// 							     //    resultPairs.Add(pairKey);
// 						      //   }
// 						      //   else if (childItem.EventFlags.HasFlag(EventTypeFlags.Delete))
// 						      //   {
// 							     //    resultPairs.Remove(pairKey);
// 							     //    observer.OnNext(new ResourceEvent<Tuple<string, string>, TResult>(EventTypeFlags.Delete, pairKey, selector(childItem.Value, owner), selector(childItem.Value, owner)));
// 						      //   }
// 						      //   else
// 						      //   {
// 				        //
// 							     //    observer.OnNext(new ResourceEvent<Tuple<string, string>, TResult>(EventTypeFlags.Modify, pairKey, selector(childItem.Value, owner)));
// 						      //   }
// 				        //
// 					       //  }
// 				        // }
// 			        })
// 			        .Do(_ => semaphore.Release())
// 			        // .ObserveOn(scheduler)
// 			        // .SubscribeOn(scheduler)
// 			        .Subscribe(x => { }, observer.OnError, observer.OnCompleted)
// 			        .DisposeWith(subscription);
//
// 		        ownerStreamConnected
// 			        .Do(x => semaphore.Wait())
// 			        .AsInformable()
// 			        .Into(owners)
// 			        .Select(x => x.Value)
// 			        .Do(ownerEvent =>
// 			        {
// 				        isOwnerSynchronized = true;
// 				        if (!isSourceSynchronized)
// 					        return;
// 				        if (!isSynchronized)
// 				        {
// 					        InitialJoin();
// 					        return;
// 				        }
//
// 				        if (childrenByOwnerName.TryGetValue(ownerEvent.Key, out var childrenOfCurrentOwner))
// 				        {
//
// 					        foreach (var child in childrenOfCurrentOwner)
// 					        {
// 						        var pairKey = Tuple.Create(child, ownerEvent.Key);
// 						        if (!ownerEvent.EventFlags.HasFlag(EventTypeFlags.Delete))
// 						        {
// 							        resultPairs.Add(pairKey);
// 						        }
// 						        else
// 						        {
// 							        resultPairs.Remove(pairKey);
// 						        }
//
// 						        var value = selector(children[child], ownerEvent.Value);
// 						        
// 						        TResult oldValue = ownerEvent.OldValue != null ? selector(children[child], ownerEvent.OldValue) : default;
// 						        observer.OnNext(new ResourceEvent<Tuple<string, string>, TResult>(ownerEvent.EventFlags, pairKey, value, oldValue));
// 					        }
// 				        }
// 			        })
// 			        .Do(_ => semaphore.Release())
// 			        // .ObserveOn(scheduler)
// 			        // .SubscribeOn(scheduler)
// 			        .Subscribe(x => { }, observer.OnError, observer.OnCompleted)
// 			        .DisposeWith(subscription);
// 		        
// 		        merged.Connect().DisposeWith(subscription);
//
// 		        void InitialJoin()
// 		        {
// 			        childrenByOwnerName = children.Values
// 				        .SelectMany(x => x.OwnerReferencesSafe().Select(o => new {Owner = o.Name, Child = x}))
// 				        .Where(x => x != null)
// 				        .GroupBy(x => x.Owner)
// 				        .ToDictionary(x => x.Key, x => x.Select(y => y.Child.Metadata.Name).ToHashSet());
// 			        var results = new Dictionary<Tuple<string, string>, TResult>();
// 			        foreach (var child in children)
// 			        {
// 				        var ownerNames = child.Value.OwnerReferencesSafe().Select(o => o.Name);
// 				        foreach (var ownerName in ownerNames)
// 				        {
// 					        if (owners.TryGetValue(ownerName, out var owner))
// 					        {
// 						        var pairKey = Tuple.Create(child.Key, ownerName);
// 						        results[pairKey] = selector(child.Value, owner);
// 					        }
// 				        }
// 			        }
//
// 			        resultPairs = results.Keys.ToHashSet();
// 			        foreach (var item in results.ToReset(true))
// 			        {
// 				        observer.OnNext(item);
// 			        }
//
// 			        isSynchronized = true;
// 		        }
//
// 		        return subscription;
// 	        });
//         }
//
//         public static IEnumerable<V1OwnerReference> OwnerReferencesSafe(this IKubernetesObject<V1ObjectMeta> source)
//         {
// 	        return source?.Metadata?.OwnerReferences ?? Enumerable.Empty<V1OwnerReference>();
//         }
//     
//     }
// }