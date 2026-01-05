using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Debug.DAP
{
    /// <summary>
    /// MiniPanda DAP 调试适配器
    /// </summary>
    public class DebugAdapter : IDisposable
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

        public void Dispose()
        {
            Stop();
            _configurationDoneEvent?.Dispose();
            _launchEvent?.Dispose();
            _breakpointsSetEvent?.Dispose();
        }

        private Request ReadMessage()
        {
            try
            {
                int length = -1;

                // 读取所有头部，直到空行
                while (true)
                {
                    var line = ReadLine();
                    if (line == null) return null;
                    if (line.Length == 0) break;

                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        length = int.Parse(line.Substring(15).Trim());
                        // 防止恶意客户端发送超大值导致内存耗尽
                        if (length > 10 * 1024 * 1024) // 10MB 上限
                            return null;
                    }
                }

                if (length < 0) return null;

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
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[MiniPanda DAP] ReadMessage error: {ex.Message}");
                return null;
            }
        }

        private string ReadLine()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var b = _input.ReadByte();
                if (b == -1)
                {
                    return sb.Length == 0 ? null : sb.ToString();
                }
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
            if (string.IsNullOrEmpty(json)) return null;

            var obj = JObject.Parse(json);
            var request = new Request
            {
                seq = obj.Value<int?>("seq") ?? 0,
                command = obj.Value<string>("command"),
                arguments = ToDictionary(obj["arguments"])
            };
            return request;
        }

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private void SendMessage(ProtocolMessage message)
        {
            message.seq = _seq++;
            var json = JsonConvert.SerializeObject(message, JsonSettings);
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
            var context = GetArg<string>(request, "context"); // "hover", "watch", "repl"

            try
            {
                Value result;

                // hover 时优先在当前作用域查找
                if (context == "hover" && !expression.Contains("("))
                {
                    result = EvaluateInCurrentScope(expression);
                    if (!result.IsNull)
                        goto sendResult;
                }

                // 回退到 Eval
                result = _vm.Eval(expression);

                sendResult:
                var resultStr = FormatValue(result);
                var typeStr = GetValueTypeName(result);

                var varRef = 0;
                if (result.IsArray || result.IsDict || result.IsInstance ||
                    result.As<MiniPandaModule>() != null ||
                    result.As<MiniPandaGlobalTable>() != null)
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

            if (scopeType == "local")
            {
                // 1. 栈上的局部变量
                var locals = _vm.GetLocalVariables(frameId);
                foreach (var kv in locals)
                {
                    variables.Add(CreateVariable(kv.Key, kv.Value));
                }

                // 2. 当前帧的闭包环境变量（脚本级变量）
                var env = _vm.GetFrameEnvironment(frameId);
                if (env != null)
                {
                    foreach (var kv in env.GetAll())
                    {
                        // 跳过已经在局部变量中的
                        if (locals.ContainsKey(kv.Key)) continue;
                        // 跳过内置函数
                        if (kv.Value.As<NativeFunction>() != null) continue;
                        variables.Add(CreateVariable(kv.Key, kv.Value));
                    }
                }
            }
            else if (scopeType == "global")
            {
                // 只显示真正的全局变量（不包括内置函数）
                foreach (var kv in _vm.GlobalScope.GetAll())
                {
                    // 跳过内置函数，它们太多了
                    if (kv.Value.As<NativeFunction>() != null) continue;
                    variables.Add(CreateVariable(kv.Key, kv.Value));
                }
            }

            return variables;
        }

        private Variable CreateVariable(string name, Value value)
        {
            var varRef = 0;
            // 可展开的类型
            if (value.IsArray || value.IsDict || value.IsInstance ||
                value.As<MiniPandaGlobalTable>() != null ||
                value.As<MiniPandaModule>() != null)
            {
                varRef = _nextVarRef++;
                _varRefs[varRef] = value;
            }

            return new Variable
            {
                name = name,
                value = FormatValue(value),
                type = GetValueTypeName(value),
                variablesReference = varRef
            };
        }

        private string FormatValue(Value value)
        {
            // 对函数类型显示更友好的格式
            if (value.As<MiniPandaFunction>() is { } func)
                return $"function {func.Prototype?.Name ?? "<anonymous>"}()";
            if (value.As<MiniPandaBoundMethod>() is { } method)
                return $"method {method.Method?.Prototype?.Name ?? "<bound>"}()";
            if (value.As<NativeFunction>() != null)
                return "<native function>";
            if (value.As<MiniPandaClass>() is { } cls)
                return $"class {cls.Name}";
            if (value.As<MiniPandaGlobalTable>() != null)
                return "<global table>";
            if (value.As<MiniPandaModule>() is { } module)
                return $"module {System.IO.Path.GetFileNameWithoutExtension(module.Path)}";
            return value.AsString();
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
                    variables.Add(CreateVariable(kv.Key, kv.Value));
                }
            }
            else if (value.As<MiniPandaGlobalTable>() is { } globalTable)
            {
                // 显示 _G 的内容（全局变量）
                foreach (var kv in _vm.GlobalScope.GetAll())
                {
                    // 跳过内置函数
                    if (kv.Value.As<NativeFunction>() != null) continue;
                    variables.Add(CreateVariable(kv.Key, kv.Value));
                }
            }
            else if (value.As<MiniPandaModule>() is { } module)
            {
                // 显示模块导出的成员
                foreach (var kv in module.Env.GetAll())
                {
                    // 如果有导出列表，只显示导出的
                    if (module.Exports != null && module.Exports.Count > 0 && !module.Exports.Contains(kv.Key))
                        continue;
                    variables.Add(CreateVariable(kv.Key, kv.Value));
                }
            }

            return variables;
        }

        private string GetValueTypeName(Value value)
        {
            if (value.As<MiniPandaGlobalTable>() != null) return "global";
            if (value.As<MiniPandaModule>() != null) return "module";

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

        /// <summary>
        /// 在当前作用域中求值表达式（支持成员访问如 config.debug）
        /// </summary>
        private Value EvaluateInCurrentScope(string expression)
        {
            var parts = expression.Split('.');
            var rootName = parts[0];

            // 查找根变量
            Value value = Value.Null;
            var frames = _vm.GetStackTrace();

            if (frames.Length > 0)
            {
                // 1. 局部变量
                var locals = _vm.GetLocalVariables(frames[0].Id);
                if (locals.TryGetValue(rootName, out var localValue))
                {
                    value = localValue;
                }
                else
                {
                    // 2. 闭包环境
                    var env = _vm.GetFrameEnvironment(frames[0].Id);
                    if (env != null)
                    {
                        value = env.Get(rootName);
                    }
                }
            }

            // 3. 全局变量
            if (value.IsNull)
            {
                value = _vm.GlobalScope.Get(rootName);
            }

            if (value.IsNull) return Value.Null;

            // 处理成员访问链 (config.debug.xxx 或 module.func)
            for (int i = 1; i < parts.Length && !value.IsNull; i++)
            {
                var member = parts[i];
                if (value.As<MiniPandaObject>() is { } obj)
                {
                    value = obj.Fields.TryGetValue(member, out var v) ? v : Value.Null;
                }
                else if (value.As<MiniPandaInstance>() is { } inst)
                {
                    value = inst.Fields.TryGetValue(member, out var v) ? v : Value.Null;
                }
                else if (value.As<MiniPandaModule>() is { } module)
                {
                    value = module.GetMember(member);
                }
                else
                {
                    return Value.Null;
                }
            }

            return value;
        }

        private int GetCurrentFrameDepth()
        {
            return _vm.FrameDepth;
        }

        // JSON 解析辅助方法（基于 Newtonsoft）
        private static Dictionary<string, object> ToDictionary(JToken token)
        {
            return ToPlainObject(token) as Dictionary<string, object> ?? new Dictionary<string, object>();
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
    }
}












