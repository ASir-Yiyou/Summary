using System;

namespace Summary.Common.Model
{
    public class AppAccessBasicConfiguration
    {
        private string? _appAccessDomain;

        public AppAccessBasicConfiguration()
        {
            Root = AppDomain.CurrentDomain.BaseDirectory;
            ServiceIp = "127.0.0.1";
            ServicePort = 5000;
        }

        /// <summary>
        /// application根目录
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// [宿主机/docker]中服务的ip
        /// </summary>
        public string? ServiceIp { get; set; }

        /// <summary>
        /// [宿主机/docker]中服务的端口
        /// </summary>
        public int? ServicePort { get; set; }

        public bool UseHttps { get; set; }

        /// <summary>
        /// 证书path
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// 证书密码
        /// </summary>
        public string? CertificatePassWord { get; set; }

        /// <summary>
        /// 程序访问的域名
        /// </summary>
        public string AppAccessDomain
        {
            get
            {
                if (string.IsNullOrEmpty(_appAccessDomain))
                {
                    return $"{(UseHttps ? "https" : "http")}://{ServiceIp}:{ServicePort}";
                }

                return _appAccessDomain;
            }
            set
            {
                _appAccessDomain = value;
            }
        }
    }
}