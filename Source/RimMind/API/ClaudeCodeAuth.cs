using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Verse;

namespace RimMind.API
{
    public static class ClaudeCodeAuth
    {
        private const string TOKEN_URL = "https://platform.claude.com/v1/oauth/token";
        private const string CLIENT_ID = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
        private const string SCOPES = "user:profile user:inference user:sessions:claude_code user:mcp_servers";

        private static string cachedAccessToken;
        private static string cachedRefreshToken;
        private static long expiresAtMs;
        private static readonly object tokenLock = new object();

        public static string GetAccessToken()
        {
            lock (tokenLock)
            {
                // If we have a cached token that's not expired (with 5 min buffer), use it
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (!string.IsNullOrEmpty(cachedAccessToken) && expiresAtMs > nowMs + 300000)
                    return cachedAccessToken;

                // Try to read from credentials file
                if (!ReadCredentials())
                    return null;

                // Check if token is still valid
                nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (expiresAtMs > nowMs + 300000)
                    return cachedAccessToken;

                // Token expired, try refresh
                if (!string.IsNullOrEmpty(cachedRefreshToken))
                {
                    if (RefreshToken())
                        return cachedAccessToken;
                }

                return null;
            }
        }

        public static bool IsAvailable()
        {
            string credPath = GetCredentialsPath();
            if (!File.Exists(credPath))
                return false;

            try
            {
                string json = File.ReadAllText(credPath, Encoding.UTF8);
                var root = JSONNode.Parse(json);
                var oauth = root["claudeAiOauth"];
                return oauth != null && !oauth.IsNull && !string.IsNullOrEmpty(oauth["accessToken"]?.Value);
            }
            catch
            {
                return false;
            }
        }

        private static bool ReadCredentials()
        {
            try
            {
                string credPath = GetCredentialsPath();
                if (!File.Exists(credPath))
                {
                    Log.Warning("[RimMind] Claude Code credentials not found at: " + credPath);
                    return false;
                }

                string json = File.ReadAllText(credPath, Encoding.UTF8);
                var root = JSONNode.Parse(json);
                var oauth = root["claudeAiOauth"];

                if (oauth == null || oauth.IsNull)
                {
                    Log.Warning("[RimMind] No claudeAiOauth entry in credentials file.");
                    return false;
                }

                cachedAccessToken = oauth["accessToken"]?.Value;
                cachedRefreshToken = oauth["refreshToken"]?.Value;

                var expiresNode = oauth["expiresAt"];
                if (expiresNode != null && !expiresNode.IsNull)
                    expiresAtMs = expiresNode.AsLong;

                return !string.IsNullOrEmpty(cachedAccessToken);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Failed to read Claude Code credentials: " + ex.Message);
                return false;
            }
        }

        private static bool RefreshToken()
        {
            try
            {
                Log.Message("[RimMind] Refreshing Claude Code OAuth token...");

                var requestBody = new JSONObject();
                requestBody["grant_type"] = "refresh_token";
                requestBody["refresh_token"] = cachedRefreshToken;
                requestBody["client_id"] = CLIENT_ID;
                requestBody["scope"] = SCOPES;
                string body = requestBody.ToString();

                var httpRequest = (HttpWebRequest)WebRequest.Create(TOKEN_URL);
                httpRequest.Method = "POST";
                httpRequest.ContentType = "application/json";
                httpRequest.Timeout = 30000;

                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                httpRequest.ContentLength = bodyBytes.Length;

                using (var stream = httpRequest.GetRequestStream())
                {
                    stream.Write(bodyBytes, 0, bodyBytes.Length);
                }

                using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
                using (var reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                {
                    string responseJson = reader.ReadToEnd();
                    var root = JSONNode.Parse(responseJson);

                    string newAccessToken = root["access_token"]?.Value;
                    string newRefreshToken = root["refresh_token"]?.Value;
                    int expiresIn = root["expires_in"]?.AsInt ?? 28800;

                    if (string.IsNullOrEmpty(newAccessToken))
                    {
                        Log.Warning("[RimMind] Token refresh returned no access token.");
                        return false;
                    }

                    cachedAccessToken = newAccessToken;
                    if (!string.IsNullOrEmpty(newRefreshToken))
                        cachedRefreshToken = newRefreshToken;
                    expiresAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (expiresIn * 1000L);

                    // Write updated credentials back to file
                    SaveCredentials();

                    Log.Message("[RimMind] Claude Code OAuth token refreshed successfully.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Token refresh failed: " + ex.Message);
                return false;
            }
        }

        private static void SaveCredentials()
        {
            try
            {
                string credPath = GetCredentialsPath();
                string json = File.ReadAllText(credPath, Encoding.UTF8);
                var root = JSONNode.Parse(json);
                var oauth = root["claudeAiOauth"];

                if (oauth == null || oauth.IsNull)
                    return;

                oauth["accessToken"] = cachedAccessToken;
                if (!string.IsNullOrEmpty(cachedRefreshToken))
                    oauth["refreshToken"] = cachedRefreshToken;
                oauth["expiresAt"] = expiresAtMs.ToString();

                File.WriteAllText(credPath, root.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimMind] Failed to save refreshed credentials: " + ex.Message);
            }
        }

        private static string GetCredentialsPath()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".claude", ".credentials.json");
        }
    }
}
