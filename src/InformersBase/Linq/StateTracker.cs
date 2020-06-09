using Steeltoe.Informers.InformersBase;

namespace System.Linq
{
    internal class StateTracker<TKey, TSource>
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
}