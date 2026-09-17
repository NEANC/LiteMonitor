using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;

namespace LiteMonitor
{
    /// <summary>
    /// 更新流程专用 HttpClient 工厂。
    /// 根据设置中的“更新代理”配置构造带 HTTP / SOCKS5 代理的客户端，
    /// 配置为空或无效时静默回退为直连，不阻断更新流程。
    /// </summary>
    public static class UpdateHttpClient
    {
        /// <summary>
        /// 构造一个更新流程使用的 HttpClient（信任所有证书，行为与历史版本一致）。
        /// </summary>
        /// <param name="timeout">请求超时时间。</param>
        public static HttpClient Create(TimeSpan timeout)
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                SslOptions = new SslClientAuthenticationOptions
                {
                    // 强制信任所有证书（解决用户证书报错问题）
                    RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                }
            };

            var proxyUri = BuildProxyUri(Settings.Load());
            if (proxyUri != null)
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(proxyUri);
            }

            return new HttpClient(handler) { Timeout = timeout };
        }

        /// <summary>
        /// 根据设置拼出代理 URI；未启用或配置不合法时返回 null（直连）。
        /// </summary>
        private static Uri? BuildProxyUri(Settings? cfg)
        {
            if (cfg == null) return null;

            string type = (cfg.UpdateProxyType ?? "off").Trim().ToLowerInvariant();
            if (type != "http" && type != "socks5") return null;

            string host = (cfg.UpdateProxyHost ?? "").Trim();
            int port = cfg.UpdateProxyPort;
            if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535) return null;

            // .NET 8 的 SocketsHttpHandler 原生支持 socks5:// 协议
            string scheme = type == "socks5" ? "socks5" : "http";
            return new Uri($"{scheme}://{host}:{port}");
        }
    }
}
