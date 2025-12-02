using System;
using Summary.Domain.Interfaces;

namespace Summary.Domain.Entities
{
    public class Bill : ISoftDelete, ICreateEntity<Guid, Guid>, IHaveTenant<string>
    {
        public bool IsDeleted { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
        public double Price { get; set; }
        public decimal Amount { get; set; }
        public string Account { get; set; } = string.Empty;

        //[MaxLength(256)]
        public string Description { get; set; } = string.Empty;

        public Guid CreationUserId { get; set; }
        public DateTime CreationTime { get; set; }
        public string TenantId { get; set; } = string.Empty;
    }
}