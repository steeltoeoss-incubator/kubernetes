using System;
using System.Collections.Generic;
using System.Reactive.Disposables;

namespace Steeltoe.Informers.InformersBase
{
    public static class InformerExtensions
    {
        /// <summary>
        ///     Removes an item from the dictionary
        /// </summary>
        /// <param name="source">The source dictionary</param>
        /// <param name="key">The key for which item should be removed</param>
        /// <param name="result">The value of the object that was removed, or <see langword="null" /> if value was not present in dictionary</param>
        /// <typeparam name="TKey">The type of key</typeparam>
        /// <typeparam name="TValue">The type of value</typeparam>
        /// <returns><see langword="true" /> if the object was removed from dictionry, or <see langword="false" /> if the specific key was not present in dictionary</returns>
        internal static bool Remove<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key, out TValue result)
        {
            result = default;
            if (!source.TryGetValue(key, out result))
            {
                return false;
            }

            source.Remove(key);
            return true;
        }


        /// <summary>
        ///     Creates a <see cref="HashSet{T}" /> for <see cref="IEnumerable{T}" />
        /// </summary>
        /// <param name="source">The source enumerable</param>
        /// <typeparam name="T">The type of elements</typeparam>
        /// <returns>The produced hashset</returns>
        internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return source.ToHashSet(null);
        }

        /// <summary>
        ///     Creates a <see cref="HashSet{T}" /> for <see cref="IEnumerable{T}" />
        /// </summary>
        /// <param name="source">The source enumerable</param>
        /// <param name="comparer">The comparer to use</param>
        /// <typeparam name="T">The type of elements</typeparam>
        /// <returns>The produced hashset</returns>
        internal static HashSet<T> ToHashSet<T>(
            this IEnumerable<T> source,
            IEqualityComparer<T> comparer)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return new HashSet<T>(source, comparer);
        }


        /// <summary>
        ///     Attaches the source <see cref="IDisposable" /> to the target <see cref="CompositeDisposable" />
        /// </summary>
        /// <param name="source">The original <see cref="IDisposable" /></param>
        /// <param name="composite">The <see cref="CompositeDisposable" /> to attach to</param>
        /// <returns>The original disposable passed as <paramref name="source" /> </returns>
        public static IDisposable DisposeWith(this IDisposable source, CompositeDisposable composite)
        {
            composite.Add(source);
            return composite;
        }

        /// <summary>
        ///     Combines the source disposable with another into a single disposable
        /// </summary>
        /// <param name="source">The original <see cref="IDisposable" /></param>
        /// <param name="composite">The <see cref="IDisposable" /> to combine with</param>
        /// <returns>Composite disposable made up of <paramref name="source" /> and <paramref name="other" /> </returns>
        public static IDisposable CombineWith(this IDisposable source, IDisposable other)
        {
            return new CompositeDisposable(source, other);
        }

        public static IDisposable Subscribe<T>(this IObservable<T> source, IObserver<T> observer, Action onFinished = null)
        {
            return source.Subscribe(observer, _ => { }, x => onFinished(), onFinished);
        }

        public static IDisposable Subscribe<T>(this IObservable<T> source, IObserver<T> observer, Action<T> onNext = null, Action<Exception> onError = null, Action onCompleted = null)
        {
            onNext ??= obj => { };
            onError ??= obj => { };
            onCompleted ??= () => { };
            return source.Subscribe(x =>
                {
                    observer.OnNext(x);
                    onNext(x);
                },
                error =>
                {
                    observer.OnError(error);
                    onError(error);
                },
                () =>
                {
                    observer.OnCompleted();
                    onCompleted();
                });
        }

#pragma warning disable 1998
        internal static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> source)
#pragma warning restore 1998
        {
            foreach (var item in source)
            {
                yield return item;
            }
        }

    }
}
