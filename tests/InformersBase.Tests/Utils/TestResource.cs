using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Steeltoe.Informers.InformersBase.Tests.Utils
{
    [DebuggerStepThrough]
    public class TestResource : IEquatable<TestResource>
    {
        public bool Equals(TestResource other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Value == other.Value && Key == other.Key && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TestResource) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Value != null ? Value.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Key != null ? Key.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Version;
                return hashCode;
            }
        }

        public static bool operator ==(TestResource left, TestResource right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TestResource left, TestResource right)
        {
            return !Equals(left, right);
        }

        private sealed class KeyVersionEqualityComparer : IEqualityComparer<TestResource>
        {
            public bool Equals(TestResource x, TestResource y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(y, null))
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return x.Key == y.Key && x.Version == y.Version;
            }

            public int GetHashCode(TestResource obj)
            {
                unchecked
                {
                    return (obj.Key != null ? obj.Key.GetHashCode() : 0) ^ obj.Version;
                }
            }
        }

        public static IEqualityComparer<TestResource> KeyVersionComparer { get; } = new KeyVersionEqualityComparer();

        public ResourceEvent<string, TestResource> ToResourceEvent(EventTypeFlags typeFlags)
        {
            return new ResourceEvent<string, TestResource>(typeFlags, Key, this);
        }
        public TestResource(string key, int version = 1, string value = "test")
        {
            Value = value;
            Version = version;
            Key = key;
        }

        public string Value { get; }
        public string Key { get; }
        public int Version { get; }

        public override string ToString()
        {
            return $"{nameof(Key)}: {Key}, {nameof(Value)}: {Value}, {nameof(Version)} {Version}";
        }
    }
}
