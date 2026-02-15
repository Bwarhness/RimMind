using System.Collections.Generic;

namespace RimMind.API
{
    public class ChatRequest
    {
        public string model;
        public List<ChatMessage> messages;
        public float temperature = 0.7f;
        public int max_tokens = 1024;
        public List<JSONNode> tools;

        public JSONNode ToJSON()
        {
            var obj = new JSONObject();
            obj["model"] = model;
            obj["temperature"] = temperature;
            obj["max_tokens"] = max_tokens;
            obj["stream"] = false;

            var msgArray = new JSONArray();
            foreach (var msg in messages)
                msgArray.Add(msg.ToJSON());
            obj["messages"] = msgArray;

            if (tools != null && tools.Count > 0)
            {
                var toolArray = new JSONArray();
                foreach (var tool in tools)
                    toolArray.Add(tool);
                obj["tools"] = toolArray;
                obj["tool_choice"] = "auto";
            }

            return obj;
        }
    }
}
