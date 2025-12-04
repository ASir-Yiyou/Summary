using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Summary.Common.Core.Extensions
{
    public class Certificate
    {
        public static X509Certificate2 Get()
        {
            string allpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates//test.pfx");
            return new X509Certificate2(allpath, "123456");
        }

        public static X509Certificate2 Get(string certificatePath, string certificatePassWord, ILogger? logger = null)
        {
            if (string.IsNullOrEmpty(certificatePath))
                throw new ArgumentNullException("certificatePath");
            if (string.IsNullOrEmpty(certificatePassWord))
                throw new ArgumentNullException("certificatePassWord");

            string allpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, certificatePath);
            logger?.LogInformation("certificatePath:{0}", allpath);
            return new X509Certificate2(allpath, certificatePassWord);
        }
    }
}