using System;
using System.Net;
using System.Net.Sockets;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Debug.DAP
{
    /// <summary>
    /// DAP 调试服务器 - 在 Unity 中运行，接收 VS Code 的调试请求
    /// </summary>
    public class DebugServer : IDisposable
    {
        private readonly VirtualMachine _vm;
        private TcpListener _listener;
        private TcpClient _client;
        private System.Threading.Thread _listenThread;
        private DebugAdapter _adapter;
        private bool _running;
        private int _port;
        private System.Threading.ManualResetEvent _clientConnectedEvent = new System.Threading.ManualResetEvent(false);

        /// <summary>服务器是否正在运行</summary>
        public bool IsRunning => _running;

        /// <summary>监听端口</summary>
        public int Port => _port;

        /// <summary>是否有客户端已连接</summary>
        public bool IsClientConnected => _adapter != null;

        /// <summary>获取当前调试适配器</summary>
        public DebugAdapter Adapter => _adapter;

        /// <summary>客户端连接事件</summary>
        public event EventHandler<EventArgs> ClientConnected;

        /// <summary>客户端断开事件</summary>
        public event EventHandler<EventArgs> ClientDisconnected;

        public DebugServer(VirtualMachine vm)
        {
            _vm = vm;
        }

        /// <summary>
        /// 启动调试服务器
        /// </summary>
        public void Start(int port = 4711)
        {
            if (_running) return;

            _port = port;
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _running = true;

            _listenThread = new System.Threading.Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "MiniPanda Debug Server"
            };
            _listenThread.Start();

            UnityEngine.Debug.Log($"[MiniPanda] Debug server started on port {port}");
        }

        /// <summary>
        /// 等待调试客户端连接
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功连接</returns>
        public bool WaitForConnection(int timeoutMs = -1)
        {
            if (!_running) return false;
            UnityEngine.Debug.Log("[MiniPanda] Waiting for debugger to connect...");
            return _clientConnectedEvent.WaitOne(timeoutMs);
        }

        /// <summary>
        /// 等待调试器完成配置（设置断点等）
        /// 必须在 WaitForConnection 之后调用
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功</returns>
        public bool WaitForConfigurationDone(int timeoutMs = -1)
        {
            if (_adapter == null) return false;
            UnityEngine.Debug.Log("[MiniPanda] Waiting for debugger configuration...");
            return _adapter.WaitForConfigurationDone(timeoutMs);
        }

        /// <summary>
        /// 等待调试器发送 launch 请求
        /// 必须在 WaitForConnection 之后调用
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功</returns>
        public bool WaitForLaunch(int timeoutMs = -1)
        {
            if (_adapter == null) return false;
            UnityEngine.Debug.Log("[MiniPanda] Waiting for launch request...");
            return _adapter.WaitForLaunch(timeoutMs);
        }

        /// <summary>
        /// 等待调试器完全准备好（连接 + 配置 + launch）
        /// 这是推荐的等待方法，确保断点已设置完成
        /// </summary>
        /// <param name="timeoutMs">超时时间（毫秒），-1 表示无限等待</param>
        /// <returns>是否成功</returns>
        public bool WaitForReady(int timeoutMs = -1)
        {
            if (!_running) return false;

            UnityEngine.Debug.Log("[MiniPanda] Waiting for debugger to connect...");
            if (!_clientConnectedEvent.WaitOne(timeoutMs)) return false;

            UnityEngine.Debug.Log("[MiniPanda] Waiting for debugger to be ready...");
            if (_adapter == null) return false;

            // 等待 launch 请求
            if (!_adapter.WaitForLaunch(timeoutMs)) return false;

            // 等待断点设置完成（最多等待 3 秒）
            UnityEngine.Debug.Log("[MiniPanda] Waiting for breakpoints...");
            _adapter.WaitForBreakpointsSet(3000);

            UnityEngine.Debug.Log("[MiniPanda] Debugger ready, breakpoints set");
            return true;
        }

        /// <summary>
        /// 停止调试服务器
        /// </summary>
        public void Stop()
        {
            if (!_running) return;

            _running = false;
            _adapter?.Stop();
            _client?.Close();
            _client = null;
            _listener?.Stop();
            _listenThread?.Join(1000);

            UnityEngine.Debug.Log("[MiniPanda] Debug server stopped");
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
                        System.Threading.Thread.Sleep(100);
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[MiniPanda] Debug server error: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            UnityEngine.Debug.Log("[MiniPanda] Debug client connected");

            _client = client;

            try
            {
                var stream = client.GetStream();
                _adapter = new DebugAdapter(_vm, stream, stream);
                _vm.Debugger = _adapter.Debugger;

                // 在 Debugger 设置完成后才发送连接信号
                _clientConnectedEvent.Set();
                ClientConnected?.Invoke(this, EventArgs.Empty);

                var adapterThread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        _adapter.Run();
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[MiniPanda] Debug adapter error: {ex.Message}");
                    }
                    finally
                    {
                        client.Close();
                        _client = null;
                        _vm.Debugger = null;
                        _clientConnectedEvent.Reset();
                        ClientDisconnected?.Invoke(this, EventArgs.Empty);
                        UnityEngine.Debug.Log("[MiniPanda] Debug client disconnected");
                    }
                })
                {
                    IsBackground = true,
                    Name = "MiniPanda Debug Adapter"
                };
                adapterThread.Start();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[MiniPanda] Failed to handle debug client: {ex.Message}");
                client.Close();
            }
        }

        public void Dispose()
        {
            Stop();
            _clientConnectedEvent?.Dispose();
            _adapter?.Dispose();
        }
    }
}





