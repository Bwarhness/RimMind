using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using RimMind.Core;
using Verse;

namespace RimMind.API
{
    /// <summary>
    /// Client for custom OpenAI-compatible API providers.
    /// Supports local LLMs (Ollama, LM Studio, llama.cpp) and alternative providers (Groq, Together, OpenRouter).
    /// </summary>
    public static class CustomProviderClient
    {
        public static void SendAsync(ChatRequest request, Action<ChatResponse> callback)
        {
            string endpoint = RimMindMod.Settings.customEndpointUrl;
            string apiKey = RimMindMod.Settings.customApiKey;
            
            if (string.IsNullOrEmpty(endpoint))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    callback(new ChatResponse
                    {
                        success = false,
                        error = "No custom endpoint URL configured. Set your OpenAI-compatible endpoint in RimMind mod settings."
                    });
                });
                return;
            }

            if (string.IsNullOrEmpty(RimMindMod.Settings.customModelId))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    callback(new ChatResponse
                    {
                        success = false,
                        error = "No model ID configured for custom provider. Set the model name in RimMind mod settings."
                    });
                });
                return;
            }

            string jsonBody = request.ToJSON().ToString();
            DebugLogger.LogRawRequest(jsonBody);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    // Ensure endpoint ends with /chat/completions
                    string url = endpoint.TrimEnd('/');
                    if (!url.EndsWith("/chat/completions"))
                    {
                        url += "/chat/completions";
                    }

                    var httpRequest = (HttpWebRequest)WebRequest.Create(url);
                    httpRequest.Method = "POST";
                    httpRequest.ContentType = "application/json";
                    
                    // Add Authorization header if API key is provided (some local LLMs don't need it)
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        httpRequest.Headers.Add("Authorization", "Bearer " + apiKey);
                    }
                    
                    httpRequest.Timeout = 120000;
                    httpRequest.ReadWriteTimeout = 120000;

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
                        DebugLogger.LogRawResponse(responseJson);
                        var response = ChatResponse.FromJSON(responseJson);

                        MainThreadDispatcher.Enqueue(() => callback(response));
                    }
                }
                catch (WebException webEx)
                {
                    DebugLogger.LogAPIError("WebException: " + webEx.Message);
                    string errorMsg = "Network error: " + webEx.Message;
                    int statusCode = 0;

                    if (webEx.Response is HttpWebResponse errResponse)
                    {
                        statusCode = (int)errResponse.StatusCode;
                        try
                        {
                            using (var reader = new StreamReader(errResponse.GetResponseStream(), Encoding.UTF8))
                            {
                                string errBody = reader.ReadToEnd();
                                DebugLogger.LogRawResponse("HTTP " + statusCode + " ERROR:\n" + errBody);
                                Log.Warning("[RimMind] Custom provider API error (HTTP " + statusCode + "): " + errBody);

                                var parsed = ChatResponse.FromJSON(errBody);
                                if (!string.IsNullOrEmpty(parsed.error))
                                    errorMsg = parsed.error;
                            }
                        }
                        catch { }

                        // Add helpful hint for 400 errors (often caused by unsupported tool/function calling)
                        if (statusCode == 400 && RimMindMod.Settings.customProviderSupportsTools)
                        {
                            errorMsg += "\n\n[Hint] If your provider doesn't support function/tool calling, disable 'Supports tool calling' in RimMind mod settings.";
                        }
                    }
                    else
                    {
                        Log.Warning("[RimMind] Custom provider network error: " + webEx.Message);
                    }

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        callback(new ChatResponse { success = false, error = errorMsg });
                    });
                }
                catch (Exception ex)
                {
                    DebugLogger.LogAPIError("Exception: " + ex.Message);
                    Log.Warning("[RimMind] Custom provider API call failed: " + ex.Message);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        callback(new ChatResponse { success = false, error = "Request failed: " + ex.Message });
                    });
                }
            });
        }
    }
}
