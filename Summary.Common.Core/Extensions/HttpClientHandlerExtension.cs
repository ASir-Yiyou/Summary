using System.Security.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Summary.Common.Model;

namespace Summary.Common.Core.Extensions
{
    public static class HttpClientHandlerExtension
    {
        public static IHttpClientBuilder ConfigurePrimaryHttpsMessageHandler(this IHttpClientBuilder builder)
        {
            return builder.ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var configOptions = provider.GetRequiredService<IOptions<AppAccessBasicConfiguration>>().Value;

                var handler = new HttpClientHandler();

                handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

                handler.UseCookies = false;
                handler.AllowAutoRedirect = false;

                //if (configOptions.SkipSslValidation)
                //{
                //    handler.ServerCertificateCustomValidationCallback =
                //        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                //}

                // --- 客户端证书 (mTLS) ---

                if (!string.IsNullOrEmpty(configOptions.CertificatePath) &&
                    !string.IsNullOrEmpty(configOptions.CertificatePassWord))
                {
                    // 关键修正：必须设为 Manual，否则手动添加的证书可能无效
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;

                    handler.ClientCertificates.Add(
                        Certificate.Get(configOptions.CertificatePath, configOptions.CertificatePassWord)
                    );
                }
                else
                {
                    // 如果没配证书，让它自动处理（或者根据需求设为 None）
                    handler.ClientCertificateOptions = ClientCertificateOption.Automatic;
                }

                return handler;
            });
        }
    }
}