using System;
using System.IO;
using System.Text;

namespace LEDCountDown
{
    /// <summary>
    /// LED 显示程序配置文件，读写 config.json
    /// </summary>
    public class Config
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int WebPort { get; set; }
        public string MarqueeText { get; set; }
        public string LogoPath { get; set; }
        public int ClockFace { get; set; }

        /// <summary>配置文件路径（与 exe 同目录）</summary>
        private static string ConfigPath
        {
            get
            {
                string exeDir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
                return Path.Combine(exeDir, "config.json");
            }
        }

        // ── 默认值 ──
        private const int DEFAULT_WIDTH = 1500;
        private const int DEFAULT_HEIGHT = 190;
        private const int DEFAULT_WEBPORT = 8000;
        private const string DEFAULT_MARQUEE = "热烈欢迎，这里是LED显示程序";

        public Config()
        {
            Width = DEFAULT_WIDTH;
            Height = DEFAULT_HEIGHT;
            WebPort = DEFAULT_WEBPORT;
            MarqueeText = DEFAULT_MARQUEE;
            LogoPath = "";
            ClockFace = 0;
        }

        /// <summary>
        /// 加载配置，文件不存在则自动创建默认配置
        /// </summary>
        public static Config Load()
        {
            Config cfg = new Config();
            string path = ConfigPath;

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    cfg.ParseJson(json);
                }
                catch
                {
                    // 解析失败则用默认值
                }
            }
            else
            {
                // 不存在则创建默认配置
                cfg.Save();
            }

            return cfg;
        }

        /// <summary>
        /// 保存配置到 config.json
        /// </summary>
        public void Save()
        {
            string path = ConfigPath;
            string json = ToJson();
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        // ════════════════════════════════════════
        //  简易 JSON 序列化/反序列化（仅支持此简单结构）
        // ════════════════════════════════════════

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"width\": " + Width + ",");
            sb.AppendLine("  \"height\": " + Height + ",");
            sb.AppendLine("  \"webPort\": " + WebPort + ",");
            sb.AppendLine("  \"marqueeText\": " + EncodeString(MarqueeText) + ",");
            sb.AppendLine("  \"logoPath\": " + EncodeString(LogoPath) + ",");
            sb.AppendLine("  \"clockFace\": " + ClockFace);
            sb.AppendLine("}");
            return sb.ToString();
        }

        private void ParseJson(string json)
        {
            Width = ParseInt(json, "width", DEFAULT_WIDTH);
            Height = ParseInt(json, "height", DEFAULT_HEIGHT);
            WebPort = ParseInt(json, "webPort", DEFAULT_WEBPORT);
            MarqueeText = ParseString(json, "marqueeText", DEFAULT_MARQUEE);
            LogoPath = ParseString(json, "logoPath", "");
            ClockFace = ParseInt(json, "clockFace", 0);
        }

        private static int ParseInt(string json, string key, int defaultValue)
        {
            string val = ExtractValue(json, key);
            if (val == null) return defaultValue;
            int result;
            if (int.TryParse(val, out result))
                return result;
            return defaultValue;
        }

        private static string ParseString(string json, string key, string defaultValue)
        {
            string val = ExtractValue(json, key);
            if (val == null) return defaultValue;
            return DecodeString(val);
        }

        /// <summary>
        /// 提取 key 对应的 value 部分（不处理嵌套，仅适合扁平 JSON）
        /// </summary>
        private static string ExtractValue(string json, string key)
        {
            // 查找 "key":
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx += search.Length;

            // 跳过空格和冒号
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;
            if (idx < json.Length && json[idx] == ':')
                idx++;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length) return null;

            // 字符串值
            if (json[idx] == '"')
            {
                idx++; // 跳过开头引号
                int end = idx;
                while (end < json.Length && json[end] != '"')
                {
                    if (json[end] == '\\') end++; // 跳过转义
                    end++;
                }
                return json.Substring(idx, end - idx);
            }

            // 数字或其它值（读到逗号、换行、} 为止）
            int start = idx;
            while (idx < json.Length && json[idx] != ',' && json[idx] != '}' && json[idx] != '\r' && json[idx] != '\n')
                idx++;
            return json.Substring(start, idx - start).Trim();
        }

        private static string EncodeString(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string DecodeString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
