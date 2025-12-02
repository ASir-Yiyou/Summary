namespace Summary.Domain.Interfaces
{
    public interface IHaveTenant<K>
    {
        K TenantId { get; set; }
    }
}