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
        /// 代理地址按“主机:端口”组合书写，如 127.0.0.1:7890；
        /// 也兼容用户误带协议前缀（http:// 或 socks5://）以及 IPv6 字面量。
        /// </summary>
        private static Uri? BuildProxyUri(Settings? cfg)
        {
            if (cfg == null) return null;

            string type = (cfg.UpdateProxyType ?? "off").Trim().ToLowerInvariant();
            if (type != "http" && type != "socks5") return null;

            string server = (cfg.UpdateProxyServer ?? "").Trim();
            if (string.IsNullOrWhiteSpace(server)) return null;

            // 去掉可能误带的协议前缀，剩余部分必须显式包含端口（冒号），
            // 避免把 “127.0.0.1” 静默解释成 80 端口
            string authority = server.Contains("://", StringComparison.Ordinal)
                ? server.Substring(server.IndexOf("://", StringComparison.Ordinal) + 3)
                : server;
            if (!authority.Contains(':')) return null;

            // 借 Uri 解析主机和端口；未写协议前缀时临时补 http://
            string parseText = server.Contains("://", StringComparison.Ordinal) ? server : "http://" + server;
            if (!Uri.TryCreate(parseText, UriKind.Absolute, out var parsed)) return null;

            // 主机非空且端口范围合法（漏写端口的情况已由上面的冒号校验拦截）
            if (parsed.Port > 65535 || string.IsNullOrWhiteSpace(parsed.Host)) return null;

            // .NET 8 的 SocketsHttpHandler 原生支持 socks5:// 协议
            string scheme = type == "socks5" ? "socks5" : "http";
            return new UriBuilder(scheme, parsed.Host, parsed.Port).Uri;
        }
    }
}
