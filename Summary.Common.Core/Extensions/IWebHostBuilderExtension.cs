using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Summary.Common.Model;

namespace Summary.Common.Core.Extensions
{
    public static class IWebHostBuilderExtension
    {
        public static IWebHostBuilder UseKestrelbyConfig(this IWebHostBuilder webHostBuilder, IConfiguration configuration, Action<ListenOptions>? configure = null)
        {
            webHostBuilder.UseKestrel(kestrel =>
            {
                var basicfg = configuration.GetSection(nameof(AppAccessBasicConfiguration)).Get<AppAccessBasicConfiguration>();

                if (null == basicfg)
                    throw new ArgumentNullException("appsettings => AppAccessBasicConfiguration not be null");

                if (string.IsNullOrEmpty(basicfg.ServiceIp))
                    throw new ArgumentNullException("appsettings => AppAccessBasicConfiguration.ServiceIp not be null");

                if (null == basicfg.ServicePort || 0 == basicfg.ServicePort)
                    throw new ArgumentNullException("appsettings => AppAccessBasicConfiguration.ServicePort not be null or zero");

                kestrel.Listen(IPAddress.Parse(basicfg.ServiceIp!), basicfg.ServicePort.Value, option =>
                {
                    if (null != configure)
                    {
                        configure(option);
                    }
                    else if (basicfg.UseHttps)
                    {
                        option.Protocols = HttpProtocols.Http2;
                        option.UseHttps(
                            Certificate.Get(
                                basicfg.CertificatePath!, basicfg.CertificatePassWord!));
                    }
                });
            });

            return webHostBuilder;
        }

        public static IWebHostBuilder UseKestrelListenAnyIP(this IWebHostBuilder webHostBuilder, IConfiguration configuration, Action<ListenOptions>? configure = null)
        {
            webHostBuilder.UseKestrel(kestrel =>
            {
                var basicfg = configuration.GetSection(nameof(AppAccessBasicConfiguration)).Get<AppAccessBasicConfiguration>();

                if (null == basicfg)
                    throw new ArgumentNullException("appsettings => AppAccessBasicConfiguration not be null");

                if (null == basicfg.ServicePort || 0 == basicfg.ServicePort)
                    throw new ArgumentNullException("appsettings => AppAccessBasicConfiguration.ServicePort not be null or zero");

                kestrel.ListenAnyIP(basicfg.ServicePort.Value, option =>
                {
                    if (null != configure)
                    {
                        configure(option);
                    }
                    else if (basicfg.UseHttps)
                    {
                        option.Protocols = HttpProtocols.Http2;
                        option.UseHttps(
                            Certificate.Get(
                                basicfg.CertificatePath!, basicfg.CertificatePassWord!));
                    }
                });
            });

            return webHostBuilder;
        }
    }
}