using System;

namespace DotsAndBoxes.Gameplay
{
    // Credentials live in memory only, never in PlayerPrefs or scene assets.
    public static class GameAccountSession
    {
        private static readonly object SESSION_LOCK = new object();
        private static string s_serverUrl = string.Empty;
        private static string s_accessToken = string.Empty;
        private static Guid s_userId;
        private static string s_displayName = string.Empty;
        private static DateTimeOffset s_expiresAtUtc;

        public static void Set(string serverUrl, Guid userId, string displayName, string accessToken, DateTimeOffset expiresAtUtc)
        {
            if (userId == Guid.Empty || string.IsNullOrWhiteSpace(accessToken) || expiresAtUtc <= DateTimeOffset.UtcNow.AddSeconds(30))
                throw new ArgumentException("서버 로그인 응답이 올바르지 않습니다.");
            lock (SESSION_LOCK)
            {
                s_serverUrl = serverUrl.TrimEnd('/');
                s_userId = userId;
                s_displayName = displayName;
                s_accessToken = accessToken;
                s_expiresAtUtc = expiresAtUtc;
            }
        }

        public static bool TryGet(string serverUrl, out Guid userId, out string displayName)
        {
            lock (SESSION_LOCK)
            {
                userId = s_userId;
                displayName = s_displayName;
                return s_userId != Guid.Empty && s_serverUrl == serverUrl.TrimEnd('/') &&
                    s_expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(15);
            }
        }

        public static string GetAccessToken(string serverUrl, Guid userId)
        {
            lock (SESSION_LOCK)
            {
                if (s_userId != userId || s_serverUrl != serverUrl.TrimEnd('/') ||
                    s_expiresAtUtc <= DateTimeOffset.UtcNow.AddSeconds(10))
                    throw new InvalidOperationException("로그인이 만료되었습니다. 로비에서 다시 로그인해 주세요.");
                return s_accessToken;
            }
        }

        public static void Clear()
        {
            lock (SESSION_LOCK)
            {
                s_userId = Guid.Empty;
                s_serverUrl = string.Empty;
                s_displayName = string.Empty;
                s_accessToken = string.Empty;
                s_expiresAtUtc = default;
            }
        }
    }
}
