using System;
using System.Threading.Tasks;
using RKS.RhythmParkour.Core.Managers;

namespace RKS.RhythmParkour.Core.Backend
{
    public sealed class AccountService
    {
        readonly BackendApi _api;
        readonly SaveManager _save;

        public event Action onChanged;

        public AccountService(BackendApi api, SaveManager save)
        {
            _api = api;
            _save = save;
        }

        public bool IsSignedIn => !string.IsNullOrEmpty(Token);
        public string Token { get; private set; } = "";
        public string AccountName { get; private set; } = "";

        public void LoadStored()
        {
            GameData.Data data = _save != null ? _save.CurrentData : null;
            if (data == null)
                return;
            Token = data.authToken ?? "";
            AccountName = data.accountName ?? "";
            NotifyChanged();
        }

        public Task<string> RegisterAsync(string username, string password)
        {
            return RunAuth(_api.Register(username, password));
        }

        public Task<string> LoginAsync(string username, string password)
        {
            return RunAuth(_api.Login(username, password));
        }

        public async Task ValidateStoredAsync()
        {
            if (!IsSignedIn)
                return;
            ApiReply reply = await _api.Me(Token);
            if (reply.Error == "network" || reply.Error == "server" || reply.Error == "rate_limited")
                return;
            if (!reply.Ok)
                ClearStored();
        }

        public async Task SignOutAsync()
        {
            string token = Token;
            ClearStored();
            if (!string.IsNullOrEmpty(token))
                await _api.Logout(token);
        }

        async Task<string> RunAuth(Task<ApiReply> call)
        {
            ApiReply reply = await call;
            if (!reply.Ok)
                return reply.Error;
            Token = reply.Token ?? "";
            AccountName = reply.AccountName ?? "";
            Persist();
            NotifyChanged();
            return "";
        }

        void ClearStored()
        {
            Token = "";
            AccountName = "";
            Persist();
            NotifyChanged();
        }

        void Persist()
        {
            GameData.Data data = _save != null ? _save.CurrentData : null;
            if (data == null)
                return;
            data.authToken = Token;
            data.accountName = AccountName;
            _save.Write();
        }

        void NotifyChanged()
        {
            onChanged?.Invoke();
        }
    }
}
