using System;
using System.Collections.Generic;

namespace Summary.Common.Model
{
    public class UserSessionCache
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public List<string> Roles { get; set; } = [];
    }
}