using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RKS.RhythmParkour.Core.Settings
{
    public enum UpdateCheckState
    {
        Checking,
        UpToDate,
        UpdateAvailable,
        Failed,
    }

    [Serializable]
    public sealed class UpdateManifest
    {
        public string latestVersion;
        public string downloadUrl;
        public string notes;
        public string signature;
    }

    public sealed class UpdateChecker
    {
        public const string DefaultManifestUrl = "";

        const string PublicKeyXml = "<RSAKeyValue><Modulus>x0STCsL2Tvs9ily0fpFQAe0o6D+s5c2lqAHdUG4T+xoiL8kNHKVH7E93f6IKzkKW2QIS1fsaU5x4UxPoYlyT6udUcp/rZVJ/jcfIRdBklZiNpyZc0oKa0vOvkkRlLpTLS5YyK+BNg54Jxr3Cssvv8znsWw+VmK/rLB/BuNqRaBmyd6zDhKe7GAYpAwSJX9qwgKREht2hR18rOSaSBuThT5ZiWKOLHGygfrVPYn8sjmPV344SPvw8ujl0uGn2C/NuWYShS62MZ48LwhYhMy91O9IvW6vCZaGWKxSOL9V+TuhL52sGFcMOuiOSqPkt34r0fb1Z16qop54uuxyIXPXQmQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public string manifestUrl = DefaultManifestUrl;
        public int timeoutSeconds = 10;

        public bool isChecking { get; private set; }
        public string LatestVersion { get; private set; }
        public string DownloadUrl { get; private set; }
        public string Notes { get; private set; }

        public event Action<UpdateCheckState, string> OnResult;

        public void CheckForUpdates(string urlOverride = null)
        {
            if (isChecking) return;

            string url = !string.IsNullOrEmpty(urlOverride) ? urlOverride : manifestUrl;
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning("[UpdateChecker] Manifest URL is not set (UpdateChecker.manifestUrl).");
                OnResult?.Invoke(UpdateCheckState.Failed, null);
                return;
            }

            isChecking = true;
            CheckRoutine(url);
        }

        async void CheckRoutine(string url)
        {
            try
            {
                using (var request = UnityWebRequest.Get(url))
                {
                    request.timeout = timeoutSeconds;
                    await request.SendWebRequest();

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[UpdateChecker] Server did not respond: {request.error}");
                        OnResult?.Invoke(UpdateCheckState.Failed, request.error);
                        return;
                    }

                    UpdateManifest manifest;
                    try
                    {
                        manifest = JsonUtility.FromJson<UpdateManifest>(request.downloadHandler.text);
                    }
                    catch
                    {
                        manifest = null;
                    }

                    if (manifest == null || string.IsNullOrEmpty(manifest.latestVersion))
                    {
                        Debug.LogWarning("[UpdateChecker] Invalid update manifest (no latestVersion).");
                        OnResult?.Invoke(UpdateCheckState.Failed, "bad manifest");
                        return;
                    }

                    if (!VerifySignature(manifest))
                    {
                        OnResult?.Invoke(UpdateCheckState.Failed, "bad signature");
                        return;
                    }

                    LatestVersion = manifest.latestVersion.Trim();
                    DownloadUrl = manifest.downloadUrl;
                    Notes = manifest.notes;

                    int cmp = CompareVersions(BuildInfo.Version, LatestVersion);
                    OnResult?.Invoke(cmp < 0 ? UpdateCheckState.UpdateAvailable : UpdateCheckState.UpToDate, LatestVersion);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UpdateChecker] " + e.Message);
                OnResult?.Invoke(UpdateCheckState.Failed, e.Message);
            }
            finally
            {
                isChecking = false;
            }
        }

        static string BuildCanonical(UpdateManifest manifest)
        {
            return (manifest.latestVersion ?? string.Empty) + "\n"
                 + (manifest.downloadUrl ?? string.Empty) + "\n"
                 + (manifest.notes ?? string.Empty);
        }

        static bool VerifySignature(UpdateManifest manifest)
        {
            if (string.IsNullOrEmpty(manifest.signature))
            {
                Debug.LogWarning("[UpdateChecker] Manifest has no signature.");
                return false;
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(BuildCanonical(manifest));
                byte[] sig = Convert.FromBase64String(manifest.signature);
                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(PublicKeyXml);
                    return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UpdateChecker] Signature check failed: " + e.Message);
                return false;
            }
        }

        public static int CompareVersions(string left, string right)
        {
            if (string.IsNullOrEmpty(left)) left = "0";
            if (string.IsNullOrEmpty(right)) right = "0";

            string[] lp = left.Split('.');
            string[] rp = right.Split('.');
            int count = Math.Max(lp.Length, rp.Length);

            for (int i = 0; i < count; i++)
            {
                int li = ParseVersionPart(i < lp.Length ? lp[i] : "0");
                int ri = ParseVersionPart(i < rp.Length ? rp[i] : "0");
                if (li != ri) return li < ri ? -1 : 1;
            }
            return 0;
        }

        static int ParseVersionPart(string part)
        {
            if (string.IsNullOrEmpty(part)) return 0;
            int digits = 0;
            while (digits < part.Length && char.IsDigit(part[digits])) digits++;
            return int.TryParse(part.Substring(0, digits), out int value) ? value : 0;
        }
    }
}
