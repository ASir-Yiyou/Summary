namespace WebService.Interface
{
    public interface IAuthService
    {
        public Task RegisterAsync(string username, string password, Guid tenantId);

        public Task<string?> LoginAsync(string username, string password);

        Task ChangePasswordAsync(Guid userId, string newPassword);
    }
}