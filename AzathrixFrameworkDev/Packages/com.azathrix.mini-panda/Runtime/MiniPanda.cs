using System.Collections.Generic;
using Azathrix.MiniPanda.Compiler;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;
using Environment = Azathrix.MiniPanda.Core.Environment;

namespace Azathrix.MiniPanda
{
    /// <summary>
    /// MiniPanda 脚本引擎主入口
    /// </summary>
    /// <remarks>
    /// 提供简洁的 API 封装，包括：
    /// - 脚本执行：Run, Eval
    /// - 编译：Compile
    /// - 文件操作：RunFile, LoadFile, LoadModule
    /// - 全局变量：SetGlobal, GetGlobal
    /// - 函数调用：Call
    /// - 作用域管理：GetScope, ClearScope
    /// </remarks>
    public class MiniPanda
    {
        private readonly VirtualMachine _vm;  // 虚拟机实例
        private bool _started;                // 是否已启动

        /// <summary>是否已启动</summary>
        public bool IsStarted => _started;
        /// <summary>内部虚拟机实例（用于调试器）</summary>
        public VirtualMachine VM => _vm;
        /// <summary>是否启用字节码缓存</summary>
        public bool CacheEnabled { get => _vm.CacheEnabled; set => _vm.CacheEnabled = value; }
        /// <summary>自定义文件加载器</summary>
        public FileLoader CustomLoader { get => _vm.CustomLoader; set => _vm.CustomLoader = value; }

        /// <summary>创建 MiniPanda 实例</summary>
        public MiniPanda()
        {
            _vm = new VirtualMachine();
        }

        /// <summary>启动引擎（注册内置函数）</summary>
        public void Start()
        {
            if (_started) return;
            _vm.RegisterBuiltins();
            _started = true;
        }

        /// <summary>重置虚拟机状态（清除缓存和对象池）</summary>
        public void Reset()
        {
            _vm.Reset();
        }

        /// <summary>关闭引擎（重置状态）</summary>
        public void Shutdown()
        {
            _vm.Reset();
            _started = false;
        }

        #region 执行 API

        /// <summary>执行脚本代码</summary>
        /// <param name="code">脚本源代码</param>
        /// <param name="scopeName">作用域名称</param>
        /// <param name="clearScope">是否清除作用域</param>
        public Value Run(string code, string scopeName = "main", bool clearScope = true)
            => _vm.Run(code, scopeName, clearScope);

        /// <summary>执行字节码</summary>
        public Value Run(byte[] data, string scopeName = "main", bool clearScope = true)
            => _vm.Run(data, scopeName, clearScope);

        /// <summary>执行脚本并转换返回值类型</summary>
        public T Run<T>(string code, string scopeName = "main", bool clearScope = true)
            => _vm.Run<T>(code, scopeName, clearScope);

        /// <summary>求值表达式</summary>
        public Value Eval(string expression, string scopeName = "main", bool clearScope = true)
            => _vm.Eval(expression, scopeName, clearScope);

        /// <summary>求值表达式（带 Environment 环境变量）</summary>
        public Value Eval(string expression, Environment env, string scopeName = "main", bool clearScope = true)
            => _vm.Eval(expression, env, scopeName, clearScope);

        /// <summary>求值表达式（带 Dictionary 环境变量）</summary>
        public Value Eval(string expression, Dictionary<string, object> env, string scopeName = "main", bool clearScope = true)
            => _vm.Eval(expression, env, scopeName, clearScope);

        /// <summary>求值表达式（带自定义环境提供者）</summary>
        public Value Eval(string expression, IEnvironmentProvider env, string scopeName = "main", bool clearScope = true)
            => _vm.Eval(expression, env, scopeName, clearScope);

        /// <summary>求值表达式并转换返回值类型</summary>
        public T Eval<T>(string expression, string scopeName = "main", bool clearScope = true)
            => _vm.Eval<T>(expression, scopeName, clearScope);

        /// <summary>求值表达式并转换返回值类型（带 Environment 环境变量）</summary>
        public T Eval<T>(string expression, Environment env, string scopeName = "main", bool clearScope = true)
            => _vm.Eval<T>(expression, env, scopeName, clearScope);

        /// <summary>求值表达式并转换返回值类型（带 Dictionary 环境变量）</summary>
        public T Eval<T>(string expression, Dictionary<string, object> env, string scopeName = "main", bool clearScope = true)
            => _vm.Eval<T>(expression, env, scopeName, clearScope);

        /// <summary>求值表达式并转换返回值类型（带自定义环境提供者）</summary>
        public T Eval<T>(string expression, IEnvironmentProvider env, string scopeName = "main", bool clearScope = true)
            => _vm.Eval<T>(expression, env, scopeName, clearScope);

        #endregion

        #region 编译 API

        /// <summary>编译脚本为字节码</summary>
        public CompiledScript Compile(string code) => _vm.Compile(code);

        #endregion

        #region 文件操作

        /// <summary>执行脚本文件</summary>
        public Value RunFile(string path) => _vm.RunFile(path);

        /// <summary>加载文件内容</summary>
        public (byte[] data, string fullPath) LoadFile(string path) => _vm.LoadFile(path);

        /// <summary>加载模块</summary>
        public void LoadModule(byte[] data, string moduleName, string sourcePath = null)
            => _vm.LoadModule(data, moduleName, sourcePath);

        #endregion

        #region 全局变量

        /// <summary>设置全局变量</summary>
        public void SetGlobal(string name, Value value) => _vm.SetGlobal(name, value);
        /// <summary>设置全局变量（数字）</summary>
        public void SetGlobal(string name, double value) => _vm.SetGlobal(name, value);
        /// <summary>设置全局变量（布尔）</summary>
        public void SetGlobal(string name, bool value) => _vm.SetGlobal(name, value);
        /// <summary>设置全局变量（字符串）</summary>
        public void SetGlobal(string name, string value) => _vm.SetGlobal(name, value);
        /// <summary>设置全局变量（原生函数）</summary>
        public void SetGlobal(string name, NativeFunction func) => _vm.SetGlobal(name, func);
        /// <summary>获取全局变量</summary>
        public Value GetGlobal(string name) => _vm.GetGlobal(name);

        #endregion

        #region 函数调用

        /// <summary>调用全局函数</summary>
        public Value Call(string funcName, params object[] args) => _vm.Call(funcName, args);
        /// <summary>调用指定 Environment 作用域的函数</summary>
        public Value Call(Environment scope, string funcName, params object[] args) => _vm.Call(scope, funcName, args);
        /// <summary>调用指定 Dictionary 作用域的函数</summary>
        public Value Call(Dictionary<string, object> scope, string funcName, params object[] args) => _vm.Call(scope, funcName, args);

        #endregion

        #region 作用域管理

        /// <summary>获取指定作用域</summary>
        public Environment GetScope(string name) => _vm.GetScope(name);
        /// <summary>清除指定作用域</summary>
        public void ClearScope(string name) => _vm.ClearScope(name);

        #endregion

        #region 缓存管理

        /// <summary>清除字节码缓存</summary>
        public void ClearCache() => _vm.ClearCache();

        #endregion

        #region 工具方法

        /// <summary>检查数据是否为字节码格式</summary>
        public static bool IsBytecode(byte[] data) => VirtualMachine.IsBytecode(data);
        /// <summary>转换路径格式</summary>
        public static string ConvertPath(string path) => VirtualMachine.ConvertPath(path);

        #endregion
    }
}
