#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azathrix.UpmEditor.Editor.Core;
using UnityEngine;

namespace  Azathrix.UpmEditor.Editor.Services
{
    /// <summary>
    /// Service for reading and writing package.json files
    /// </summary>
    public static class PackageJsonService
    {
        /// <summary>
        /// Read package.json from directory
        /// </summary>
        public static UPMPackageData ReadPackageJson(string directoryPath)
        {
            var packageJsonPath = Path.Combine(Path.GetFullPath(directoryPath), UPMConstants.PackageJsonFileName);
            if (!File.Exists(packageJsonPath))
                return null;

            try
            {
                var json = File.ReadAllText(packageJsonPath);
                return ParsePackageJson(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to read package.json: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Write package.json to directory
        /// </summary>
        public static bool WritePackageJson(string directoryPath, UPMPackageData data)
        {
            try
            {
                var fullPath = Path.GetFullPath(directoryPath);
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                var packageJsonPath = Path.Combine(fullPath, UPMConstants.PackageJsonFileName);
                var json = SerializePackageJson(data);
                File.WriteAllText(packageJsonPath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to write package.json: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if package.json exists
        /// </summary>
        public static bool PackageJsonExists(string directoryPath)
        {
            var packageJsonPath = Path.Combine(Path.GetFullPath(directoryPath), UPMConstants.PackageJsonFileName);
            return File.Exists(packageJsonPath);
        }

        /// <summary>
        /// Create default package data
        /// </summary>
        public static UPMPackageData CreateDefaultPackageData(string packageName, string displayName)
        {
            return new UPMPackageData
            {
                name = packageName,
                displayName = displayName,
                version = UPMConstants.DefaultVersion,
                unity = UPMConstants.DefaultUnityVersion,
                description = "",
                keywords = new List<string>(),
                author = new UPMPackageData.AuthorInfo(),
                dependencies = new Dictionary<string, string>()
            };
        }

        /// <summary>
        /// Parse JSON string to UPMPackageData (simple parser without external dependencies)
        /// </summary>
        private static UPMPackageData ParsePackageJson(string json)
        {
            var data = new UPMPackageData();

            data.name = GetJsonStringValue(json, "name");
            data.displayName = GetJsonStringValue(json, "displayName");
            data.version = GetJsonStringValue(json, "version");
            data.unity = GetJsonStringValue(json, "unity");
            data.description = GetJsonStringValue(json, "description");
            data.documentationUrl = GetJsonStringValue(json, "documentationUrl");
            data.changelogUrl = GetJsonStringValue(json, "changelogUrl");
            data.licensesUrl = GetJsonStringValue(json, "licensesUrl");
            data.hideInEditor = GetJsonBoolValue(json, "hideInEditor");

            // Parse keywords array
            data.keywords = GetJsonStringArray(json, "keywords");

            // Parse author object
            var authorJson = GetJsonObject(json, "author");
            if (!string.IsNullOrEmpty(authorJson))
            {
                data.author.name = GetJsonStringValue(authorJson, "name");
                data.author.email = GetJsonStringValue(authorJson, "email");
                data.author.url = GetJsonStringValue(authorJson, "url");
            }

            // Parse dependencies object
            var depsJson = GetJsonObject(json, "dependencies");
            if (!string.IsNullOrEmpty(depsJson))
            {
                data.dependencies = ParseDependencies(depsJson);
            }

            return data;
        }

        /// <summary>
        /// Serialize UPMPackageData to JSON string
        /// </summary>
        private static string SerializePackageJson(UPMPackageData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // Required fields
            sb.AppendLine($"    \"name\": \"{EscapeJson(data.name)}\",");
            sb.AppendLine($"    \"displayName\": \"{EscapeJson(data.displayName)}\",");
            sb.AppendLine($"    \"version\": \"{EscapeJson(data.version)}\",");
            sb.AppendLine($"    \"unity\": \"{EscapeJson(data.unity)}\",");
            sb.AppendLine($"    \"description\": \"{EscapeJson(data.description)}\",");

            // Optional URL fields
            if (!string.IsNullOrEmpty(data.documentationUrl))
                sb.AppendLine($"    \"documentationUrl\": \"{EscapeJson(data.documentationUrl)}\",");
            if (!string.IsNullOrEmpty(data.changelogUrl))
                sb.AppendLine($"    \"changelogUrl\": \"{EscapeJson(data.changelogUrl)}\",");
            if (!string.IsNullOrEmpty(data.licensesUrl))
                sb.AppendLine($"    \"licensesUrl\": \"{EscapeJson(data.licensesUrl)}\",");

            // hideInEditor
            if (data.hideInEditor)
                sb.AppendLine("    \"hideInEditor\": true,");

            // Keywords
            if (data.keywords != null && data.keywords.Count > 0)
            {
                sb.AppendLine("    \"keywords\": [");
                for (int i = 0; i < data.keywords.Count; i++)
                {
                    var comma = i < data.keywords.Count - 1 ? "," : "";
                    sb.AppendLine($"        \"{EscapeJson(data.keywords[i])}\"{comma}");
                }
                sb.AppendLine("    ],");
            }

            // Author
            if (data.author != null && !string.IsNullOrEmpty(data.author.name))
            {
                sb.AppendLine("    \"author\": {");
                var authorFields = new List<string>();
                if (!string.IsNullOrEmpty(data.author.name))
                    authorFields.Add($"        \"name\": \"{EscapeJson(data.author.name)}\"");
                if (!string.IsNullOrEmpty(data.author.email))
                    authorFields.Add($"        \"email\": \"{EscapeJson(data.author.email)}\"");
                if (!string.IsNullOrEmpty(data.author.url))
                    authorFields.Add($"        \"url\": \"{EscapeJson(data.author.url)}\"");
                sb.AppendLine(string.Join(",\n", authorFields));
                sb.AppendLine("    },");
            }

            // Dependencies
            sb.AppendLine("    \"dependencies\": {");
            if (data.dependencies != null && data.dependencies.Count > 0)
            {
                var deps = new List<string>();
                foreach (var dep in data.dependencies)
                {
                    deps.Add($"        \"{EscapeJson(dep.Key)}\": \"{EscapeJson(dep.Value)}\"");
                }
                sb.AppendLine(string.Join(",\n", deps));
            }
            sb.AppendLine("    }");

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string GetJsonStringValue(string json, string key)
        {
            var pattern = $"\"{key}\"\\s*:\\s*\"";
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return "";

            var colonIdx = json.IndexOf(':', idx);
            if (colonIdx < 0) return "";

            var startQuote = json.IndexOf('"', colonIdx + 1);
            if (startQuote < 0) return "";

            var endQuote = startQuote + 1;
            while (endQuote < json.Length)
            {
                if (json[endQuote] == '"' && json[endQuote - 1] != '\\')
                    break;
                endQuote++;
            }

            if (endQuote >= json.Length) return "";
            return UnescapeJson(json.Substring(startQuote + 1, endQuote - startQuote - 1));
        }

        private static bool GetJsonBoolValue(string json, string key)
        {
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return false;

            var colonIdx = json.IndexOf(':', idx);
            if (colonIdx < 0) return false;

            var valueStart = colonIdx + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length) return false;
            return json.Substring(valueStart).TrimStart().StartsWith("true");
        }

        private static List<string> GetJsonStringArray(string json, string key)
        {
            var result = new List<string>();
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return result;

            var bracketStart = json.IndexOf('[', idx);
            if (bracketStart < 0) return result;

            var bracketEnd = json.IndexOf(']', bracketStart);
            if (bracketEnd < 0) return result;

            var arrayContent = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            var inString = false;
            var currentString = new StringBuilder();

            for (int i = 0; i < arrayContent.Length; i++)
            {
                var c = arrayContent[i];
                if (c == '"' && (i == 0 || arrayContent[i - 1] != '\\'))
                {
                    if (inString)
                    {
                        result.Add(UnescapeJson(currentString.ToString()));
                        currentString.Clear();
                    }
                    inString = !inString;
                }
                else if (inString)
                {
                    currentString.Append(c);
                }
            }

            return result;
        }

        private static string GetJsonObject(string json, string key)
        {
            var idx = json.IndexOf($"\"{key}\"");
            if (idx < 0) return "";

            var braceStart = json.IndexOf('{', idx);
            if (braceStart < 0) return "";

            var depth = 1;
            var braceEnd = braceStart + 1;
            while (braceEnd < json.Length && depth > 0)
            {
                if (json[braceEnd] == '{') depth++;
                else if (json[braceEnd] == '}') depth--;
                braceEnd++;
            }

            if (depth != 0) return "";
            return json.Substring(braceStart, braceEnd - braceStart);
        }

        private static Dictionary<string, string> ParseDependencies(string depsJson)
        {
            var result = new Dictionary<string, string>();
            var content = depsJson.Trim().TrimStart('{').TrimEnd('}');

            // 状态: 0=等待key, 1=读取key, 2=等待冒号, 3=等待value, 4=读取value
            int state = 0;
            var currentKey = new StringBuilder();
            var currentValue = new StringBuilder();

            for (int i = 0; i < content.Length; i++)
            {
                var c = content[i];
                switch (state)
                {
                    case 0: // 等待 key 的开始引号
                        if (c == '"') state = 1;
                        break;
                    case 1: // 读取 key
                        if (c == '"') state = 2;
                        else currentKey.Append(c);
                        break;
                    case 2: // 等待冒号
                        if (c == ':') state = 3;
                        break;
                    case 3: // 等待 value 的开始引号
                        if (c == '"') state = 4;
                        break;
                    case 4: // 读取 value
                        if (c == '"')
                        {
                            result[currentKey.ToString()] = currentValue.ToString();
                            currentKey.Clear();
                            currentValue.Clear();
                            state = 0;
                        }
                        else currentValue.Append(c);
                        break;
                }
            }

            return result;
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

            var result = new StringBuilder();
            foreach (var c in str)
            {
                if (c == '\\') result.Append("\\\\");
                else if (c == '"') result.Append("\\\"");
                else if (c == '\n') result.Append("\\n");
                else if (c == '\r') result.Append("\\r");
                else if (c == '\t') result.Append("\\t");
                else if (c > 127) result.Append($"\\u{(int)c:x4}");
                else result.Append(c);
            }
            return result.ToString();
        }

        private static string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

            var result = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == '\\' && i + 1 < str.Length)
                {
                    var next = str[i + 1];
                    if (next == 'n') { result.Append('\n'); i++; }
                    else if (next == 'r') { result.Append('\r'); i++; }
                    else if (next == 't') { result.Append('\t'); i++; }
                    else if (next == '"') { result.Append('"'); i++; }
                    else if (next == '\\') { result.Append('\\'); i++; }
                    else if (next == 'u' && i + 5 < str.Length)
                    {
                        var hex = str.Substring(i + 2, 4);
                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                        {
                            result.Append((char)code);
                            i += 5;
                        }
                        else
                        {
                            result.Append(str[i]);
                        }
                    }
                    else
                    {
                        result.Append(str[i]);
                    }
                }
                else
                {
                    result.Append(str[i]);
                }
            }
            return result.ToString();
        }
    }
}
#endif
