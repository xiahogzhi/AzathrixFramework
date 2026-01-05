using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.VM;
using System.Diagnostics;
using Azathrix.MiniPanda.Core;
using UnityEngine;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 性能压力测试
    /// </summary>
    [TestFixture]
    public class PerformanceTests
    {
        private MiniPanda _vm;
        private Stopwatch _sw;

        [SetUp]
        public void Setup()
        {
            _vm = new MiniPanda();
            _vm.Start();
            _sw = new Stopwatch();
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Shutdown();
        }

        private long MeasureGCAlloc(System.Action action, int iterations)
        {
            // 预热
            action();

            // 强制 GC
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            long before = System.GC.GetTotalMemory(false);

            for (int i = 0; i < iterations; i++)
            {
                action();
            }

            // 不强制 GC，直接测量
            long after = System.GC.GetTotalMemory(false);

            return after - before;
        }

        // ========== GC 分配测试 ==========

        [Test]
        public void GC_Eval_SimpleExpression()
        {
            const int iterations = 1000;
            long allocated = MeasureGCAlloc(() => _vm.Eval("1 + 2 * 3"), iterations);
            UnityEngine.Debug.Log($"[GC] Eval 简单表达式 x{iterations}: 分配 {allocated / 1024.0:F2}KB ({allocated / iterations}B/次)");
        }

        [Test]
        public void GC_Run_Loop()
        {
            const string code = @"
                var sum = 0
                for i in range(1000) {
                    sum = sum + i
                }
                return sum
            ";

            const int iterations = 100;
            long allocated = MeasureGCAlloc(() => _vm.Run(code), iterations);
            UnityEngine.Debug.Log($"[GC] Run 循环1000次 x{iterations}: 分配 {allocated / 1024.0:F2}KB ({allocated / iterations}B/次)");
        }

        [Test]
        public void GC_SetAndEval()
        {
            int counter = 0;
            const int iterations = 1000;
            long allocated = MeasureGCAlloc(() =>
            {
                _vm.SetGlobal("x", Value.FromNumber(counter++));
                _vm.Eval("x * 2");
            }, iterations);
            UnityEngine.Debug.Log($"[GC] Set+Eval x{iterations}: 分配 {allocated / 1024.0:F2}KB ({allocated / iterations}B/次)");
        }

        [Test]
        public void GC_ObjectCreation()
        {
            const string code = @"
                var arr = []
                for i in range(100) {
                    push(arr, { x: i, y: i * 2 })
                }
                return len(arr)
            ";

            const int iterations = 100;
            long allocated = MeasureGCAlloc(() => _vm.Run(code), iterations);
            UnityEngine.Debug.Log($"[GC] 对象创建100个 x{iterations}: 分配 {allocated / 1024.0:F2}KB ({allocated / iterations}B/次)");
        }

        [Test]
        public void GC_StringConcat()
        {
            const int iterations = 1000;
            long allocated = MeasureGCAlloc(() => _vm.Eval("\"hello\" + \" \" + \"world\""), iterations);
            UnityEngine.Debug.Log($"[GC] 字符串拼接 x{iterations}: 分配 {allocated / 1024.0:F2}KB ({allocated / iterations}B/次)");
        }

        // ========== Eval 性能测试 ==========

        [Test]
        public void Perf_Eval_SimpleExpression_10000()
        {
            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.Eval("1 + 2 * 3");
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] Eval 简单表达式 x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        [Test]
        public void Perf_Eval_FunctionCall_10000()
        {
            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.Run("func add(a, b) { return a + b } return add(1, 2)");
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] Run 函数定义+调用 x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        [Test]
        public void Perf_Eval_StringConcat_10000()
        {
            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.Eval("\"hello\" + \" \" + \"world\"");
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] Eval 字符串拼接 x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        // ========== Run 性能测试 ==========

        [Test]
        public void Perf_Run_Loop_100000()
        {
            const string code = @"
                var sum = 0
                for i in range(100000) {
                    sum = sum + i
                }
                return sum
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] Run 循环 100000 次: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        [Test]
        public void Perf_Run_Recursion_Fib20()
        {
            const string code = @"
                func fib(n) {
                    if n <= 1 return n
                    return fib(n - 1) + fib(n - 2)
                }
                return fib(20)
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] Run 递归 fib(20): {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        [Test]
        public void Perf_Run_ObjectCreation_10000()
        {
            const string code = @"
                var arr = []
                for i in range(10000) {
                    push(arr, { x: i, y: i * 2 })
                }
                return len(arr)
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] Run 对象创建 10000 个: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        // ========== 环境设置性能测试 ==========

        [Test]
        public void Perf_SetGlobal_10000()
        {
            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.SetGlobal("testVar", Value.FromNumber(i));
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] SetGlobal x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        [Test]
        public void Perf_GetGlobal_10000()
        {
            _vm.SetGlobal("testVar", Value.FromNumber(42));

            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.GetGlobal("testVar");
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] GetGlobal x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        [Test]
        public void Perf_SetAndEval_10000()
        {
            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.SetGlobal("x", Value.FromNumber(i));
                _vm.Eval("x * 2");
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] Set+Eval x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        // ========== 方法调用性能测试 ==========

        [Test]
        public void Perf_MethodCall_10000()
        {
            const string code = @"
                class Counter {
                    func Counter() { this.value = 0 }
                    func inc() { this.value = this.value + 1 }
                    func get() { return this.value }
                }
                var counter = Counter()
                for i in range(10000) {
                    counter.inc()
                }
                return counter.get()
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] 方法调用 x10000: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        [Test]
        public void Perf_PropertyAccess_10000()
        {
            const string code = @"
                var obj = { x: 1, y: 2, z: 3 }
                var sum = 0
                for i in range(10000) {
                    sum = sum + obj.x + obj.y + obj.z
                }
                return sum
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] 属性访问 x10000: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        // ========== 数组操作性能测试 ==========

        [Test]
        public void Perf_ArrayPush_10000()
        {
            const string code = @"
                var arr = []
                for i in range(10000) {
                    push(arr, i)
                }
                return len(arr)
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] 数组 push 10000 次: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        [Test]
        public void Perf_ArrayIteration_10000()
        {
            const string code = @"
                var arr = []
                for i in range(10000) {
                    push(arr, i)
                }
                var sum = 0
                for x in arr {
                    sum = sum + x
                }
                return sum
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] 数组迭代 10000 元素: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }

        // ========== 编译性能测试 ==========

        [Test]
        public void Perf_Compile_SimpleCode_1000()
        {
            const string code = "var x = 1 + 2 * 3\nreturn x";

            const int iterations = 1000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.Run(code);
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] 编译+执行简单代码 x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        [Test]
        public void Perf_Compile_ComplexCode_100()
        {
            const string code = @"
                func factorial(n) {
                    if n <= 1 return 1
                    return n * factorial(n - 1)
                }

                var results = []
                for i in range(1, 11) {
                    push(results, factorial(i))
                }
                return len(results)
            ";

            const int iterations = 100;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                var vm = new MiniPanda();
                vm.Start();
                vm.Run(code);
                vm.Shutdown();
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] 新建VM+编译+执行复杂代码 x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        // ========== Native 函数调用性能测试 ==========

        [Test]
        public void Perf_NativeCall_10000()
        {
            const int iterations = 10000;
            _sw.Restart();

            for (int i = 0; i < iterations; i++)
            {
                _vm.Eval("len(\"hello\")");
            }

            _sw.Stop();
            UnityEngine.Debug.Log($"[Perf] Native 函数调用 x{iterations}: {_sw.ElapsedMilliseconds}ms ({_sw.ElapsedMilliseconds * 1000.0 / iterations:F2}μs/次)");
        }

        // ========== 字符串插值性能测试 ==========

        [Test]
        public void Perf_StringInterpolation_10000()
        {
            const string code = @"
                var name = ""world""
                var count = 42
                var result = """"
                for i in range(10000) {
                    result = ""Hello {name} count is {count}""
                }
                return result
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] 字符串插值 x10000: {_sw.ElapsedMilliseconds}ms");
        }

        // ========== 闭包性能测试 ==========

        [Test]
        public void Perf_Closure_10000()
        {
            const string code = @"
                func makeCounter() {
                    var count = 0
                    return () => {
                        count = count + 1
                        return count
                    }
                }
                var counter = makeCounter()
                for i in range(10000) {
                    counter()
                }
                return counter()
            ";

            _sw.Restart();
            var result = _vm.Run(code);
            _sw.Stop();

            UnityEngine.Debug.Log($"[Perf] 闭包调用 x10000: {_sw.ElapsedMilliseconds}ms, 结果={result.AsNumber()}");
        }
    }
}
