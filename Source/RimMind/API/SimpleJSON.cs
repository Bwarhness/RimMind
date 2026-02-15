/* * * * *
 * A simple JSON Parser / builder
 * ------------------------------
 *
 * It mainly has been written as a simple JSON parser. It can build a JSON string
 * from the node-tree, or generate a node tree from any valid JSON string.
 *
 * Written by Bunny83 (MIT License)
 * https://github.com/Bunny83/SimpleJSON
 *
 * Features / attributes:
 * - providesass JSONNode, JSONObject, JSONArray, JSONString, JSONNumber, JSONBool, JSONNull
 * - provides a hierarchical node tree
 * - provides enumerator interfaces
 * - provides serialization / deserialization to/from JSON string
 *
 * This is a stripped-down version for RimMind mod use.
 * * * * */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RimMind.API
{
    public enum JSONNodeType
    {
        Array = 1,
        Object = 2,
        String = 3,
        Number = 4,
        NullValue = 5,
        Boolean = 6,
        None = 7,
    }

    public abstract class JSONNode : IEnumerable
    {
        public virtual JSONNode this[int aIndex] { get { return null; } set { } }
        public virtual JSONNode this[string aKey] { get { return null; } set { } }
        public virtual string Value { get { return ""; } set { } }
        public virtual int Count { get { return 0; } }
        public virtual bool IsNumber { get { return false; } }
        public virtual bool IsString { get { return false; } }
        public virtual bool IsBoolean { get { return false; } }
        public virtual bool IsNull { get { return false; } }
        public virtual bool IsArray { get { return false; } }
        public virtual bool IsObject { get { return false; } }
        public virtual bool HasKey(string aKey) { return false; }
        public virtual JSONNode GetValueOrDefault(string aKey, JSONNode aDefault) { return aDefault; }

        public virtual void Add(string aKey, JSONNode aItem) { }
        public virtual void Add(JSONNode aItem) { Add("", aItem); }
        public virtual JSONNode Remove(string aKey) { return null; }
        public virtual JSONNode Remove(int aIndex) { return null; }
        public virtual JSONNode Remove(JSONNode aNode) { return aNode; }
        public virtual IEnumerable<JSONNode> Children { get { yield break; } }
        public IEnumerable<JSONNode> DeepChildren
        {
            get
            {
                foreach (var c in Children)
                {
                    foreach (var d in c.DeepChildren)
                        yield return d;
                }
            }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var c in Children)
                yield return c;
        }

        public override string ToString() { return ToString(false); }
        public virtual string ToString(bool aIndent) { return ToString(aIndent, 0); }
        internal abstract string ToString(bool aIndent, int aIndentLevel);

        public virtual double AsDouble
        {
            get
            {
                double v = 0.0;
                if (double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    return v;
                return 0.0;
            }
            set { Value = value.ToString(CultureInfo.InvariantCulture); }
        }
        public virtual int AsInt { get { return (int)AsDouble; } set { AsDouble = value; } }
        public virtual float AsFloat { get { return (float)AsDouble; } set { AsDouble = value; } }
        public virtual bool AsBool
        {
            get
            {
                bool v = false;
                if (bool.TryParse(Value, out v)) return v;
                return !string.IsNullOrEmpty(Value);
            }
            set { Value = value ? "true" : "false"; }
        }
        public virtual long AsLong { get { return (long)AsDouble; } set { AsDouble = value; } }

        public virtual JSONArray AsArray { get { return this as JSONArray; } }
        public virtual JSONObject AsObject { get { return this as JSONObject; } }

        public static implicit operator JSONNode(string s) { return new JSONString(s); }
        public static implicit operator string(JSONNode d) { return (d == null) ? null : d.Value; }
        public static implicit operator JSONNode(double n) { return new JSONNumber(n); }
        public static implicit operator double(JSONNode d) { return (d == null) ? 0 : d.AsDouble; }
        public static implicit operator JSONNode(float n) { return new JSONNumber(n); }
        public static implicit operator float(JSONNode d) { return (d == null) ? 0 : d.AsFloat; }
        public static implicit operator JSONNode(int n) { return new JSONNumber(n); }
        public static implicit operator int(JSONNode d) { return (d == null) ? 0 : d.AsInt; }
        public static implicit operator JSONNode(long n) { return new JSONNumber(n); }
        public static implicit operator long(JSONNode d) { return (d == null) ? 0L : d.AsLong; }
        public static implicit operator JSONNode(bool b) { return new JSONBool(b); }
        public static implicit operator bool(JSONNode d) { return (d == null) ? false : d.AsBool; }

        public static bool operator ==(JSONNode a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            bool aIsNull = a is JSONNull || ReferenceEquals(a, null);
            bool bIsNull = b is JSONNull || ReferenceEquals(b, null);
            if (aIsNull && bIsNull) return true;
            return !aIsNull && a.Equals(b);
        }
        public static bool operator !=(JSONNode a, object b) { return !(a == b); }
        public override bool Equals(object obj) { return ReferenceEquals(this, obj); }
        public override int GetHashCode() { return base.GetHashCode(); }

        public static JSONNode Parse(string aJSON)
        {
            var stack = new Stack<JSONNode>();
            JSONNode ctx = null;
            int i = 0;
            StringBuilder token = new StringBuilder();
            string tokenName = "";
            bool quoteMode = false;
            bool tokenIsQuoted = false;

            while (i < aJSON.Length)
            {
                switch (aJSON[i])
                {
                    case '{':
                        if (quoteMode)
                        {
                            token.Append(aJSON[i]);
                            break;
                        }
                        stack.Push(new JSONObject());
                        if (ctx != null)
                        {
                            ctx.Add(tokenName, stack.Peek());
                        }
                        tokenName = "";
                        token.Length = 0;
                        ctx = stack.Peek();
                        break;

                    case '[':
                        if (quoteMode)
                        {
                            token.Append(aJSON[i]);
                            break;
                        }
                        stack.Push(new JSONArray());
                        if (ctx != null)
                        {
                            ctx.Add(tokenName, stack.Peek());
                        }
                        tokenName = "";
                        token.Length = 0;
                        ctx = stack.Peek();
                        break;

                    case '}':
                    case ']':
                        if (quoteMode)
                        {
                            token.Append(aJSON[i]);
                            break;
                        }
                        if (stack.Count == 0)
                            throw new Exception("JSON Parse: Too many closing brackets");

                        stack.Pop();
                        if (token.Length > 0 || tokenIsQuoted)
                        {
                            ctx.Add(tokenName, ParseElement(token.ToString(), tokenIsQuoted));
                        }
                        tokenIsQuoted = false;
                        tokenName = "";
                        token.Length = 0;
                        if (stack.Count > 0)
                            ctx = stack.Peek();
                        break;

                    case ':':
                        if (quoteMode)
                        {
                            token.Append(aJSON[i]);
                            break;
                        }
                        tokenName = token.ToString();
                        token.Length = 0;
                        tokenIsQuoted = false;
                        break;

                    case '"':
                        quoteMode ^= true;
                        tokenIsQuoted |= quoteMode;
                        break;

                    case ',':
                        if (quoteMode)
                        {
                            token.Append(aJSON[i]);
                            break;
                        }
                        if (token.Length > 0 || tokenIsQuoted)
                        {
                            ctx.Add(tokenName, ParseElement(token.ToString(), tokenIsQuoted));
                        }
                        tokenIsQuoted = false;
                        tokenName = "";
                        token.Length = 0;
                        break;

                    case '\r':
                    case '\n':
                        break;

                    case ' ':
                    case '\t':
                        if (quoteMode)
                            token.Append(aJSON[i]);
                        break;

                    case '\\':
                        ++i;
                        if (quoteMode)
                        {
                            char c = aJSON[i];
                            switch (c)
                            {
                                case 't': token.Append('\t'); break;
                                case 'r': token.Append('\r'); break;
                                case 'n': token.Append('\n'); break;
                                case 'b': token.Append('\b'); break;
                                case 'f': token.Append('\f'); break;
                                case 'u':
                                    string s = aJSON.Substring(i + 1, 4);
                                    token.Append((char)int.Parse(s, NumberStyles.AllowHexSpecifier));
                                    i += 4;
                                    break;
                                default: token.Append(c); break;
                            }
                        }
                        break;

                    default:
                        token.Append(aJSON[i]);
                        break;
                }
                ++i;
            }
            if (quoteMode)
                throw new Exception("JSON Parse: Quotation marks seems to be messed up.");
            if (ctx == null)
                return ParseElement(token.ToString(), tokenIsQuoted);
            return ctx;
        }

        static JSONNode ParseElement(string token, bool quoted)
        {
            if (quoted) return token;
            string tmp = token.ToLower();
            if (tmp == "false" || tmp == "true")
                return tmp == "true";
            if (tmp == "null")
                return JSONNull.CreateOrGet();
            double val;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                return val;
            return token;
        }

        internal static string Escape(string aText)
        {
            var sb = new StringBuilder();
            foreach (char c in aText)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }

    public class JSONArray : JSONNode
    {
        private List<JSONNode> m_List = new List<JSONNode>();
        public override bool IsArray { get { return true; } }
        public override JSONNode this[int aIndex]
        {
            get { if (aIndex < 0 || aIndex >= m_List.Count) return JSONNull.CreateOrGet(); return m_List[aIndex]; }
            set { if (aIndex >= 0 && aIndex < m_List.Count) m_List[aIndex] = value; else m_List.Add(value); }
        }
        public override JSONNode this[string aKey] { get { return JSONNull.CreateOrGet(); } set { m_List.Add(value); } }
        public override int Count { get { return m_List.Count; } }
        public override void Add(string aKey, JSONNode aItem) { m_List.Add(aItem); }
        public override JSONNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_List.Count) return null;
            JSONNode tmp = m_List[aIndex];
            m_List.RemoveAt(aIndex);
            return tmp;
        }
        public override JSONNode Remove(JSONNode aNode) { m_List.Remove(aNode); return aNode; }
        public override IEnumerable<JSONNode> Children { get { foreach (var n in m_List) yield return n; } }

        internal override string ToString(bool aIndent, int aIndentLevel)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < m_List.Count; i++)
            {
                if (i > 0) sb.Append(',');
                if (aIndent) { sb.AppendLine(); sb.Append(new string(' ', (aIndentLevel + 1) * 2)); }
                sb.Append(m_List[i].ToString(aIndent, aIndentLevel + 1));
            }
            if (aIndent) { sb.AppendLine(); sb.Append(new string(' ', aIndentLevel * 2)); }
            sb.Append(']');
            return sb.ToString();
        }
    }

    public class JSONObject : JSONNode
    {
        private Dictionary<string, JSONNode> m_Dict = new Dictionary<string, JSONNode>();
        public override bool IsObject { get { return true; } }
        public override JSONNode this[string aKey]
        {
            get { return m_Dict.ContainsKey(aKey) ? m_Dict[aKey] : JSONNull.CreateOrGet(); }
            set
            {
                if (value == null) value = JSONNull.CreateOrGet();
                if (m_Dict.ContainsKey(aKey)) m_Dict[aKey] = value;
                else m_Dict.Add(aKey, value);
            }
        }
        public override JSONNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_Dict.Count) return null;
                return m_Dict.ElementAt(aIndex).Value;
            }
            set
            {
                if (aIndex < 0 || aIndex >= m_Dict.Count) return;
                string key = m_Dict.ElementAt(aIndex).Key;
                m_Dict[key] = value;
            }
        }
        public override int Count { get { return m_Dict.Count; } }
        public override bool HasKey(string aKey) { return m_Dict.ContainsKey(aKey); }
        public override JSONNode GetValueOrDefault(string aKey, JSONNode aDefault)
        {
            return m_Dict.ContainsKey(aKey) ? m_Dict[aKey] : aDefault;
        }
        public override void Add(string aKey, JSONNode aItem)
        {
            if (aItem == null) aItem = JSONNull.CreateOrGet();
            if (!string.IsNullOrEmpty(aKey))
            {
                if (m_Dict.ContainsKey(aKey)) m_Dict[aKey] = aItem;
                else m_Dict.Add(aKey, aItem);
            }
            else m_Dict.Add(Guid.NewGuid().ToString(), aItem);
        }
        public override JSONNode Remove(string aKey)
        {
            if (!m_Dict.ContainsKey(aKey)) return null;
            JSONNode tmp = m_Dict[aKey];
            m_Dict.Remove(aKey);
            return tmp;
        }
        public override JSONNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_Dict.Count) return null;
            var item = m_Dict.ElementAt(aIndex);
            m_Dict.Remove(item.Key);
            return item.Value;
        }
        public override JSONNode Remove(JSONNode aNode)
        {
            var item = m_Dict.Where(k => k.Value == aNode).FirstOrDefault();
            if (!string.IsNullOrEmpty(item.Key)) m_Dict.Remove(item.Key);
            return aNode;
        }
        public override IEnumerable<JSONNode> Children { get { foreach (var n in m_Dict) yield return n.Value; } }
        public IEnumerable<KeyValuePair<string, JSONNode>> Pairs { get { foreach (var n in m_Dict) yield return n; } }

        internal override string ToString(bool aIndent, int aIndentLevel)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var n in m_Dict)
            {
                if (!first) sb.Append(',');
                first = false;
                if (aIndent) { sb.AppendLine(); sb.Append(new string(' ', (aIndentLevel + 1) * 2)); }
                sb.Append('\"').Append(Escape(n.Key)).Append('\"');
                sb.Append(':');
                if (aIndent) sb.Append(' ');
                sb.Append(n.Value.ToString(aIndent, aIndentLevel + 1));
            }
            if (aIndent) { sb.AppendLine(); sb.Append(new string(' ', aIndentLevel * 2)); }
            sb.Append('}');
            return sb.ToString();
        }
    }

    public class JSONString : JSONNode
    {
        private string m_Data;
        public override bool IsString { get { return true; } }
        public override string Value { get { return m_Data; } set { m_Data = value; } }
        public JSONString(string aData) { m_Data = aData; }
        internal override string ToString(bool aIndent, int aIndentLevel)
        {
            return "\"" + Escape(m_Data) + "\"";
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            if (obj is string s) return m_Data == s;
            if (obj is JSONString js) return m_Data == js.m_Data;
            return false;
        }
        public override int GetHashCode() { return m_Data.GetHashCode(); }
    }

    public class JSONNumber : JSONNode
    {
        private double m_Data;
        public override bool IsNumber { get { return true; } }
        public override string Value { get { return m_Data.ToString(CultureInfo.InvariantCulture); } set { double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out m_Data); } }
        public override double AsDouble { get { return m_Data; } set { m_Data = value; } }
        public JSONNumber(double aData) { m_Data = aData; }
        internal override string ToString(bool aIndent, int aIndentLevel)
        {
            return Value;
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            if (obj is JSONNumber jn) return m_Data == jn.m_Data;
            return false;
        }
        public override int GetHashCode() { return m_Data.GetHashCode(); }
    }

    public class JSONBool : JSONNode
    {
        private bool m_Data;
        public override bool IsBoolean { get { return true; } }
        public override string Value { get { return m_Data.ToString().ToLower(); } set { bool.TryParse(value, out m_Data); } }
        public override bool AsBool { get { return m_Data; } set { m_Data = value; } }
        public JSONBool(bool aData) { m_Data = aData; }
        internal override string ToString(bool aIndent, int aIndentLevel)
        {
            return m_Data ? "true" : "false";
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj)) return true;
            if (obj is bool b) return m_Data == b;
            return false;
        }
        public override int GetHashCode() { return m_Data.GetHashCode(); }
    }

    public class JSONNull : JSONNode
    {
        static JSONNull m_StaticInstance = new JSONNull();
        public static JSONNull CreateOrGet() { return m_StaticInstance; }
        private JSONNull() { }
        public override bool IsNull { get { return true; } }
        public override string Value { get { return "null"; } set { } }
        public override bool AsBool { get { return false; } set { } }
        internal override string ToString(bool aIndent, int aIndentLevel)
        {
            return "null";
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return obj is JSONNull;
        }
        public override int GetHashCode() { return 0; }
    }
}
