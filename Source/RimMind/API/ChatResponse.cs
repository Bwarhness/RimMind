using System.Collections.Generic;

namespace RimMind.API
{
    public class ChatResponse
    {
        public string id;
        public string model;
        public ChatMessage message;
        public string error;
        public bool success;

        public bool HasToolCalls => message?.tool_calls != null && message.tool_calls.Count > 0;

        public static ChatResponse FromJSON(string json)
        {
            try
            {
                var root = JSONNode.Parse(json);

                // Check for error
                if (root["error"] != null && !root["error"].IsNull)
                {
                    var errorNode = root["error"];
                    string errorMsg;

                    if (errorNode.IsString)
                    {
                        errorMsg = errorNode.Value;
                    }
                    else
                    {
                        string message = errorNode["message"]?.Value ?? "Unknown error";
                        string code = errorNode["code"]?.Value;
                        string provider = errorNode["metadata"]?["provider_name"]?.Value;

                        errorMsg = message;
                        if (!string.IsNullOrEmpty(code))
                            errorMsg += " (code: " + code + ")";
                        if (!string.IsNullOrEmpty(provider))
                            errorMsg += " [" + provider + "]";
                    }

                    return new ChatResponse
                    {
                        success = false,
                        error = errorMsg
                    };
                }

                var response = new ChatResponse
                {
                    success = true,
                    id = root["id"]?.Value,
                    model = root["model"]?.Value
                };

                var choices = root["choices"];
                if (choices != null && choices.IsArray && choices.Count > 0)
                {
                    var msgNode = choices[0]["message"];
                    if (msgNode != null)
                    {
                        response.message = ChatMessage.FromJSON(msgNode);
                    }
                }

                return response;
            }
            catch (System.Exception ex)
            {
                return new ChatResponse
                {
                    success = false,
                    error = "Failed to parse response: " + ex.Message
                };
            }
        }
    }
}
