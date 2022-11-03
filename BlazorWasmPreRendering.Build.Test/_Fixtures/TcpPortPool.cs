using System.Net.NetworkInformation;
using AngleSharp.Common;

namespace BlazorWasmPreRendering.Build.Test;

internal class TcpPortPool : IDisposable
{
    private static readonly HashSet<int> _UsedTcpPort = new();

    private int _Port;

    public static TcpPortPool GetAvailableTcpPort()
    {
        lock (_UsedTcpPort)
        {
            var usedTcpPorts = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Select(listener => listener.Port)
                .Concat(_UsedTcpPort)
                .ToHashSet();

            var availabeTcpPort = Enumerable.Range(5000, 999).FirstOrDefault(port => !usedTcpPorts.Contains(port));
            if (availabeTcpPort == 0) throw new Exception($"There is no avaliable TCP port in range \"5000 - 5999\".");
            _UsedTcpPort.Add(availabeTcpPort);
            return new TcpPortPool(availabeTcpPort);
        }
    }

    private TcpPortPool(int port) { this._Port = port; }

    public void Dispose() { lock (_UsedTcpPort) { _UsedTcpPort.Remove(this._Port); } }

    public override string ToString() => this._Port.ToString();

    public static implicit operator string(TcpPortPool tcpPort) => tcpPort.ToString();
}
