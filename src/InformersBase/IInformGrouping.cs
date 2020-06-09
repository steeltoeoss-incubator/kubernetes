namespace Steeltoe.Informers.InformersBase
{
    public interface IInformGrouping<out TGroupKey, TResourceKey, TResource> : IInformable<TResourceKey, TResource>
    {
        public TGroupKey Key { get; }
    }
}