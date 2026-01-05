using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new ConcurrentDictionary<int, TcpClient>();
        private int _nextClientId;

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
            foreach (var kv in _clients)
            {
                try { kv.Value.Close(); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[MiniPanda] LSP: Close client error: {ex.Message}"); }
            }
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

            var clientId = Interlocked.Increment(ref _nextClientId);
            _clients[clientId] = client;

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

                        var responses = HandleMessage(message);
                        foreach (var response in responses)
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
                    _clients.TryRemove(clientId, out _);
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
                        if (contentLength > 10 * 1024 * 1024) // 10MB 上限
                            return null;
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

        private List<Dictionary<string, object>> HandleMessage(Dictionary<string, object> message)
        {
            var responses = new List<Dictionary<string, object>>();
            var method = message.ContainsKey("method") ? message["method"]?.ToString() : null;
            var id = message.ContainsKey("id") ? message["id"] : null;
            var @params = message.ContainsKey("params") ? message["params"] as Dictionary<string, object> : null;

            object result = null;
            string diagnosticUri = null;

            switch (method)
            {
                case "initialize":
                    result = HandleInitialize(@params);
                    break;
                case "initialized":
                    return responses;
                case "shutdown":
                    result = null;
                    break;
                case "exit":
                    Stop();
                    return responses;
                case "textDocument/didOpen":
                    diagnosticUri = HandleDidOpen(@params);
                    break;
                case "textDocument/didChange":
                    diagnosticUri = HandleDidChange(@params);
                    break;
                case "textDocument/didClose":
                    HandleDidClose(@params);
                    return responses;
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
                case "textDocument/formatting":
                    result = HandleFormatting(@params);
                    break;
                case "textDocument/rename":
                    result = HandleRename(@params);
                    break;
                default:
                    if (id != null)
                    {
                        responses.Add(CreateErrorResponse(id, -32601, $"Method not found: {method}"));
                    }
                    return responses;
            }

            if (id != null)
            {
                responses.Add(CreateResponse(id, result));
            }

            // 发送诊断通知
            if (diagnosticUri != null)
            {
                responses.Add(CreateDiagnosticsNotification(diagnosticUri));
            }

            return responses;
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
                    },
                    ["documentFormattingProvider"] = true,
                    ["renameProvider"] = true
                },
                ["serverInfo"] = new Dictionary<string, object>
                {
                    ["name"] = "MiniPanda Language Server",
                    ["version"] = "1.0.0"
                }
            };
        }

        private string HandleDidOpen(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();
            var text = textDocument?["text"]?.ToString();
            if (uri != null && text != null)
            {
                _service.OpenDocument(uri, text);
                return uri;
            }
            return null;
        }

        private string HandleDidChange(Dictionary<string, object> @params)
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
                    return uri;
                }
            }
            return null;
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

        private object HandleFormatting(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var options = @params?["options"] as Dictionary<string, object>;
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null) return new List<object>();

            var tabSize = 4;
            var insertSpaces = true;
            if (options != null)
            {
                if (options.TryGetValue("tabSize", out var ts))
                    tabSize = Convert.ToInt32(ts);
                if (options.TryGetValue("insertSpaces", out var ins))
                    insertSpaces = Convert.ToBoolean(ins);
            }

            var edits = _service.FormatDocument(uri, tabSize, insertSpaces);
            return edits.ConvertAll(e => new Dictionary<string, object>
            {
                ["range"] = RangeToDict(e.Range),
                ["newText"] = e.NewText
            });
        }

        private object HandleRename(Dictionary<string, object> @params)
        {
            var textDocument = @params?["textDocument"] as Dictionary<string, object>;
            var position = @params?["position"] as Dictionary<string, object>;
            var newName = @params?["newName"]?.ToString();
            var uri = textDocument?["uri"]?.ToString();

            if (uri == null || position == null || string.IsNullOrEmpty(newName)) return null;

            var pos = new Position(
                Convert.ToInt32(position["line"]),
                Convert.ToInt32(position["character"])
            );

            var edit = _service.Rename(uri, pos, newName);
            if (edit.Changes.Count == 0) return null;

            var changes = new Dictionary<string, object>();
            foreach (var kv in edit.Changes)
            {
                changes[kv.Key] = kv.Value.ConvertAll(e => new Dictionary<string, object>
                {
                    ["range"] = RangeToDict(e.Range),
                    ["newText"] = e.NewText
                });
            }

            return new Dictionary<string, object> { ["changes"] = changes };
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

        private Dictionary<string, object> CreateDiagnosticsNotification(string uri)
        {
            var diagnostics = _service.GetDiagnostics(uri);
            return new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "textDocument/publishDiagnostics",
                ["params"] = new Dictionary<string, object>
                {
                    ["uri"] = uri,
                    ["diagnostics"] = diagnostics.ConvertAll(d => new Dictionary<string, object>
                    {
                        ["range"] = RangeToDict(d.Range),
                        ["severity"] = (int)d.Severity,
                        ["source"] = d.Source,
                        ["message"] = d.Message
                    })
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
            if (string.IsNullOrEmpty(json)) return new Dictionary<string, object>();
            var token = JToken.Parse(json);
            return ToPlainObject(token) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private string SerializeJson(Dictionary<string, object> obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        private static object ToPlainObject(JToken token)
        {
            if (token == null) return null;

            switch (token.Type)
            {
                case JTokenType.Object:
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in ((JObject)token).Properties())
                    {
                        dict[prop.Name] = ToPlainObject(prop.Value);
                    }
                    return dict;
                }
                case JTokenType.Array:
                {
                    var list = new List<object>();
                    foreach (var item in (JArray)token)
                    {
                        list.Add(ToPlainObject(item));
                    }
                    return list.ToArray();
                }
                case JTokenType.Integer:
                    return token.Value<long>();
                case JTokenType.Float:
                    return token.Value<double>();
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.String:
                    return token.Value<string>();
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;
                default:
                    return token.ToString();
            }
        }

        #endregion

        public void Dispose()
        {
            Stop();
        }
    }
}













