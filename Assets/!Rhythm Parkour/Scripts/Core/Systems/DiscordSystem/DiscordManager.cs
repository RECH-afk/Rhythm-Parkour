using Discord;
using UnityEngine;
using RKS.HadalZone.Core;

namespace RKS.HadalZone.Core.Managers
{
    public class DiscordManager : RKSBehaviour
    {
        [Header("Discord Application Settings")]
        public long applicationID;

        [Header("Rich Presence Details")]
        public string details;
        public string state;
        public string largeImage;
        public string largeText;

        private long time;
        private Discord.Discord discord;

        protected override void OnReady()
        {
            discord = new Discord.Discord(applicationID, (ulong)Discord.CreateFlags.NoRequireDiscord);
            time = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();

            UpdateStatus();
        }

        protected override void Update()
        {
            if (discord == null)
                return;

            SafeInvoke(UpdateStatus);

            try
            {
                discord.RunCallbacks();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Discord callbacks error: {ex}");
                SafeDispose();
            }
        }

        private void UpdateStatus()
        {
            var activityManager = discord.GetActivityManager();

            var activity = new Discord.Activity
            {
                Details = details,
                State = state,
                Assets =
                {
                    LargeImage = largeImage,
                    LargeText = largeText
                },
                Timestamps =
                {
                    Start = time
                }
            };

            activityManager.UpdateActivity(activity, res =>
            {
                if (res != Discord.Result.Ok)
                    Debug.LogWarning("Failed connecting to Discord!");
            });
        }

        protected override void OnDisposed()
        {
            SafeDispose();
        }

        private void OnApplicationQuit()
        {
            SafeDispose();
        }

        private void OnDisable()
        {
            SafeDispose();
        }

        private void SafeDispose()
        {
            if (discord != null)
            {
                try { discord.Dispose(); }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Discord Dispose exception: {ex}");
                }

                discord = null;
                Debug.Log("Discord RPC disposed.");
            }
        }
    }
}
