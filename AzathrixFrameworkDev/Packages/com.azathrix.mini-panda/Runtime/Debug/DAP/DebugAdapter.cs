using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Debug.DAP
{
    /// <summary>
    /// MiniPanda DAP 调试适配器
    /// </summary>
    public class DebugAdapter
    {
        private readonly VirtualMachine _vm;
        private readonly Debugger _debugger;
        private readonly Stream _input;
        private readonly Stream _output;
        private int _seq = 1;
        private bool _running;
        private string _programPath;
        private readonly System.Threading.ManualResetEvent _configurationDoneEvent = new System.Threading.ManualResetEvent(false);
        private readonly System.Threading.ManualResetEvent _launchEvent = new System.Threading.ManualResetEvent(false);
        private readonly System.Threading.ManualResetEvent _breakpointsSetEvent = new System.Threading.ManualResetEvent(false);

        // 变量引用管理
        private int _nextVarRef = 1;
        private readonly Dictionary<int, object> _varRefs = new Dictionary<int, object>();
        private readonly Dictionary<int, int> _frameIdToIndex = new Dictionary<int, int>();

        public DebugAdapter(VirtualMachine vm, Stream input, Stream output)
        {
            _vm = vm;
            _debugger = new Debugger { Enabled = true };
            _input = input;
            _output = output;

            _debugger.Stopped += OnDebuggerStopped;
            _debugger.Output += OnDebuggerOutput;
        }

        /// <summary>
        /// 获取调试器实例
        /// </summary>
        public Debugger Debugger => _debugger;

        /// <summary>
        /// 等待 VS Code 完成配置（设置断点等）
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功</returns>
        public bool WaitForConfigurationDone(int timeoutMs = -1)
        {
            return _configurationDoneEvent.WaitOne(timeoutMs);
        }

        /// <summary>
        /// 等待 VS Code 发送 launch 请求
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功</returns>
        public bool WaitForLaunch(int timeoutMs = -1)
        {
            return _launchEvent.WaitOne(timeoutMs);
        }

        /// <summary>
        /// 等待断点设置完成
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功</returns>
        public bool WaitForBreakpointsSet(int timeoutMs = -1)
        {
            return _breakpointsSetEvent.WaitOne(timeoutMs);
        }

        /// <summary>
        /// 启动调试会话
        /// </summary>
        public void Run()
        {
            _running = true;
            while (_running)
            {
                var request = ReadMessage();
                if (request == null) break;
                HandleRequest(request);
            }
        }

        /// <summary>
        /// 停止调试会话
        /// </summary>
        public void Stop()
        {
            _running = false;
        }

        private Request ReadMessage()
        {
            try
            {
                // 读取 Content-Length 头
                var headerLine = ReadLine();
                if (string.IsNullOrEmpty(headerLine)) return null;

                if (!headerLine.StartsWith("Content-Length:"))
                    return null;

                var length = int.Parse(headerLine.Substring(15).Trim());

                // 跳过空行
                ReadLine();

                // 读取 JSON 内容
                var buffer = new byte[length];
                var read = 0;
                while (read < length)
                {
                    var n = _input.Read(buffer, read, length - read);
                    if (n == 0) return null;
                    read += n;
                }

                var json = Encoding.UTF8.GetString(buffer);
                return ParseRequest(json);
            }
            catch
            {
                return null;
            }
        }

        private string ReadLine()
        {
            var sb = new StringBuilder();
            int b;
            while ((b = _input.ReadByte()) != -1)
            {
                if (b == '\r')
                {
                    _input.ReadByte(); // 跳过 \n
                    break;
                }
                if (b == '\n') break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        private Request ParseRequest(string json)
        {
            // 简单 JSON 解析（实际项目应使用 JSON 库）
            var request = new Request();
            request.seq = ExtractInt(json, "seq");
            request.command = ExtractString(json, "command");
            request.arguments = ExtractArguments(json);
            return request;
        }

        private void SendMessage(ProtocolMessage message)
        {
            message.seq = _seq++;
            var json = SerializeMessage(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var header = $"Content-Length: {bytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            lock (_output)
            {
                _output.Write(headerBytes, 0, headerBytes.Length);
                _output.Write(bytes, 0, bytes.Length);
                _output.Flush();
            }
        }

        private void SendResponse(Request request, bool success, object body = null, string message = null)
        {
            var response = new Response
            {
                request_seq = request.seq,
                success = success,
                command = request.command,
                body = body,
                message = message
            };
            SendMessage(response);
        }

        private void SendEvent(string eventName, object body = null)
        {
            var evt = new Event
            {
                @event = eventName,
                body = body
            };
            SendMessage(evt);
        }

        private void HandleRequest(Request request)
        {
            switch (request.command)
            {
                case "initialize":
                    HandleInitialize(request);
                    break;
                case "launch":
                    HandleLaunch(request);
                    break;
                case "attach":
                    HandleAttach(request);
                    break;
                case "setBreakpoints":
                    HandleSetBreakpoints(request);
                    break;
                case "configurationDone":
                    HandleConfigurationDone(request);
                    break;
                case "threads":
                    HandleThreads(request);
                    break;
                case "stackTrace":
                    HandleStackTrace(request);
                    break;
                case "scopes":
                    HandleScopes(request);
                    break;
                case "variables":
                    HandleVariables(request);
                    break;
                case "evaluate":
                    HandleEvaluate(request);
                    break;
                case "continue":
                    HandleContinue(request);
                    break;
                case "next":
                    HandleNext(request);
                    break;
                case "stepIn":
                    HandleStepIn(request);
                    break;
                case "stepOut":
                    HandleStepOut(request);
                    break;
                case "pause":
                    HandlePause(request);
                    break;
                case "disconnect":
                    HandleDisconnect(request);
                    break;
                case "terminate":
                    HandleTerminate(request);
                    break;
                default:
                    SendResponse(request, false, message: $"Unknown command: {request.command}");
                    break;
            }
        }

        #region 请求处理

        private void HandleInitialize(Request request)
        {
            var capabilities = new Capabilities();
            SendResponse(request, true, capabilities);
            SendEvent("initialized");
        }

        private void HandleLaunch(Request request)
        {
            _programPath = GetArg<string>(request, "program");
            var stopOnEntry = GetArg<bool>(request, "stopOnEntry");

            _launchEvent.Set();
            SendResponse(request, true);

            if (stopOnEntry)
            {
                SendEvent("stopped", new StoppedEventBody
                {
                    reason = "entry",
                    threadId = 1
                });
            }
        }

        private void HandleAttach(Request request)
        {
            SendResponse(request, true);
        }

        private void HandleSetBreakpoints(Request request)
        {
            var source = GetArg<Dictionary<string, object>>(request, "source");
            var path = source?["path"]?.ToString() ?? "";
            var breakpointsArg = GetArg<object[]>(request, "breakpoints");

            UnityEngine.Debug.Log($"[MiniPanda DAP] SetBreakpoints for: {path}");

            _debugger.ClearBreakpoints(path);

            var breakpoints = new List<DAP.Breakpoint>();
            if (breakpointsArg != null)
            {
                foreach (var bpObj in breakpointsArg)
                {
                    if (bpObj is Dictionary<string, object> bp)
                    {
                        var line = Convert.ToInt32(bp["line"]);
                        var condition = bp.ContainsKey("condition") ? bp["condition"]?.ToString() : null;

                        var addedBp = _debugger.AddBreakpoint(path, line, condition);
                        UnityEngine.Debug.Log($"[MiniPanda DAP] Added breakpoint at line {line}");
                        breakpoints.Add(new DAP.Breakpoint
                        {
                            id = addedBp.Id,
                            verified = true,
                            line = line,
                            source = new Source { path = path }
                        });
                    }
                }
            }

            SendResponse(request, true, new SetBreakpointsResponseBody
            {
                breakpoints = breakpoints.ToArray()
            });

            // 标记断点已设置
            _breakpointsSetEvent.Set();
        }

        private void HandleConfigurationDone(Request request)
        {
            _configurationDoneEvent.Set();
            SendResponse(request, true);
        }

        private void HandleThreads(Request request)
        {
            SendResponse(request, true, new ThreadsResponseBody
            {
                threads = new[] { new Thread { id = 1, name = "Main Thread" } }
            });
        }

        private void HandleStackTrace(Request request)
        {
            var frames = GetStackFrames();
            SendResponse(request, true, new StackTraceResponseBody
            {
                stackFrames = frames,
                totalFrames = frames.Length
            });
        }

        private void HandleScopes(Request request)
        {
            var frameId = GetArg<int>(request, "frameId");

            _varRefs.Clear();
            _nextVarRef = 1;

            var localRef = _nextVarRef++;
            var globalRef = _nextVarRef++;

            _varRefs[localRef] = ("local", frameId);
            _varRefs[globalRef] = ("global", frameId);

            SendResponse(request, true, new ScopesResponseBody
            {
                scopes = new[]
                {
                    new Scope { name = "Local", variablesReference = localRef, expensive = false },
                    new Scope { name = "Global", variablesReference = globalRef, expensive = true }
                }
            });
        }

        private void HandleVariables(Request request)
        {
            var varRef = GetArg<int>(request, "variablesReference");
            var variables = new List<Variable>();

            if (_varRefs.TryGetValue(varRef, out var refData))
            {
                if (refData is ValueTuple<string, int> scopeRef)
                {
                    var (scopeType, frameId) = scopeRef;
                    variables = GetScopeVariables(scopeType, frameId);
                }
                else if (refData is Value value)
                {
                    variables = GetValueChildren(value);
                }
            }

            SendResponse(request, true, new VariablesResponseBody
            {
                variables = variables.ToArray()
            });
        }

        private void HandleEvaluate(Request request)
        {
            var expression = GetArg<string>(request, "expression");

            try
            {
                var result = _vm.Eval(expression);
                var resultStr = result.AsString();
                var typeStr = GetValueTypeName(result);

                var varRef = 0;
                if (result.IsArray || result.IsDict || result.IsInstance)
                {
                    varRef = _nextVarRef++;
                    _varRefs[varRef] = result;
                }

                SendResponse(request, true, new EvaluateResponseBody
                {
                    result = resultStr,
                    type = typeStr,
                    variablesReference = varRef
                });
            }
            catch (Exception ex)
            {
                SendResponse(request, false, message: ex.Message);
            }
        }

        private void HandleContinue(Request request)
        {
            _debugger.Continue();
            SendResponse(request, true, new ContinueResponseBody());
        }

        private void HandleNext(Request request)
        {
            _debugger.StepOver(GetCurrentFrameDepth());
            SendResponse(request, true);
        }

        private void HandleStepIn(Request request)
        {
            _debugger.StepIn();
            SendResponse(request, true);
        }

        private void HandleStepOut(Request request)
        {
            _debugger.StepOut(GetCurrentFrameDepth());
            SendResponse(request, true);
        }

        private void HandlePause(Request request)
        {
            _debugger.Pause();
            SendResponse(request, true);
        }

        private void HandleDisconnect(Request request)
        {
            SendResponse(request, true);
            Stop();
        }

        private void HandleTerminate(Request request)
        {
            SendResponse(request, true);
            SendEvent("terminated", new TerminatedEventBody());
            Stop();
        }

        #endregion

        #region 事件处理

        private void OnDebuggerStopped(object sender, DebugEventArgs e)
        {
            var reason = e.Reason switch
            {
                StopReason.Breakpoint => "breakpoint",
                StopReason.Step => "step",
                StopReason.StepIn => "step",
                StopReason.StepOut => "step",
                StopReason.Pause => "pause",
                StopReason.Exception => "exception",
                StopReason.Entry => "entry",
                _ => "step"
            };

            SendEvent("stopped", new StoppedEventBody
            {
                reason = reason,
                threadId = 1,
                description = e.Message
            });
        }

        private void OnDebuggerOutput(object sender, string message)
        {
            SendEvent("output", new OutputEventBody
            {
                category = "stdout",
                output = message + "\n"
            });
        }

        #endregion

        #region 辅助方法

        private T GetArg<T>(Request request, string name)
        {
            if (request.arguments != null && request.arguments.TryGetValue(name, out var value))
            {
                if (value is T t) return t;
                try { return (T)Convert.ChangeType(value, typeof(T)); }
                catch { return default; }
            }
            return default;
        }

        private StackFrame[] GetStackFrames()
        {
            var vmFrames = _vm.GetStackTrace();
            if (vmFrames == null || vmFrames.Length == 0)
            {
                return new[] { new StackFrame { id = 0, name = "<main>", line = 1, column = 1 } };
            }

            var frames = new StackFrame[vmFrames.Length];
            for (int i = 0; i < vmFrames.Length; i++)
            {
                var f = vmFrames[i];
                var isValidPath = f.File.IndexOfAny(System.IO.Path.GetInvalidPathChars()) < 0;
                frames[i] = new StackFrame
                {
                    id = f.Id,
                    name = f.Name,
                    source = isValidPath ? new Source { path = f.File, name = System.IO.Path.GetFileName(f.File) } : null,
                    line = f.Line,
                    column = f.Column
                };
            }
            return frames;
        }

        private List<Variable> GetScopeVariables(string scopeType, int frameId)
        {
            var variables = new List<Variable>();

            if (scopeType == "global")
            {
                foreach (var kv in _vm.GlobalScope.GetAll())
                {
                    var varRef = 0;
                    if (kv.Value.IsArray || kv.Value.IsDict || kv.Value.IsInstance)
                    {
                        varRef = _nextVarRef++;
                        _varRefs[varRef] = kv.Value;
                    }

                    variables.Add(new Variable
                    {
                        name = kv.Key,
                        value = kv.Value.AsString(),
                        type = GetValueTypeName(kv.Value),
                        variablesReference = varRef
                    });
                }
            }

            return variables;
        }

        private List<Variable> GetValueChildren(Value value)
        {
            var variables = new List<Variable>();

            if (value.As<MiniPandaArray>() is { } arr)
            {
                for (int i = 0; i < arr.Elements.Count; i++)
                {
                    var elem = arr.Elements[i];
                    var varRef = 0;
                    if (elem.IsArray || elem.IsDict || elem.IsInstance)
                    {
                        varRef = _nextVarRef++;
                        _varRefs[varRef] = elem;
                    }

                    variables.Add(new Variable
                    {
                        name = $"[{i}]",
                        value = elem.AsString(),
                        type = GetValueTypeName(elem),
                        variablesReference = varRef
                    });
                }
            }
            else if (value.As<MiniPandaObject>() is { } obj)
            {
                foreach (var kv in obj.Fields)
                {
                    var varRef = 0;
                    if (kv.Value.IsArray || kv.Value.IsDict || kv.Value.IsInstance)
                    {
                        varRef = _nextVarRef++;
                        _varRefs[varRef] = kv.Value;
                    }

                    variables.Add(new Variable
                    {
                        name = kv.Key,
                        value = kv.Value.AsString(),
                        type = GetValueTypeName(kv.Value),
                        variablesReference = varRef
                    });
                }
            }
            else if (value.As<MiniPandaInstance>() is { } inst)
            {
                foreach (var kv in inst.Fields)
                {
                    var varRef = 0;
                    if (kv.Value.IsArray || kv.Value.IsDict || kv.Value.IsInstance)
                    {
                        varRef = _nextVarRef++;
                        _varRefs[varRef] = kv.Value;
                    }

                    variables.Add(new Variable
                    {
                        name = kv.Key,
                        value = kv.Value.AsString(),
                        type = GetValueTypeName(kv.Value),
                        variablesReference = varRef
                    });
                }
            }

            return variables;
        }

        private string GetValueTypeName(Value value)
        {
            return value.Type switch
            {
                Core.ValueType.Null => "null",
                Core.ValueType.Bool => "bool",
                Core.ValueType.Number => "number",
                Core.ValueType.Object when value.IsString => "string",
                Core.ValueType.Object when value.IsArray => "array",
                Core.ValueType.Object when value.IsDict => "object",
                Core.ValueType.Object when value.IsFunction => "function",
                Core.ValueType.Object when value.IsClass => "class",
                Core.ValueType.Object when value.IsInstance => "instance",
                _ => "unknown"
            };
        }

        private int GetCurrentFrameDepth()
        {
            return _vm.FrameDepth;
        }

        // 简单 JSON 解析辅助方法
        private static int ExtractInt(string json, string key)
        {
            var pattern = $"\"{key}\":";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return 0;
            idx += pattern.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return int.TryParse(json.Substring(idx, end - idx), out var result) ? result : 0;
        }

        private static string ExtractString(string json, string key)
        {
            var pattern = $"\"{key}\":\"";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return null;
            idx += pattern.Length;
            var end = json.IndexOf('"', idx);
            return end > idx ? json.Substring(idx, end - idx) : null;
        }

        private static Dictionary<string, object> ExtractArguments(string json)
        {
            // 查找 "arguments" 字段
            var pattern = "\"arguments\":";
            var idx = json.IndexOf(pattern);
            if (idx < 0) return new Dictionary<string, object>();
            idx += pattern.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            if (idx >= json.Length || json[idx] != '{') return new Dictionary<string, object>();
            return ParseJsonObject(json, ref idx);
        }

        private static Dictionary<string, object> ParseJsonObject(string json, ref int idx)
        {
            var result = new Dictionary<string, object>();
            idx++; // skip '{'
            SkipWhitespace(json, ref idx);

            while (idx < json.Length && json[idx] != '}')
            {
                SkipWhitespace(json, ref idx);
                if (json[idx] == '}') break;

                // 解析 key
                var key = ParseJsonString(json, ref idx);
                SkipWhitespace(json, ref idx);
                idx++; // skip ':'
                SkipWhitespace(json, ref idx);

                // 解析 value
                var value = ParseJsonValue(json, ref idx);
                result[key] = value;

                SkipWhitespace(json, ref idx);
                if (json[idx] == ',') idx++;
            }

            if (idx < json.Length) idx++; // skip '}'
            return result;
        }

        private static object ParseJsonValue(string json, ref int idx)
        {
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length) return null;

            var c = json[idx];
            if (c == '"') return ParseJsonString(json, ref idx);
            if (c == '{') return ParseJsonObject(json, ref idx);
            if (c == '[') return ParseJsonArray(json, ref idx);
            if (c == 't' || c == 'f') return ParseJsonBool(json, ref idx);
            if (c == 'n') { idx += 4; return null; }
            if (char.IsDigit(c) || c == '-') return ParseJsonNumber(json, ref idx);
            return null;
        }

        private static string ParseJsonString(string json, ref int idx)
        {
            idx++; // skip opening quote
            var sb = new StringBuilder();
            while (idx < json.Length && json[idx] != '"')
            {
                if (json[idx] == '\\' && idx + 1 < json.Length)
                {
                    idx++;
                    switch (json[idx])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        default: sb.Append(json[idx]); break;
                    }
                }
                else
                {
                    sb.Append(json[idx]);
                }
                idx++;
            }
            if (idx < json.Length) idx++; // skip closing quote
            return sb.ToString();
        }

        private static object[] ParseJsonArray(string json, ref int idx)
        {
            var list = new List<object>();
            idx++; // skip '['
            SkipWhitespace(json, ref idx);

            while (idx < json.Length && json[idx] != ']')
            {
                list.Add(ParseJsonValue(json, ref idx));
                SkipWhitespace(json, ref idx);
                if (json[idx] == ',') idx++;
                SkipWhitespace(json, ref idx);
            }

            if (idx < json.Length) idx++; // skip ']'
            return list.ToArray();
        }

        private static bool ParseJsonBool(string json, ref int idx)
        {
            if (json.Substring(idx, 4) == "true") { idx += 4; return true; }
            idx += 5; return false;
        }

        private static double ParseJsonNumber(string json, ref int idx)
        {
            var start = idx;
            if (json[idx] == '-') idx++;
            while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == 'e' || json[idx] == 'E' || json[idx] == '+' || json[idx] == '-'))
                idx++;
            double.TryParse(json.Substring(start, idx - start), out var result);
            return result;
        }

        private static void SkipWhitespace(string json, ref int idx)
        {
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
        }

        private static string SerializeMessage(ProtocolMessage message)
        {
            // 简化实现，实际应使用 JSON 库
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"seq\":{message.seq},");
            sb.Append($"\"type\":\"{message.type}\"");

            if (message is Response response)
            {
                sb.Append($",\"request_seq\":{response.request_seq}");
                sb.Append($",\"success\":{response.success.ToString().ToLower()}");
                sb.Append($",\"command\":\"{response.command}\"");
                if (response.message != null)
                    sb.Append($",\"message\":\"{EscapeString(response.message)}\"");
                if (response.body != null)
                    sb.Append($",\"body\":{SerializeObject(response.body)}");
            }
            else if (message is Event evt)
            {
                sb.Append($",\"event\":\"{evt.@event}\"");
                if (evt.body != null)
                    sb.Append($",\"body\":{SerializeObject(evt.body)}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static string SerializeObject(object obj)
        {
            if (obj == null) return "null";
            if (obj is bool b) return b.ToString().ToLower();
            if (obj is int i) return i.ToString();
            if (obj is string s) return $"\"{EscapeString(s)}\"";

            // 使用反射序列化对象
            var sb = new StringBuilder();
            sb.Append("{");
            var first = true;
            foreach (var prop in obj.GetType().GetProperties())
            {
                var value = prop.GetValue(obj);
                if (value == null) continue;

                if (!first) sb.Append(",");
                first = false;

                var name = prop.Name;
                if (name == "event") name = "@event"; // 特殊处理

                sb.Append($"\"{name}\":");

                if (value is bool bv)
                    sb.Append(bv.ToString().ToLower());
                else if (value is int iv)
                    sb.Append(iv);
                else if (value is string sv)
                    sb.Append($"\"{EscapeString(sv)}\"");
                else if (value is Array arr)
                {
                    sb.Append("[");
                    for (int j = 0; j < arr.Length; j++)
                    {
                        if (j > 0) sb.Append(",");
                        sb.Append(SerializeObject(arr.GetValue(j)));
                    }
                    sb.Append("]");
                }
                else
                    sb.Append(SerializeObject(value));
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        #endregion
    }
}
