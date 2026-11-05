// Tiny JSON reader/writer with no Unity dependency.
// Objects -> Dictionary<string, object>, arrays -> List<object>,
// numbers -> double, strings -> string, true/false -> bool, null -> null.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Refraction.Core
{
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWs();
            object v = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw p.Error("Unexpected trailing characters");
            return v;
        }

        public static string Serialize(object value, bool pretty = true)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        sealed class Parser
        {
            readonly string s; int i; int line = 1;
            public Parser(string s) { this.s = s; }
            public bool End => i >= s.Length;

            public FormatException Error(string msg) => new FormatException($"JSON error at line {line}: {msg}");

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == '\n') { line++; i++; }
                    else if (c == ' ' || c == '\t' || c == '\r' || c == '﻿') i++;
                    else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                    {   // allow // comments (handy for hand/AI-written level files)
                        while (i < s.Length && s[i] != '\n') i++;
                    }
                    else break;
                }
            }

            public object ReadValue()
            {
                if (End) throw Error("Unexpected end of input");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"Unexpected character '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error($"Expected '{word}'");
                i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                i++; SkipWs();
                if (!End && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (End || s[i] != '"') throw Error("Expected property name");
                    string key = ReadString();
                    SkipWs();
                    if (End || s[i] != ':') throw Error("Expected ':'");
                    i++; SkipWs();
                    d[key] = ReadValue();
                    SkipWs();
                    if (End) throw Error("Unterminated object");
                    if (s[i] == ',') { i++; SkipWs(); if (!End && s[i] == '}') { i++; return d; } continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Error("Expected ',' or '}'");
                }
            }

            List<object> ReadArray()
            {
                var list = new List<object>();
                i++; SkipWs();
                if (!End && s[i] == ']') { i++; return list; }
                while (true)
                {
                    SkipWs();
                    list.Add(ReadValue());
                    SkipWs();
                    if (End) throw Error("Unterminated array");
                    if (s[i] == ',') { i++; SkipWs(); if (!End && s[i] == ']') { i++; return list; } continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw Error("Expected ',' or ']'");
                }
            }

            string ReadString()
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (End) throw Error("Unterminated string");
                    char c = s[i++];
                    if (c == '"') break;
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("Bad escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("Bad unicode escape");
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4; break;
                        default: throw Error($"Bad escape '\\{e}'");
                    }
                }
                return sb.ToString();
            }

            double ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
                string num = s.Substring(start, i - start);
                if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    throw Error($"Bad number '{num}'");
                return v;
            }
        }

        static void Write(StringBuilder sb, object v, bool pretty, int indent)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string str: WriteString(sb, str); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> dict:
                    {
                        if (dict.Count == 0) { sb.Append("{}"); break; }
                        // Small, flat objects stay on one line for readability.
                        bool inline = !pretty || IsFlat(dict);
                        sb.Append('{');
                        int k = 0;
                        foreach (var kv in dict)
                        {
                            if (k++ > 0) sb.Append(inline ? ", " : ",");
                            if (!inline) NewLine(sb, indent + 1);
                            WriteString(sb, kv.Key);
                            sb.Append(inline ? ": " : ": ");
                            Write(sb, kv.Value, pretty, indent + 1);
                        }
                        if (!inline) NewLine(sb, indent);
                        sb.Append('}');
                        break;
                    }
                case System.Collections.IList list:
                    {
                        if (list.Count == 0) { sb.Append("[]"); break; }
                        bool inline = !pretty || IsScalarList(list);
                        sb.Append('[');
                        for (int k = 0; k < list.Count; k++)
                        {
                            if (k > 0) sb.Append(inline ? ", " : ",");
                            if (!inline) NewLine(sb, indent + 1);
                            Write(sb, list[k], pretty, indent + 1);
                        }
                        if (!inline) NewLine(sb, indent);
                        sb.Append(']');
                        break;
                    }
                default:
                    WriteString(sb, Convert.ToString(v, CultureInfo.InvariantCulture)); break;
            }
        }

        static bool IsFlat(IDictionary<string, object> d)
        {
            if (d.Count > 8) return false;
            foreach (var kv in d)
            {
                if (kv.Value is IDictionary<string, object>) return false;
                if (kv.Value is System.Collections.IList l && !IsScalarList(l)) return false;
            }
            return true;
        }

        static bool IsScalarList(System.Collections.IList l)
        {
            foreach (var o in l) if (o is IDictionary<string, object> || o is System.Collections.IList) return false;
            return true;
        }

        static void NewLine(StringBuilder sb, int indent)
        {
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        static void WriteString(StringBuilder sb, string str)
        {
            sb.Append('"');
            foreach (char c in str)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- convenience accessors ----
        public static int GetInt(this Dictionary<string, object> d, string key, int def)
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null)
            {
                if (v is double dd) return (int)Math.Round(dd);
                if (v is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            }
            return def;
        }

        public static double GetDouble(this Dictionary<string, object> d, string key, double def)
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null)
            {
                if (v is double dd) return dd;
                if (v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)) return n;
            }
            return def;
        }

        public static string GetString(this Dictionary<string, object> d, string key, string def)
        {
            if (d != null && d.TryGetValue(key, out var v) && v != null)
            {
                if (v is string s) return s;
                if (v is double dd) return dd.ToString(CultureInfo.InvariantCulture);
                if (v is bool b) return b ? "true" : "false";
            }
            return def;
        }

        public static bool GetBool(this Dictionary<string, object> d, string key, bool def)
        {
            if (d != null && d.TryGetValue(key, out var v) && v is bool b) return b;
            return def;
        }

        public static List<object> GetList(this Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out var v) && v is List<object> l) return l;
            return null;
        }

        public static Dictionary<string, object> GetObject(this Dictionary<string, object> d, string key)
        {
            if (d != null && d.TryGetValue(key, out var v) && v is Dictionary<string, object> o) return o;
            return null;
        }
    }
}
