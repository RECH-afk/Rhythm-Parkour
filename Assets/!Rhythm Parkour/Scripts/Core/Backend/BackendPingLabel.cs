using TMPro;
using UnityEngine;
using Zenject;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Backend;

namespace RKS.RhythmParkour.UI
{
    public class BackendPingLabel : RKSBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("TMP text that shows the current server latency")]
        private TextMeshProUGUI _label;

        [Header("Text")]
        [SerializeField, Tooltip("Localization key for online state; {0} is replaced with ms")]
        private string _onlineKey = "backend.pingOnline";
        [SerializeField, Tooltip("Localization key for offline state")]
        private string _offlineKey = "backend.pingOffline";

        [Header("Colors")]
        [SerializeField] private Color _onlineColor = new Color(0.55f, 0.9f, 0.6f, 1f);
        [SerializeField] private Color _offlineColor = new Color(0.95f, 0.5f, 0.5f, 1f);

        [InjectOptional] private BackendService _backendService;

        BackendService _backend;

        protected override void OnInjected()
        {
            _backend = _backendService;
        }

        protected override void OnReady()
        {
            if (_backend != null && _backend.Monitor != null)
            {
                _backend.Monitor.onPingUpdated += OnPing;
                _backend.Monitor.onConnectivityChanged += OnConnectivity;
            }
            Refresh();
        }

        protected override void OnDisposed()
        {
            if (_backend != null && _backend.Monitor != null)
            {
                _backend.Monitor.onPingUpdated -= OnPing;
                _backend.Monitor.onConnectivityChanged -= OnConnectivity;
            }
        }

        void OnPing(int latencyMs)
        {
            Refresh();
        }

        void OnConnectivity(bool online)
        {
            Refresh();
        }

        void Refresh()
        {
            if (_label == null)
                return;

            if (_backend == null || _backend.Monitor == null || !_backend.Monitor.Online)
            {
                _label.text = Loc(_offlineKey);
                _label.color = _offlineColor;
                return;
            }

            _label.text = Loc(_onlineKey).Replace("{0}", _backend.Monitor.LatencyMs.ToString());
            _label.color = _onlineColor;
        }

        string Loc(string key)
        {
            return Localization != null ? Localization.GetLocalizedValue(key) : key;
        }
    }
}
