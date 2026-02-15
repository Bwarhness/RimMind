using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using RimMind.Core;
using Verse;

namespace RimMind.API
{
    public static class OpenRouterClient
    {
        private const string API_URL = "https://openrouter.ai/api/v1/chat/completions";

        public static void SendAsync(ChatRequest request, Action<ChatResponse> callback)
        {
            string apiKey = RimMindMod.Settings.apiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    callback(new ChatResponse
                    {
                        success = false,
                        error = "No API key configured. Set your OpenRouter API key in RimMind mod settings."
                    });
                });
                return;
            }

            string jsonBody = request.ToJSON().ToString();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var httpRequest = (HttpWebRequest)WebRequest.Create(API_URL);
                    httpRequest.Method = "POST";
                    httpRequest.ContentType = "application/json";
                    httpRequest.Headers.Add("Authorization", "Bearer " + apiKey);
                    httpRequest.Headers.Add("HTTP-Referer", "https://github.com/rimmind");
                    httpRequest.Headers.Add("X-Title", "RimMind");
                    httpRequest.Timeout = 60000;
                    httpRequest.ReadWriteTimeout = 60000;

                    byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                    httpRequest.ContentLength = bodyBytes.Length;

                    using (var stream = httpRequest.GetRequestStream())
                    {
                        stream.Write(bodyBytes, 0, bodyBytes.Length);
                    }

                    using (var httpResponse = (HttpWebResponse)httpRequest.GetResponse())
                    using (var reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                    {
                        string responseJson = reader.ReadToEnd();
                        var response = ChatResponse.FromJSON(responseJson);

                        MainThreadDispatcher.Enqueue(() => callback(response));
                    }
                }
                catch (WebException webEx)
                {
                    string errorMsg = "Network error: " + webEx.Message;
                    if (webEx.Response is HttpWebResponse errResponse)
                    {
                        try
                        {
                            using (var reader = new StreamReader(errResponse.GetResponseStream(), Encoding.UTF8))
                            {
                                string errBody = reader.ReadToEnd();
                                var parsed = ChatResponse.FromJSON(errBody);
                                if (!string.IsNullOrEmpty(parsed.error))
                                    errorMsg = parsed.error;
                            }
                        }
                        catch { }
                    }

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        callback(new ChatResponse { success = false, error = errorMsg });
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning("[RimMind] API call failed: " + ex.Message);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        callback(new ChatResponse { success = false, error = "Request failed: " + ex.Message });
                    });
                }
            });
        }
    }
}
