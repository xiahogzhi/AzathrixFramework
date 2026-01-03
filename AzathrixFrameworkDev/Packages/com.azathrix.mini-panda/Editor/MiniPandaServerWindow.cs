using UnityEditor;
using Azathrix.MiniPanda.LSP;

namespace Azathrix.MiniPanda.Editor
{
    /// <summary>
    /// MiniPanda LSP 服务器自动启动
    /// </summary>
    [InitializeOnLoad]
    public static class MiniPandaLSPServer
    {
        private const string LSPPortKey = "MiniPanda_LSPServer_Port";
        private static LanguageServer _lspServer;

        static MiniPandaLSPServer()
        {
            EditorApplication.delayCall += StartLSPServer;
            AssemblyReloadEvents.beforeAssemblyReload += StopLSPServer;
            EditorApplication.quitting += StopLSPServer;
        }

        private static int LSPPort
        {
            get => EditorPrefs.GetInt(LSPPortKey, 4712);
            set => EditorPrefs.SetInt(LSPPortKey, value);
        }

        private static void StartLSPServer()
        {
            if (_lspServer?.IsRunning == true) return;

            try
            {
                _lspServer = new LanguageServer();
                _lspServer.Start(LSPPort);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[MiniPanda] Failed to start LSP server: {ex.Message}");
            }
        }

        private static void StopLSPServer()
        {
            try
            {
                _lspServer?.Stop();
            }
            catch { }
            _lspServer = null;
        }
    }
}
