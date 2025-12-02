using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Summary.Common.Extensions
{
    public class Certificate
    {
        public static X509Certificate2 Get()
        {
            string allpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates//test.pfx");
            return new X509Certificate2(allpath, "123456");
        }

        public static X509Certificate2 Get(string certificatePath, string certificatePassWord)
        {
            if (string.IsNullOrEmpty(certificatePath))
                throw new ArgumentNullException("certificatePath");
            if (string.IsNullOrEmpty(certificatePassWord))
                throw new ArgumentNullException("certificatePassWord");

            string allpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, certificatePath);
            return new X509Certificate2(allpath, certificatePassWord);
        }
    }
}
