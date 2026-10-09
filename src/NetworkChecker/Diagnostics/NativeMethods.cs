// File: src/NetworkChecker/Diagnostics/NativeMethods.cs
using System.Runtime.InteropServices;

namespace NetworkChecker.Diagnostics;

internal static class NativeMethods
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint macAddrLen);
}