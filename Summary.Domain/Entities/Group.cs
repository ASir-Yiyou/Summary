using System;
using System.Collections.Generic;
using System.Text;
using Summary.Domain.Interfaces;

namespace Summary.Domain.Entities
{
    public class Group : IHaveTenant<Guid>
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;

        // 树形结构
        public Guid? ParentId { get; set; }
        public Group? Parent { get; set; }
        public ICollection<Group> Children { get; set; } = [];
    }
}
