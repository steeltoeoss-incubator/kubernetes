using System.Collections.Generic;
using System.Linq;
using Steeltoe.Informers.InformersBase;
using Steeltoe.Informers.InformersBase.Cache;

namespace System.Linq
{
    public static partial class Informable
    {
        public static IInformable<TKey, TResource> ComputeMissedEventsBetweenResets<TKey, TResource>(this IInformable<TKey, TResource> source, IEqualityComparer<TResource> comparer = null)
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
                        .Select(x => x.With(EventTypeFlags.Add | EventTypeFlags.Computed))
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
                            .Select(x => x.With(EventTypeFlags.Modify | EventTypeFlags.Computed))
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
                .Do(x => { cacheSynchronized = true; });
        }
    }
}