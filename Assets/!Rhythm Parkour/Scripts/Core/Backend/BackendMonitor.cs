using System;

namespace RKS.RhythmParkour.Core.Backend
{
    public sealed class BackendMonitor
    {
        public event Action<int> onPingUpdated;
        public event Action<bool> onConnectivityChanged;

        public int LatencyMs { get; private set; }
        public bool Online { get; private set; }

        public void PublishPing(int latencyMs)
        {
            bool online = latencyMs >= 0;
            LatencyMs = latencyMs;
            bool changed = online != Online;
            Online = online;
            onPingUpdated?.Invoke(latencyMs);
            if (changed)
                onConnectivityChanged?.Invoke(online);
        }
    }
}
