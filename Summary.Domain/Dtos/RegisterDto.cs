using System;

namespace Summary.Domain.Dtos
{
    public class RegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid TenantId { get; set; } // 注册时指定租户
        public Guid GroupId { get; set; }  // 注册时指定部门
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}