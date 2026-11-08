using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace RKS.RhythmParkour.Core.Backend
{
    public sealed class ApiReply
    {
        public bool Ok;
        public string Error = "";
        public string ServerTime = "";
        public string MinVersion = "";
        public string Token = "";
        public string AccountName = "";
        public int LatencyMs = -1;
    }

    public sealed class BackendApi
    {
        const int TimeoutSeconds = 10;
        const int MaxResponseChars = 8192;

        readonly string _baseUrl;
        readonly string _secret;

        public BackendApi(string baseUrl, string secret)
        {
            string trimmed = (baseUrl ?? "").Trim();
            _baseUrl = trimmed.Length > 0 && !trimmed.EndsWith("/", StringComparison.Ordinal)
                ? trimmed + "/"
                : trimmed;
            _secret = secret ?? "";
        }

        public string BaseUrl => _baseUrl;

        public Task<ApiReply> Ping()
        {
            return Send("GET", "ping", null, "");
        }

        public Task<ApiReply> Register(string username, string password)
        {
            return Send("POST", "register", new CredentialsDto { username = username, password = password }, "");
        }

        public Task<ApiReply> Login(string username, string password)
        {
            return Send("POST", "login", new CredentialsDto { username = username, password = password }, "");
        }

        public Task<ApiReply> Logout(string token)
        {
            return Send("POST", "logout", null, token);
        }

        public Task<ApiReply> Me(string token)
        {
            return Send("GET", "me", null, token);
        }

        public Task<ApiReply> SendSession(StatsSessionDto payload, string token)
        {
            return Send("POST", "stats/session", payload, token);
        }

        public Task<ApiReply> SendLevel(StatsLevelDto payload, string token)
        {
            return Send("POST", "stats/level", payload, token);
        }

        async Task<ApiReply> Send(string method, string route, object payload, string token)
        {
            ApiReply reply = new ApiReply();
            if (!HasHttpsBase(reply))
                return reply;

            using (UnityWebRequest request = Build(method, _baseUrl + route, payload))
            {
                request.timeout = TimeoutSeconds;
                request.SetRequestHeader("RhythmParkour-Client", _secret);
                if (!string.IsNullOrEmpty(token))
                    request.SetRequestHeader("X-Auth-Token", token);

                try
                {
                    await WaitForDone(request, reply);
                    string text = request.downloadHandler != null ? request.downloadHandler.text : "";
                    if (request.result == UnityWebRequest.Result.ConnectionError)
                    {
                        reply.Error = "network";
                        return reply;
                    }
                    Parse(reply, text);
                }
                catch (Exception)
                {
                    reply.Error = "network";
                }
                return reply;
            }
        }

        UnityWebRequest Build(string method, string url, object payload)
        {
            if (payload != null)
            {
                byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
                UnityWebRequest request = new UnityWebRequest(url, method)
                {
                    uploadHandler = new UploadHandlerRaw(body),
                    downloadHandler = new DownloadHandlerBuffer()
                };
                request.uploadHandler.contentType = "application/json";
                return request;
            }

            return new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };
        }

        async Task WaitForDone(UnityWebRequest request, ApiReply reply)
        {
            Stopwatch clock = Stopwatch.StartNew();
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<AsyncOperation> handler = null;
            handler = op =>
            {
                op.completed -= handler;
                clock.Stop();
                reply.LatencyMs = (int)clock.ElapsedMilliseconds;
                completion.TrySetResult(true);
            };
            op.completed += handler;
            await completion.Task;
        }

        void Parse(ApiReply reply, string text)
        {
            if (!string.IsNullOrEmpty(text) && text.Length <= MaxResponseChars)
            {
                BackendResponseDto dto = null;
                try
                {
                    dto = JsonUtility.FromJson<BackendResponseDto>(text);
                }
                catch (Exception)
                {
                    dto = null;
                }
                if (dto != null)
                {
                    reply.Ok = dto.ok;
                    reply.Error = dto.error ?? "";
                    reply.ServerTime = dto.serverTime ?? "";
                    reply.MinVersion = dto.minVersion ?? "";
                    reply.Token = dto.token ?? "";
                    reply.AccountName = dto.accountName ?? "";
                }
            }

            if (!reply.Ok && string.IsNullOrEmpty(reply.Error))
                reply.Error = "server";
            if (reply.Ok)
                reply.Error = "";
        }

        bool HasHttpsBase(ApiReply reply)
        {
            if (_baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;
            if (_baseUrl.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase)
                || _baseUrl.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                return true;
            reply.Error = "https_required";
            return false;
        }
    }
}
