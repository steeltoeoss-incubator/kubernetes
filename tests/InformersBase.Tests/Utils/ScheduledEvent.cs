namespace Steeltoe.Informers.InformersBase.Tests.Utils
{
    public struct ScheduledEvent<TResource>
    {
        public ResourceEvent<string, TResource> Event { get; set; }
        public long ScheduledAt { get; set; }
        public override string ToString()
        {
            return $"\n   T{ScheduledAt}: {Event.ToString().Replace("\r\n", string.Empty).Trim()}";
        }
    }
}
