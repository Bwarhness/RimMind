using System.Collections.Generic;

namespace RimMind.API
{
    public class ChatMessage
    {
        public string role;
        public string content;
        public string name;
        public string tool_call_id;
        public List<ToolCall> tool_calls;

        public ChatMessage() { }

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }

        public static ChatMessage System(string content) => new ChatMessage("system", content);
        public static ChatMessage User(string content) => new ChatMessage("user", content);
        public static ChatMessage Assistant(string content) => new ChatMessage("assistant", content);

        public static ChatMessage ToolResult(string toolCallId, string content)
        {
            return new ChatMessage
            {
                role = "tool",
                tool_call_id = toolCallId,
                content = content
            };
        }

        public JSONNode ToJSON()
        {
            var obj = new JSONObject();
            obj["role"] = role;

            if (content != null)
                obj["content"] = content;

            if (name != null)
                obj["name"] = name;

            if (tool_call_id != null)
                obj["tool_call_id"] = tool_call_id;

            if (tool_calls != null && tool_calls.Count > 0)
            {
                var arr = new JSONArray();
                foreach (var tc in tool_calls)
                    arr.Add(tc.ToJSON());
                obj["tool_calls"] = arr;
            }

            return obj;
        }

        public static ChatMessage FromJSON(JSONNode node)
        {
            var msg = new ChatMessage();
            msg.role = node["role"]?.Value;
            msg.content = node["content"]?.IsNull == true ? null : node["content"]?.Value;
            msg.name = node["name"]?.IsNull == true ? null : node["name"]?.Value;
            msg.tool_call_id = node["tool_call_id"]?.IsNull == true ? null : node["tool_call_id"]?.Value;

            if (node["tool_calls"] != null && node["tool_calls"].IsArray)
            {
                msg.tool_calls = new List<ToolCall>();
                foreach (JSONNode tc in node["tool_calls"].AsArray)
                {
                    msg.tool_calls.Add(ToolCall.FromJSON(tc));
                }
            }

            return msg;
        }
    }

    public class ToolCall
    {
        public string id;
        public string type = "function";
        public ToolCallFunction function;

        public JSONNode ToJSON()
        {
            var obj = new JSONObject();
            obj["id"] = id;
            obj["type"] = type;
            obj["function"] = function.ToJSON();
            return obj;
        }

        public static ToolCall FromJSON(JSONNode node)
        {
            return new ToolCall
            {
                id = node["id"]?.Value,
                type = node["type"]?.Value ?? "function",
                function = ToolCallFunction.FromJSON(node["function"])
            };
        }
    }

    public class ToolCallFunction
    {
        public string name;
        public string arguments;

        public JSONNode ToJSON()
        {
            var obj = new JSONObject();
            obj["name"] = name;
            obj["arguments"] = arguments;
            return obj;
        }

        public static ToolCallFunction FromJSON(JSONNode node)
        {
            return new ToolCallFunction
            {
                name = node["name"]?.Value,
                arguments = node["arguments"]?.Value
            };
        }
    }
}
