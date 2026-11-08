using System;
using System.Collections;
using UnityEngine;
using RKS.RhythmParkour.Rhythm;

namespace RKS.RhythmParkour.Core.Backend
{
    public class BackendService : RKSBehaviour
    {
        [Header("Connection")]
        [SerializeField, Tooltip("HTTPS base URL ending with /, e.g. https://backend.relkvinstudio.top/api/")]
        private string _baseUrl = "https://backend.relkvinstudio.top/api/";

        [SerializeField, Tooltip("Secret sent as the RhythmParkour-Client header; must equal RP_CLIENT_SECRET in Backend/api/config.php")]
        private string _clientSecret = "CHANGE_ME";

        [SerializeField, Tooltip("Seconds between automatic health checks")]
        private float _pingIntervalSeconds = 10f;

        [SerializeField, Tooltip("Send anonymous session statistics at startup")]
        private bool _sendSessionAnalytics = true;

        public BackendApi Api { get; private set; }
        public BackendMonitor Monitor { get; private set; }
        public AccountService Account { get; private set; }
        public AnalyticsService Analytics { get; private set; }

        public string BaseUrl => Api != null ? Api.BaseUrl : "";

        public bool IsConfigured
        {
            get
            {
                string secret = _clientSecret ?? "";
                return secret.Length > 0 && secret != "CHANGE_ME";
            }
        }

        protected override void OnReady()
        {
            Api = new BackendApi(_baseUrl, _clientSecret);
            Monitor = new BackendMonitor();
            Account = new AccountService(Api, Save);
            Account.LoadStored();
            if (_sendSessionAnalytics)
                Analytics = new AnalyticsService(Api);
            StartCoroutine(PingLoop());
            StartCoroutine(BootstrapRoutine());
        }

        protected override void OnDisposed()
        {
            StopAllCoroutines();
        }

        public void TrackLevelFinished(LevelResults results)
        {
            if (Analytics == null)
                return;
            Analytics.TrackLevelFinished(results, Account != null ? Account.Token : "");
        }

        IEnumerator PingLoop()
        {
            WaitForSeconds interval = new WaitForSeconds(Mathf.Max(2f, _pingIntervalSeconds));
            while (Application.isPlaying)
            {
                FirePing();
                yield return interval;
            }
        }

        IEnumerator BootstrapRoutine()
        {
            float startedAt = Time.unscaledTime;
            while (Save == null || Save.CurrentData == null)
            {
                if (Time.unscaledTime - startedAt > 5f)
                    yield break;
                yield return null;
            }

            yield return new WaitForSeconds(1f);

            Account.LoadStored();

            if (Account.IsSignedIn)
                FireValidateStored();
            else
                FireSessionStart();
        }

        async void FirePing()
        {
            try
            {
                ApiReply reply = await Api.Ping();
                if (Monitor != null)
                    Monitor.PublishPing(reply.Ok ? reply.LatencyMs : -1);
            }
            catch (Exception)
            {
                if (Monitor != null)
                    Monitor.PublishPing(-1);
            }
        }

        async void FireValidateStored()
        {
            try
            {
                await Account.ValidateStoredAsync();
            }
            catch (Exception)
            {
            }
            FireSessionStart();
        }

        void FireSessionStart()
        {
            if (!_sendSessionAnalytics || Analytics == null)
                return;
            string language = Save.CurrentData != null ? Save.CurrentData.language : "en_US";
            Analytics.SendSessionStart(language, Account != null ? Account.Token : "");
        }
    }
}
