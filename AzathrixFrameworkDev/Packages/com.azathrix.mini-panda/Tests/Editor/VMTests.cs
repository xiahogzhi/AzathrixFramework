using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Exceptions;
using Azathrix.MiniPanda.VM;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 虚拟机测试（栈操作、边界检查等）
    /// </summary>
    [TestFixture]
    public class VMTests
    {
        private MiniPanda _vm;

        [SetUp]
        public void Setup()
        {
            _vm = new MiniPanda();
            _vm.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Shutdown();
        }

        // ========== 复合赋值测试 (使用 Dup2, SwapUnder, Rot3Under) ==========

        [Test]
        public void CompoundAssignment_PropertyPlusEquals()
        {
            // 属性复合赋值使用 Dup2 操作
            var result = _vm.Run(@"
                var obj = { value: 10 }
                obj.value += 5
                return obj.value
            ");
            Assert.AreEqual(15.0, result.AsNumber());
        }

        [Test]
        public void CompoundAssignment_ArrayIndexPlusEquals()
        {
            // 数组索引复合赋值使用 Dup2 操作
            var result = _vm.Run(@"
                var arr = [1, 2, 3]
                arr[1] += 10
                return arr[1]
            ");
            Assert.AreEqual(12.0, result.AsNumber());
        }

        [Test]
        public void CompoundAssignment_NestedProperty()
        {
            // 嵌套属性复合赋值
            var result = _vm.Run(@"
                var obj = { inner: { value: 5 } }
                obj.inner.value *= 3
                return obj.inner.value
            ");
            Assert.AreEqual(15.0, result.AsNumber());
        }

        [Test]
        public void CompoundAssignment_ChainedArrayIndex()
        {
            // 链式数组索引复合赋值
            var result = _vm.Run(@"
                var matrix = [[1, 2], [3, 4]]
                matrix[0][1] += 10
                return matrix[0][1]
            ");
            Assert.AreEqual(12.0, result.AsNumber());
        }

        [Test]
        public void CompoundAssignment_AllOperators()
        {
            // 测试所有复合赋值运算符
            var result = _vm.Run(@"
                var obj = { a: 10, b: 20, c: 5, d: 100, e: 7 }
                obj.a += 5   // 15
                obj.b -= 8   // 12
                obj.c *= 4   // 20
                obj.d /= 5   // 20
                obj.e %= 3   // 1
                return obj.a + obj.b + obj.c + obj.d + obj.e
            ");
            Assert.AreEqual(68.0, result.AsNumber());
        }

        // ========== 栈操作正确性测试 ==========

        [Test]
        public void Stack_DeepNesting_Works()
        {
            // 深层嵌套表达式测试栈操作
            var result = _vm.Run(@"
                func calc(a, b, c, d) {
                    return ((a + b) * (c - d)) / ((a - b) + (c + d))
                }
                return calc(10, 5, 8, 3)
            ");
            // ((10+5) * (8-3)) / ((10-5) + (8+3)) = (15 * 5) / (5 + 11) = 75 / 16 = 4.6875
            Assert.AreEqual(4.6875, result.AsNumber(), 0.0001);
        }

        [Test]
        public void Stack_RecursiveCall_Works()
        {
            // 递归调用测试栈帧管理
            var result = _vm.Run(@"
                func factorial(n) {
                    if n <= 1 return 1
                    return n * factorial(n - 1)
                }
                return factorial(10)
            ");
            Assert.AreEqual(3628800.0, result.AsNumber());
        }

        [Test]
        public void Stack_ClosureCapture_Works()
        {
            // 闭包捕获测试上值栈操作
            var result = _vm.Run(@"
                func makeCounter() {
                    var count = 0
                    return () => {
                        count = count + 1
                        return count
                    }
                }
                var counter = makeCounter()
                counter()
                counter()
                return counter()
            ");
            Assert.AreEqual(3.0, result.AsNumber());
        }

        // ========== 异常恢复测试 ==========

        [Test]
        public void Exception_RuntimeError_DoesNotCorruptState()
        {
            // 运行时错误后 VM 状态应该正确恢复
            Assert.Throws<MiniPandaRuntimeException>(() => _vm.Run(@"
                var x = nil
                return x.property
            "));

            // VM 应该仍然可以正常运行
            var result = _vm.Run("return 42");
            Assert.AreEqual(42.0, result.AsNumber());
        }

        [Test]
        public void Exception_DivisionByZero_Handled()
        {
            // 除零应该返回 Infinity 或 NaN，不应崩溃
            var result = _vm.Run("return 1 / 0");
            Assert.IsTrue(double.IsInfinity(result.AsNumber()));
        }

        [Test]
        public void Exception_StackOverflow_Handled()
        {
            // 深度递归应该抛出栈溢出异常，不应崩溃
            Assert.Throws<MiniPandaRuntimeException>(() => _vm.Run(@"
                func infinite() {
                    return infinite()
                }
                infinite()
            "));
        }

        // ========== 对象池测试 ==========

        [Test]
        public void ObjectPool_IteratorReuse()
        {
            // 验证迭代器被复用
            var result = _vm.Run(@"
                var count = 0
                for i in range(10) { count = count + 1 }
                for i in range(5) { count = count + 1 }
                return count
            ");
            Assert.AreEqual(15.0, result.AsNumber());
        }

        [Test]
        public void ObjectPool_NestedLoops()
        {
            // 验证嵌套循环的迭代器正确复用
            var result = _vm.Run(@"
                var sum = 0
                for i in range(3) {
                    for j in range(3) {
                        sum = sum + 1
                    }
                }
                return sum
            ");
            Assert.AreEqual(9.0, result.AsNumber());
        }

        [Test]
        public void ObjectPool_ArrayIteration()
        {
            // 数组迭代测试
            var result = _vm.Run(@"
                var arr = [1, 2, 3, 4, 5]
                var sum = 0
                for n in arr { sum = sum + n }
                return sum
            ");
            Assert.AreEqual(15.0, result.AsNumber());
        }

        [Test]
        public void ObjectPool_ObjectIteration()
        {
            // 对象迭代测试
            var result = _vm.Run(@"
                var obj = { a: 1, b: 2, c: 3 }
                var sum = 0
                for k, v in obj { sum = sum + v }
                return sum
            ");
            Assert.AreEqual(6.0, result.AsNumber());
        }

        [Test]
        public void ObjectPool_StringIteration()
        {
            // 字符串迭代测试
            var result = _vm.Run(@"
                var str = ""hello""
                var count = 0
                for ch in str { count = count + 1 }
                return count
            ");
            Assert.AreEqual(5.0, result.AsNumber());
        }

        [Test]
        public void ObjectPool_ResetWorks()
        {
            // 验证 Reset 后对象池被清空
            _vm.Run(@"
                for i in range(10) { }
            ");
            _vm.Reset();
            var result = _vm.Run(@"
                for i in range(5) { return 42 }
                return 0
            ");
            Assert.AreEqual(42.0, result.AsNumber());
        }
    }
}
