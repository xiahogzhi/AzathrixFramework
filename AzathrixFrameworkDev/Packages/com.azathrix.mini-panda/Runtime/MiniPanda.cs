using System.Collections.Generic;
using Azathrix.MiniPanda.Compiler;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;
using Environment = Azathrix.MiniPanda.Core.Environment;

namespace Azathrix.MiniPanda
{
    public class MiniPanda
    {
        private readonly VirtualMachine _vm;
        private bool _started;

        public bool IsStarted => _started;
        public bool CacheEnabled { get => _vm.CacheEnabled; set => _vm.CacheEnabled = value; }
        public FileLoader CustomLoader { get => _vm.CustomLoader; set => _vm.CustomLoader = value; }

        public MiniPanda()
        {
            _vm = new VirtualMachine();
        }

        public void Start()
        {
            if (_started) return;
            _vm.RegisterBuiltins();
            _started = true;
        }

        public void Shutdown()
        {
            _vm.Reset();
            _started = false;
        }

        // Run API
        public Value Run(string code, string scopeName = "main", bool clearScope = true)
            => _vm.Run(code, scopeName, clearScope);

        public Value Run(byte[] data, string scopeName = "main", bool clearScope = true)
            => _vm.Run(data, scopeName, clearScope);

        public T Run<T>(string code, string scopeName = "main", bool clearScope = true)
            => _vm.Run<T>(code, scopeName, clearScope);

        // Eval API
        public Value Eval(string expression, object env = null, string scopeName = "main", bool clearScope = true)
            => _vm.Eval(expression, env, scopeName, clearScope);

        public T Eval<T>(string expression, object env = null, string scopeName = "main", bool clearScope = true)
            => _vm.Eval<T>(expression, env, scopeName, clearScope);

        // Compilation
        public CompiledScript Compile(string code) => _vm.Compile(code);

        // File operations
        public Value RunFile(string path) => _vm.RunFile(path);
        public (byte[] data, string fullPath) LoadFile(string path) => _vm.LoadFile(path);
        public void LoadModule(byte[] data, string moduleName, string sourcePath = null)
            => _vm.LoadModule(data, moduleName, sourcePath);

        // Global variables
        public void SetGlobal(string name, Value value) => _vm.SetGlobal(name, value);
        public void SetGlobal(string name, double value) => _vm.SetGlobal(name, value);
        public void SetGlobal(string name, bool value) => _vm.SetGlobal(name, value);
        public void SetGlobal(string name, string value) => _vm.SetGlobal(name, value);
        public void SetGlobal(string name, NativeFunction func) => _vm.SetGlobal(name, func);
        public Value GetGlobal(string name) => _vm.GetGlobal(name);

        // Function calls
        public Value Call(string funcName, params object[] args) => _vm.Call(funcName, args);

        // Scope management
        public Environment GetScope(string name) => _vm.GetScope(name);
        public void ClearScope(string name) => _vm.ClearScope(name);

        // Cache management
        public void ClearCache() => _vm.ClearCache();

        // Utilities
        public static bool IsBytecode(byte[] data) => VirtualMachine.IsBytecode(data);
        public static string ConvertPath(string path) => VirtualMachine.ConvertPath(path);
    }
}
