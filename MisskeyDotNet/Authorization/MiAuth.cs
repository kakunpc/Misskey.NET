using System;
using System.Threading.Tasks;

namespace MisskeyDotNet
{
    public sealed class MiAuth
    {
        public string Host { get; } 
        
        public bool IsNotSecureServer { get; } = false;

        public string? Name { get; }

        public string? IconUrl { get; }

        public string? CallbackUrl { get; }

        public string Uuid { get; }

        public Permission[] Permissions { get; } = new Permission[0];

        public string Url { get; }

        public MiAuth(string host, bool isNotSecureServer, string? name, string? iconUrl = null, string? callbackUrl = null, params Permission[] permissions)
        {
            Host = host;
            Name = name;
            IsNotSecureServer = isNotSecureServer;
            IconUrl = iconUrl;
            CallbackUrl = callbackUrl;
            Permissions = permissions;
            Uuid = Guid.NewGuid().ToString();
            stringifiedPermissions = string.Join(',', Permissions.ToStringArray());
            var protocol = isNotSecureServer ? "http://" : "https://";

            Url = protocol + Host + "/miauth/" + Uuid;

            var query = new
            {
                name = Name,
                icon = IconUrl,
                callback = CallbackUrl,
                permission = stringifiedPermissions,
            }.ToQueryString();
            if (query != null)
                Url += "?" + query;
        }

        public bool TryOpenBrowser()
        {
            try
            {
                OpenBrowser();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void OpenBrowser()
        {
            Helper.OpenUrl(Url);
        }

        public async ValueTask<Misskey> CheckAsync()
        {
            var res = await new Misskey(Host, IsNotSecureServer).ApiAsync("miauth/" + Uuid + "/check");

            if (res["token"] is string token)
                return new Misskey(Host, IsNotSecureServer, token);

            throw new InvalidOperationException("Failed to check MiAuth session");
        }

        private string stringifiedPermissions;
    }
}