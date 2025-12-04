using System.Security.Cryptography;
using System.Text;

namespace AuthenticationServer.Service
{
    public interface IUrlSignerService
    {
        string GenerateSignedUrl(string resourceId, TimeSpan validFor);
        bool ValidateSignature(string resourceId, long expires, string providedSignature);
    }

    public class UrlSignerService : IUrlSignerService
    {
        private readonly string _secretKey;
        private readonly string _baseUrl;

        public UrlSignerService(IConfiguration configuration)
        {
            _secretKey = configuration["ShareLinkSecret"] ?? "01a5b97fb4e24c2688e9adeae6385d8a";
            //API 基础地址
            _baseUrl = configuration["ApiBaseUrl"] ?? "https://localhost:7072";
        }

        public string GenerateSignedUrl(string resourceId, TimeSpan validFor)
        {
            var expires = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();

            var signature = ComputeSignature(resourceId, expires);

            // 3. 拼接 URL
            // 格式: /api/share/view?id=xxx&expires=xxx&sig=xxx
            return $"{_baseUrl}/api/share/view?id={resourceId}&expires={expires}&sig={signature}";
        }

        public bool ValidateSignature(string resourceId, long expires, string providedSignature)
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
            {
                return false; // 已过期
            }

            var expectedSignature = ComputeSignature(resourceId, expires);
            return expectedSignature == providedSignature;
        }

        private string ComputeSignature(string resourceId, long expires)
        {
            var rawData = $"{resourceId}|{expires}";
            var keyBytes = Encoding.UTF8.GetBytes(_secretKey);
            var dataBytes = Encoding.UTF8.GetBytes(rawData);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(dataBytes);

            // 转为 Hex 字符串 (或者 Base64Url)
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
