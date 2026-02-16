using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using RimMind.Core;
using Verse;

namespace RimMind.API
{
    public static class AnthropicClient
    {
        private const string API_URL = "https://api.anthropic.com/v1/messages";

        public static void SendAsync(ChatRequest request, Action<ChatResponse> callback)
        {
            string token = RimMindMod.Settings.anthropicToken;
            if (string.IsNullOrEmpty(token))
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    callback(new ChatResponse
                    {
                        success = false,
                        error = "No Anthropic API key configured. Set your API key in RimMind mod settings."
                    });
                });
                return;
            }

            string jsonBody = BuildAnthropicRequest(request);
            DebugLogger.LogRawRequest(jsonBody);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var httpRequest = (HttpWebRequest)WebRequest.Create(API_URL);
                    httpRequest.Method = "POST";
                    httpRequest.ContentType = "application/json";
                    httpRequest.Headers.Add("x-api-key", token);
                    httpRequest.Headers.Add("anthropic-version", "2023-06-01");
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
                        var response = ParseAnthropicResponse(responseJson);

                        MainThreadDispatcher.Enqueue(() => callback(response));
                    }
                }
                catch (WebException webEx)
                {
                    DebugLogger.LogAPIError("WebException: " + webEx.Message);
                    string errorMsg = "Network error: " + webEx.Message;

                    if (webEx.Response is HttpWebResponse errResponse)
                    {
                        int statusCode = (int)errResponse.StatusCode;
                        try
                        {
                            using (var reader = new StreamReader(errResponse.GetResponseStream(), Encoding.UTF8))
                            {
                                string errBody = reader.ReadToEnd();
                                DebugLogger.LogRawResponse("HTTP " + statusCode + " ERROR:\n" + errBody);
                                Log.Warning("[RimMind] Anthropic API error (HTTP " + statusCode + "): " + errBody);

                                var parsed = JSONNode.Parse(errBody);
                                var errNode = parsed["error"];
                                if (errNode != null && !errNode.IsNull)
                                {
                                    string msg = errNode["message"]?.Value;
                                    string type = errNode["type"]?.Value;
                                    if (!string.IsNullOrEmpty(msg))
                                        errorMsg = msg;
                                    if (!string.IsNullOrEmpty(type))
                                        errorMsg += " (" + type + ")";
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        Log.Warning("[RimMind] Anthropic network error: " + webEx.Message);
                    }

                    MainThreadDispatcher.Enqueue(() =>
                    {
                        callback(new ChatResponse { success = false, error = errorMsg });
                    });
                }
                catch (Exception ex)
                {
                    DebugLogger.LogAPIError("Exception: " + ex.Message);
                    Log.Warning("[RimMind] Anthropic API call failed: " + ex.Message);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        callback(new ChatResponse { success = false, error = "Request failed: " + ex.Message });
                    });
                }
            });
        }

        private static string BuildAnthropicRequest(ChatRequest request)
        {
            var obj = new JSONObject();

            // Model ID: strip "anthropic/" prefix if present
            string model = request.model;
            if (model.StartsWith("anthropic/"))
                model = model.Substring("anthropic/".Length);
            obj["model"] = model;
            obj["max_tokens"] = request.max_tokens;
            obj["temperature"] = request.temperature;
            obj["stream"] = false;

            // Extract system message
            string systemPrompt = null;
            var apiMessages = new List<ChatMessage>();
            foreach (var msg in request.messages)
            {
                if (msg.role == "system")
                    systemPrompt = msg.content;
                else
                    apiMessages.Add(msg);
            }

            if (systemPrompt != null)
                obj["system"] = systemPrompt;

            // Convert messages to Anthropic format with alternating roles
            var messagesArray = new JSONArray();
            JSONObject pendingUser = null;
            JSONArray pendingUserContent = null;

            foreach (var msg in apiMessages)
            {
                if (msg.role == "tool")
                {
                    // Tool results become user messages with tool_result content blocks
                    // Batch consecutive tool results into one user message
                    if (pendingUser == null)
                    {
                        pendingUser = new JSONObject();
                        pendingUser["role"] = "user";
                        pendingUserContent = new JSONArray();
                    }

                    var toolResult = new JSONObject();
                    toolResult["type"] = "tool_result";
                    toolResult["tool_use_id"] = msg.tool_call_id;
                    toolResult["content"] = msg.content ?? "";
                    pendingUserContent.Add(toolResult);
                }
                else
                {
                    // Flush any pending tool results
                    if (pendingUser != null)
                    {
                        pendingUser["content"] = pendingUserContent;
                        messagesArray.Add(pendingUser);
                        pendingUser = null;
                        pendingUserContent = null;
                    }

                    if (msg.role == "assistant" && msg.tool_calls != null && msg.tool_calls.Count > 0)
                    {
                        // Assistant message with tool calls → content array
                        var assistantObj = new JSONObject();
                        assistantObj["role"] = "assistant";
                        var contentArr = new JSONArray();

                        if (!string.IsNullOrEmpty(msg.content))
                        {
                            var textBlock = new JSONObject();
                            textBlock["type"] = "text";
                            textBlock["text"] = msg.content;
                            contentArr.Add(textBlock);
                        }

                        foreach (var tc in msg.tool_calls)
                        {
                            var toolUse = new JSONObject();
                            toolUse["type"] = "tool_use";
                            toolUse["id"] = tc.id;
                            toolUse["name"] = tc.function.name;

                            // Parse arguments string into JSON object
                            JSONNode inputNode = null;
                            if (!string.IsNullOrEmpty(tc.function.arguments))
                            {
                                try { inputNode = JSONNode.Parse(tc.function.arguments); }
                                catch { }
                            }
                            toolUse["input"] = inputNode ?? new JSONObject();
                            contentArr.Add(toolUse);
                        }

                        assistantObj["content"] = contentArr;
                        messagesArray.Add(assistantObj);
                    }
                    else if (msg.role == "user" || msg.role == "assistant")
                    {
                        var simpleMsg = new JSONObject();
                        simpleMsg["role"] = msg.role;
                        simpleMsg["content"] = msg.content ?? "";
                        messagesArray.Add(simpleMsg);
                    }
                }
            }

            // Flush any trailing tool results
            if (pendingUser != null)
            {
                pendingUser["content"] = pendingUserContent;
                messagesArray.Add(pendingUser);
            }

            obj["messages"] = messagesArray;

            // Convert tools from OpenAI format to Anthropic format
            if (request.tools != null && request.tools.Count > 0)
            {
                var toolArray = new JSONArray();
                foreach (var tool in request.tools)
                {
                    var anthropicTool = new JSONObject();
                    var fn = tool["function"];
                    if (fn != null)
                    {
                        anthropicTool["name"] = fn["name"]?.Value ?? "";
                        anthropicTool["description"] = fn["description"]?.Value ?? "";
                        if (fn["parameters"] != null)
                            anthropicTool["input_schema"] = fn["parameters"];
                        else
                            anthropicTool["input_schema"] = new JSONObject();
                    }
                    toolArray.Add(anthropicTool);
                }
                obj["tools"] = toolArray;
                // Anthropic uses tool_choice object, "auto" is the default behavior
                var toolChoice = new JSONObject();
                toolChoice["type"] = "auto";
                obj["tool_choice"] = toolChoice;
            }

            return obj.ToString();
        }

        private static ChatResponse ParseAnthropicResponse(string json)
        {
            try
            {
                var root = JSONNode.Parse(json);

                // Check for error
                if (root["error"] != null && !root["error"].IsNull)
                {
                    var errNode = root["error"];
                    string errorMsg = errNode["message"]?.Value ?? "Unknown error";
                    string errorType = errNode["type"]?.Value;
                    if (!string.IsNullOrEmpty(errorType))
                        errorMsg += " (" + errorType + ")";

                    return new ChatResponse { success = false, error = errorMsg };
                }

                var response = new ChatResponse
                {
                    success = true,
                    id = root["id"]?.Value,
                    model = root["model"]?.Value,
                    message = new ChatMessage { role = "assistant" }
                };

                // Parse usage (Anthropic format)
                ChatResponse.ParseUsageAnthropic(response, root);

                // Parse content array
                var content = root["content"];
                if (content != null && content.IsArray)
                {
                    string textContent = "";
                    var toolCalls = new List<ToolCall>();

                    foreach (JSONNode block in content.AsArray)
                    {
                        string blockType = block["type"]?.Value;

                        if (blockType == "text")
                        {
                            string text = block["text"]?.Value;
                            if (!string.IsNullOrEmpty(text))
                            {
                                if (textContent.Length > 0) textContent += "\n";
                                textContent += text;
                            }
                        }
                        else if (blockType == "tool_use")
                        {
                            var tc = new ToolCall
                            {
                                id = block["id"]?.Value,
                                type = "function",
                                function = new ToolCallFunction
                                {
                                    name = block["name"]?.Value,
                                    arguments = block["input"]?.ToString() ?? "{}"
                                }
                            };
                            toolCalls.Add(tc);
                        }
                    }

                    response.message.content = string.IsNullOrEmpty(textContent) ? null : textContent;

                    if (toolCalls.Count > 0)
                        response.message.tool_calls = toolCalls;
                }

                return response;
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    success = false,
                    error = "Failed to parse Anthropic response: " + ex.Message
                };
            }
        }
    }
}
