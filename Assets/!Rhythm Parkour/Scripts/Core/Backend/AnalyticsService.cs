using System;
using System.Threading.Tasks;
using UnityEngine;
using RKS.RhythmParkour.Rhythm;

namespace RKS.RhythmParkour.Core.Backend
{
    public sealed class AnalyticsService
    {
        readonly BackendApi _api;
        readonly string _sessionId;

        public AnalyticsService(BackendApi api)
        {
            _api = api;
            _sessionId = Guid.NewGuid().ToString("N");
        }

        public string SessionId => _sessionId;

        public void SendSessionStart(string language, string token)
        {
            StatsSessionDto dto = new StatsSessionDto
            {
                sessionId = _sessionId,
                appVersion = BuildInfo.Version,
                platform = Application.platform.ToString(),
                os = SystemInfo.operatingSystem,
                cpuCores = SystemInfo.processorCount,
                memoryMb = SystemInfo.systemMemorySize,
                screenW = Screen.currentResolution.width,
                screenH = Screen.currentResolution.height,
                language = language ?? ""
            };
            FireQuietly(_api.SendSession(dto, token ?? ""));
        }

        public void TrackLevelFinished(LevelResults results, string token)
        {
            StatsLevelDto dto = new StatsLevelDto
            {
                sessionId = _sessionId,
                levelTitle = results.levelTitle ?? "",
                totalNotes = results.totalNotes,
                count300 = results.count300,
                count100 = results.count100,
                count50 = results.count50,
                countMiss = results.countMiss,
                maxCombo = results.maxCombo,
                accuracy = results.accuracy,
                flow = results.flow,
                rank = results.rank ?? "",
                failed = results.failed
            };
            FireQuietly(_api.SendLevel(dto, token ?? ""));
        }

        async void FireQuietly(Task<ApiReply> call)
        {
            try
            {
                await call;
            }
            catch (Exception)
            {
            }
        }
    }
}
