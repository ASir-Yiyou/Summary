using System;
using System.Collections.Generic;
using Summary.Domain.Interfaces;

namespace Summary.Domain.Entities
{
    public class User : IEntity<Guid>, IHaveTenant<Guid>, ISoftDelete
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();

        public string Username { get; set; } = string.Empty;

        public Guid MainGroupId { get; set; }

        public bool IsDeleted { get; set; }

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }

    public class UserRole
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
    }
}