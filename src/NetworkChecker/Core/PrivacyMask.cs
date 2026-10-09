// File: src/NetworkChecker/Core/PrivacyMask.cs
using System.Net;
using System.Net.Sockets;

namespace NetworkChecker.Core;

/// <summary>
/// プライバシー情報（IP・MAC・機名・ユーザー名・内部DNS名）のマスク。
/// マスク→原文の対応表を保持し、HTML レポートの「マスク解除」で利用する。
/// </summary>
public static class PrivacyMask
{
    private static readonly Dictionary<string, string> MaskedToOriginal = new(StringComparer.Ordinal);

    public static bool Enabled { get; set; } = true;

    public static IReadOnlyDictionary<string, string> Mappings => MaskedToOriginal;

    public static void Reset() => MaskedToOriginal.Clear();

    private static string Register(string original, string masked)
    {
        if (Enabled && !string.IsNullOrEmpty(original) && original != masked)
        {
            // 同一マスク表記に複数の実値が対応する場合は先勝ちとする
            MaskedToOriginal.TryAdd(masked, original);
        }
        return masked;
    }

    public static string Ip(IPAddress? ip)
    {
        if (ip is null) return "";
        if (!Enabled) return ip.ToString();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var parts = ip.ToString().Split('.');
            if (parts.Length == 4)
            {
                return Register(ip.ToString(), $"{parts[0]}.{parts[1]}.***.***");
            }
        }

        var text = ip.ToString();
        var head = text.Split(':')[0];
        return Register(text, $"{head}::***");
    }

    public static string IpString(string? ipText)
    {
        if (string.IsNullOrEmpty(ipText)) return "";
        return IPAddress.TryParse(ipText, out var ip) ? Ip(ip) : ipText;
    }

    public static string Mac(byte[]? mac)
    {
        if (mac is null || mac.Length == 0) return "";
        var original = BitConverter.ToString(mac);
        if (!Enabled) return original;
        return Register(original, "**-**-**-**-**-**");
    }

    public static string Name(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (!Enabled) return name;
        var masked = name.Length <= 1 ? "*" : name[0] + new string('*', Math.Min(name.Length - 1, 6));
        return Register(name, masked);
    }

    /// <summary>プロキシサーバー名（内部DNS名を含みうる）のマスク。</summary>
    public static string ProxyServer(string? server)
    {
        if (string.IsNullOrEmpty(server)) return "";
        if (!Enabled) return server;
        var idx = server.LastIndexOf(':');
        var host = idx > 0 ? server[..idx] : server;
        var port = idx > 0 ? server[idx..] : "";
        return Register(server, Name(host) + port);
    }
}