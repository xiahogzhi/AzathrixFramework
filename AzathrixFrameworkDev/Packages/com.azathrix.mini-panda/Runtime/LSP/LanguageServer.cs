using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Azathrix.MiniPanda.LSP
{
    /// <summary>
    /// MiniPanda LSP 服务器
    /// </summary>
    public class LanguageServer : IDisposable
    {
        private readonly LanguageService _service;
        private TcpListener _listener;
        private Thread _listenThread;
        private bool _running;
        private int _seq = 1;

        public int Port { get; private set; }
        public bool IsRunning => _running;

        public LanguageServer()
        {
            _service = new LanguageService();
        }

        /// <summary>
        /// 启动 LSP 服务器
        /// </summary>
        public void Start(int port = 4712)
        {
            if (_running) return;

            Port = port;
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _running = true;

            _listenThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "MiniPanda LSP Server"
            };
            _listenThread.Start();

            UnityEngine.Debug.Log($"[MiniPanda] LSP server started on port {port}");
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;
            _listener?.Stop();
            _listenThread?.Join(1000);

            UnityEngine.Debug.Log("[MiniPanda] LSP server stopped");
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    if (_listener.Pending())
                    {
                        var client = _listener.AcceptTcpClient();
                        HandleClient(client);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[MiniPanda] LSP server error: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            UnityEngine.Debug.Log("[MiniPanda] LSP client connected");

            var thread = new Thread(() =>
            {
                try
                {
                    var stream = client.GetStream();
                    stream.ReadTimeout = -1; // 无限等待
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                    while (_running && client.Connected)
                    {
                        var message = ReadMessage(stream);
                        if (message == null) break;

                        var response = HandleMessage(message);
                        if (response != null)
                        {
                            SendMessage(writer, response);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[MiniPanda] LSP client error: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    client.Close();
                    UnityEngine.Debug.Log("[MiniPanda] LSP client disconnected");
                }
            })
            {
                IsBackground = true,
                Name = "MiniPanda LSP Client Handler"
            };
            thread.Start();
        }

        private Dictionary<string, object> ReadMessage(NetworkStream stream)
        {
            try
            {
                var contentLength = 0;

                // 读取头部（按字节读取，直到遇到 \r\n\r\n）
                var headerBuilder = new StringBuilder();
                var prevBytes = new byte[4];

                while (true)
                {
                    var b = stream.ReadByte();
                    if (b == -1) return null; // 连接关闭

                    headerBuilder.Append((char)b);

                    // 检查是否遇到 \r\n\r\n（头部结束）
                    prevBytes[0] = prevBytes[1];
                    prevBytes[1] = prevBytes[2];
                    prevBytes[2] = prevBytes[3];
                    prevBytes[3] = (byte)b;

                    if (prevBytes[0] == '\r' && prevBytes[1] == '\n' &&
                        prevBytes[2] == '\r' && prevBytes[3] == '\n')
                    {
                        break;
                    }
                }

                // 解析 Content-Length
                var headers = headerBuilder.ToString();
                foreach (var line in headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentLength = int.Parse(line.Substring(15).Trim());
                    }
                }

                if (contentLength == 0)
                {
                    UnityEngine.Debug.Log("[MiniPanda] LSP: No Content-Length");
                    return null;
                }

                // 读取 JSON 正文（按字节数读取）
                var bodyBytes = new byte[contentLength];
                var read = 0;
                while (read < contentLength)
                {
                    var n = stream.Read(bodyBytes, read, contentLength - read);
                    if (n == 0) return null;
                    read += n;
                }

                var json = Encoding.UTF8.GetString(bodyBytes);
                return ParseJson(json);
            }
            catch (IOException)
            {
                return null;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[MiniPanda] LSP: ReadMessage error: {ex.Message}");
                return null;
            }
        }

        private void SendMessage(StreamWriter writer, Dictionary<string, object> message)
        {
            var json = SerializeJson(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            writer.Write($"Content-Length: {bytes.Length}\r\n\r\n");
            writer.Write(json);
            writer.Flush();
        }

        private Dictionary<string, object> HandleMessage(Dictionary<string, object> message)
        {
            var method = message.ContainsKey("method") ? message["method"]?.ToString() : null;
            var id = message.ContainsKey("id") ? message["id"] : null;
            var @params = message.ContainsKey("params") ? message["params"] as Dictionary<string, object> : null;

            object result = null;

            switch (method)
            {
                case "initialize":
                    result = HandleInitialize(@params);
                    break;
                case "initialized":
                    return null; // 通知，无需响应
                case "shutdown":
                    result = null;
                    break;
                case "exit":
                    Stop();
                    return null;
                case "textDocument/didOpen":
                    HandleDidOpen(@params);
                    return null;
                case "textDocument/didChange":
                    HandleDidChange(@params);
                    return null;
                case "textDocument/didClose":
                    HandleDidClose(@params);
                    return null;
                case "textDocument/completion":
                    result = HandleCompletion(@params);
                    break;
                case "textDocument/hover":
                    result = HandleHover(@params);
                    break;
                case "textDocument/definition":
                    result = HandleDefinition(@params);
                    break;
                case "textDocument/documentSymbol":
                    result = HandleDocumentSymbol(@params);
                    break;
                case "textDocument/signatureHelp":
                    result = HandleSignatureHelp(@params);
                    break;
                default:
                    if (id != null)
                    {
                        return CreateErrorResponse(id, -32601, $"Method not found: {method}");
                    }
                    return null;
            }

            if (id != null)
            {
                return CreateResponse(id, result);
            }
            return null;
        }

        #region 请求处理

        private object HandleInitialize(Dictionary<string, object> @params)
        {
            return new Dictionary<string, object>
            {
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["textDocumentSync"] = 1, // Full sync
                    ["completionProvider"] = new Dictionary<string, object>
                    {
                        ["triggerCharacters"] = new[] { ".", "(" }
                    },
                    ["hoverProvider"] = true,
                    ["definitionProvider"] = true,
                    ["documentSymbolProvider"] = true,
                    ["signatureHelpProvider"] = new Dictionary<string, object>
                    {
                        ["triggerCharacters"] = new[] { "(", "," }
                    }
                },
                ["serverInfo"] = new Dictionary<string, object>
                {
                    ["name"] = "MiniPanda Language Server",
                    ["version"] = "1.0.0"
                }
            };
        }

        private void HandleDidOpen(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();
            var text = textDocument?["text"]?.ToString();
            if (uri != null && text != null)
            {
                _service.OpenDocument(uri, text);
            }
        }

        private void HandleDidChange(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();
            var contentChanges = @params?["contentChanges"] as object[];
            if (uri != null && contentChanges?.Length > 0)
            {
                var change = contentChanges[0] as Dictionary<string, object>;
                var text = change?["text"]?.ToString();
                if (text != null)
                {
                    _service.UpdateDocument(uri, text);
                }
            }
        }

        private void HandleDidClose(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();
            if (uri != null)
            {
                _service.CloseDocument(uri);
            }
        }

        private object HandleCompletion(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var position = @params?["position"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null || position == null) return new List<object>();

            var pos = new Position(
                Convert.ToInt32(position["line"]),
                Convert.ToInt32(position["character"])
            );

            var items = _service.GetCompletions(uri, pos);
            return items.ConvertAll(item => new Dictionary<string, object>
            {
                ["label"] = item.Label,
                ["kind"] = (int)item.Kind,
                ["detail"] = item.Detail,
                ["documentation"] = item.Documentation,
                ["insertText"] = item.InsertText ?? item.Label
            });
        }

        private object HandleHover(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var position = @params?["position"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null || position == null) return null;

            var pos = new Position(
                Convert.ToInt32(position["line"]),
                Convert.ToInt32(position["character"])
            );

            var hover = _service.GetHover(uri, pos);
            if (hover == null) return null;

            return new Dictionary<string, object>
            {
                ["contents"] = new Dictionary<string, object>
                {
                    ["kind"] = "markdown",
                    ["value"] = hover.Contents
                }
            };
        }

        private object HandleDefinition(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var position = @params?["position"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null || position == null) return null;

            var pos = new Position(
                Convert.ToInt32(position["line"]),
                Convert.ToInt32(position["character"])
            );

            var location = _service.GetDefinition(uri, pos);
            if (location == null) return null;

            return new Dictionary<string, object>
            {
                ["uri"] = location.Value.Uri,
                ["range"] = RangeToDict(location.Value.Range)
            };
        }

        private object HandleDocumentSymbol(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null) return new List<object>();

            var symbols = _service.GetDocumentSymbols(uri);
            return symbols.ConvertAll(SymbolToDict);
        }

        private object HandleSignatureHelp(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var position = @params?["position"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null || position == null) return null;

            var pos = new Position(
                Convert.ToInt32(position["line"]),
                Convert.ToInt32(position["character"])
            );

            var help = _service.GetSignatureHelp(uri, pos);
            if (help == null) return null;

            return new Dictionary<string, object>
            {
                ["signatures"] = help.Signatures.ConvertAll(sig => new Dictionary<string, object>
                {
                    ["label"] = sig.Label,
                    ["documentation"] = sig.Documentation,
                    ["parameters"] = sig.Parameters.ConvertAll(p => new Dictionary<string, object>
                    {
                        ["label"] = p.Label,
                        ["documentation"] = p.Documentation
                    })
                }),
                ["activeSignature"] = help.ActiveSignature,
                ["activeParameter"] = help.ActiveParameter
            };
        }

        #endregion

        #region 辅助方法

        private Dictionary<string, object> CreateResponse(object id, object result)
        {
            return new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result
            };
        }

        private Dictionary<string, object> CreateErrorResponse(object id, int code, string message)
        {
            return new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["message"] = message
                }
            };
        }

        private Dictionary<string, object> RangeToDict(Range range)
        {
            return new Dictionary<string, object>
            {
                ["start"] = new Dictionary<string, object>
                {
                    ["line"] = range.Start.Line,
                    ["character"] = range.Start.Character
                },
                ["end"] = new Dictionary<string, object>
                {
                    ["line"] = range.End.Line,
                    ["character"] = range.End.Character
                }
            };
        }

        private Dictionary<string, object> SymbolToDict(DocumentSymbol symbol)
        {
            var dict = new Dictionary<string, object>
            {
                ["name"] = symbol.Name,
                ["kind"] = (int)symbol.Kind,
                ["range"] = RangeToDict(symbol.Range),
                ["selectionRange"] = RangeToDict(symbol.SelectionRange)
            };

            if (!string.IsNullOrEmpty(symbol.Detail))
                dict["detail"] = symbol.Detail;

            if (symbol.Children?.Count > 0)
                dict["children"] = symbol.Children.ConvertAll(SymbolToDict);

            return dict;
        }

        // JSON 解析
        private Dictionary<string, object> ParseJson(string json)
        {
            var index = 0;
            return ParseObject(json, ref index);
        }

        private Dictionary<string, object> ParseObject(string json, ref int index)
        {
            var dict = new Dictionary<string, object>();
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{') return dict;
            index++; // skip '{'

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == '}') { index++; break; }

                // Parse key
                var key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':') break;
                index++; // skip ':'

                // Parse value
                var value = ParseValue(json, ref index);
                dict[key] = value;

                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }
            return dict;
        }

        private object ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            var c = json[index];
            if (c == '"') return ParseString(json, ref index);
            if (c == '{') return ParseObject(json, ref index);
            if (c == '[') return ParseArray(json, ref index);
            if (c == 't') { index += 4; return true; }
            if (c == 'f') { index += 5; return false; }
            if (c == 'n') { index += 4; return null; }
            if (c == '-' || char.IsDigit(c)) return ParseNumber(json, ref index);
            return null;
        }

        private string ParseString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"') return "";
            index++; // skip '"'
            var sb = new StringBuilder();
            while (index < json.Length && json[index] != '"')
            {
                if (json[index] == '\\' && index + 1 < json.Length)
                {
                    index++;
                    switch (json[index])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        default: sb.Append(json[index]); break;
                    }
                }
                else
                {
                    sb.Append(json[index]);
                }
                index++;
            }
            if (index < json.Length) index++; // skip closing '"'
            return sb.ToString();
        }

        private object[] ParseArray(string json, ref int index)
        {
            var list = new List<object>();
            if (index >= json.Length || json[index] != '[') return list.ToArray();
            index++; // skip '['

            while (index < json.Length)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] == ']') { index++; break; }
                list.Add(ParseValue(json, ref index));
                SkipWhitespace(json, ref index);
                if (index < json.Length && json[index] == ',') index++;
            }
            return list.ToArray();
        }

        private object ParseNumber(string json, ref int index)
        {
            var start = index;
            if (json[index] == '-') index++;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || json[index] == '+' || json[index] == '-'))
                index++;
            var numStr = json.Substring(start, index - start);
            if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
                return double.TryParse(numStr, out var d) ? d : 0.0;
            return int.TryParse(numStr, out var i) ? i : 0;
        }

        private void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
        }

        private string SerializeJson(Dictionary<string, object> obj)
        {
            // 简化实现
            var sb = new StringBuilder();
            SerializeValue(sb, obj);
            return sb.ToString();
        }

        private void SerializeValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case int i:
                    sb.Append(i);
                    break;
                case long l:
                    sb.Append(l);
                    break;
                case double d:
                    sb.Append(d);
                    break;
                case string s:
                    sb.Append('"');
                    sb.Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r"));
                    sb.Append('"');
                    break;
                case Dictionary<string, object> dict:
                    sb.Append('{');
                    var first = true;
                    foreach (var kv in dict)
                    {
                        // result 字段必须保留（即使是 null），其他 null 值跳过
                        if (kv.Value == null && kv.Key != "result") continue;
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('"');
                        sb.Append(kv.Key);
                        sb.Append("\":");
                        SerializeValue(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IList list:
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        SerializeValue(sb, list[i]);
                    }
                    sb.Append(']');
                    break;
                default:
                    sb.Append('"');
                    sb.Append(value.ToString());
                    sb.Append('"');
                    break;
            }
        }

        #endregion

        public void Dispose()
        {
            Stop();
        }
    }
}
