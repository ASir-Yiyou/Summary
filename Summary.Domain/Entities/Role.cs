using System;
using System.Collections.Generic;
using Summary.Domain.Enums;
using Summary.Domain.Interfaces;

namespace Summary.Domain.Entities
{
    public class Role : IHaveTenant<Guid>
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    }

    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public string Resource { get; set; } = string.Empty; // 例如 "Order"
        public string Action { get; set; } = string.Empty;   // 例如 "Read"
        public DataScope Scope { get; set; }
    }
}
