﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT License.
// See the LICENSE file in the project root for more information. 

 using System;
 using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
 using Steeltoe.Informers.InformersBase.Cache;

 // Note: The type here has to be internal as System.Linq has its own public copy we're not using.

namespace Steeltoe.Informers.InformersBase.Internal
{
    /// Adapted from System.Linq.Grouping from .NET Framework
    /// Source: https://github.com/dotnet/corefx/blob/b90532bc97b07234a7d18073819d019645285f1c/src/System.Linq/src/System/Linq/Grouping.cs#L64
    internal class Grouping<TKey, TElementKey, TElement> : IInformGrouping<TKey, TElementKey,TElement> //, IDictionary<TElementKey,TElement>, IAsyncGrouping<TKey, KeyValuePair<TElementKey,TElement>>
    {
        internal int _count;
        internal SimpleCache<TElementKey,TElement> _elements;
        internal int _hashCode;
        internal Grouping<TKey, TElementKey, TElement> _hashNext;
        internal TKey _key;
        internal Grouping<TKey, TElementKey, TElement>? _next;

        public Grouping(TKey key, int hashCode, KeyValuePair<TElementKey,TElement>[] elements, Grouping<TKey, TElementKey, TElement> hashNext)
        {
            _key = key;
            _hashCode = hashCode;
            _elements = elements;
            _hashNext = hashNext;
        }

        // IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<KeyValuePair<TElementKey,TElement>> GetEnumerator()
        {
            for (var i = 0; i < _count; i++)
            {
                yield return _elements[i];
            }
        }

        // DDB195907: implement IGrouping<>.Key implicitly
        // so that WPF binding works on this property.
        public TKey Key => _key;

        int ICollection<KeyValuePair<TElementKey,TElement>>.Count => _count;

        bool ICollection<KeyValuePair<TElementKey,TElement>>.IsReadOnly => true;

        void ICollection<KeyValuePair<TElementKey,TElement>>.Add(KeyValuePair<TElementKey,TElement> item) => throw Error.NotSupported();

        void ICollection<KeyValuePair<TElementKey,TElement>>.Clear() => throw Error.NotSupported();

        bool ICollection<KeyValuePair<TElementKey,TElement>>.Contains(KeyValuePair<TElementKey,TElement> item) => Array.IndexOf(_elements, item, 0, _count) >= 0;

        void ICollection<KeyValuePair<TElementKey,TElement>>.CopyTo(KeyValuePair<TElementKey,TElement>[] array, int arrayIndex) => Array.Copy(_elements, 0, array, arrayIndex, _count);

        bool ICollection<KeyValuePair<TElementKey,TElement>>.Remove(KeyValuePair<TElementKey,TElement> item) => throw Error.NotSupported();

        int IList<KeyValuePair<TElementKey,TElement>>.IndexOf(KeyValuePair<TElementKey,TElement> item) => Array.IndexOf(_elements, item, 0, _count);

        void IList<KeyValuePair<TElementKey,TElement>>.Insert(int index, KeyValuePair<TElementKey,TElement> item) => throw Error.NotSupported();

        void IList<KeyValuePair<TElementKey,TElement>>.RemoveAt(int index) => throw Error.NotSupported();

        KeyValuePair<TElementKey,TElement> IList<KeyValuePair<TElementKey,TElement>>.this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                {
                    throw Error.ArgumentOutOfRange(nameof(index));
                }

                return _elements[index];
            }

            set => throw Error.NotSupported();
        }

        internal void Add(KeyValuePair<TElementKey,TElement> element)
        {
            if (_elements.Length == _count)
            {
                Array.Resize(ref _elements, checked(_count*2));
            }

            _elements[_count] = element;
            _count++;
        }

        internal void Trim()
        {
            if (_elements.Length != _count)
            {
                Array.Resize(ref _elements, _count);
            }
        }

        IAsyncEnumerator<ResourceEvent<TElementKey, TElement>> IAsyncEnumerable<KeyValuePair<TElementKey,TElement>>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); // NB: [LDM-2018-11-28] Equivalent to async iterator behavior.

            return this.ToAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
        }

        // public IAsyncEnumerator<ResourceEvent<TElementKey, TElement>> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
        // {
        //     throw new NotImplementedException();
        // }
    }
}
