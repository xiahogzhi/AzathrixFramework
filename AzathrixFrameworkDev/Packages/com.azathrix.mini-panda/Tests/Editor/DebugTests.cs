using NUnit.Framework;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Debug;

namespace Azathrix.MiniPanda.Tests
{
    /// <summary>
    /// 调试功能测试
    /// </summary>
    [TestFixture]
    public class DebugTests
    {
        private MiniPanda _vm;
        private Debugger _debugger;

        [SetUp]
        public void Setup()
        {
            _vm = new MiniPanda();
            _vm.Start();
            _debugger = new Debugger { Enabled = true };
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Shutdown();
        }

        [Test]
        public void Debugger_AddBreakpoint()
        {
            var bp = _debugger.AddBreakpoint("test.panda", 10);
            Assert.IsNotNull(bp);
            Assert.AreEqual(10, bp.Line);
            Assert.IsTrue(bp.Enabled);
        }

        [Test]
        public void Debugger_RemoveBreakpoint()
        {
            _debugger.AddBreakpoint("test.panda", 10);
            var removed = _debugger.RemoveBreakpoint("test.panda", 10);
            Assert.IsTrue(removed);

            removed = _debugger.RemoveBreakpoint("test.panda", 10);
            Assert.IsFalse(removed);
        }

        [Test]
        public void Debugger_CheckBreakpoint()
        {
            _debugger.AddBreakpoint("test.panda", 10);

            var hit = _debugger.CheckBreakpoint("test.panda", 10, out var bp);
            Assert.IsTrue(hit);
            Assert.IsNotNull(bp);
            Assert.AreEqual(1, bp.HitCount);

            hit = _debugger.CheckBreakpoint("test.panda", 5, out bp);
            Assert.IsFalse(hit);
        }

        [Test]
        public void Debugger_ClearBreakpoints()
        {
            _debugger.AddBreakpoint("test.panda", 10);
            _debugger.AddBreakpoint("test.panda", 20);
            _debugger.ClearBreakpoints("test.panda");

            var hit = _debugger.CheckBreakpoint("test.panda", 10, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void Debugger_ConditionalBreakpoint()
        {
            var bp = _debugger.AddBreakpoint("test.panda", 10, "x > 5");
            Assert.AreEqual("x > 5", bp.Condition);
        }

        [Test]
        public void Debugger_DisabledBreakpoint()
        {
            var bp = _debugger.AddBreakpoint("test.panda", 10);
            bp.Enabled = false;

            var hit = _debugger.CheckBreakpoint("test.panda", 10, out _);
            Assert.IsFalse(hit);
        }

        [Test]
        public void Debugger_StepMode()
        {
            _debugger.StepIn();
            Assert.IsFalse(_debugger.IsPaused);

            var shouldStop = _debugger.ShouldStop("test.panda", 1, 1, out var reason);
            Assert.IsTrue(shouldStop);
            Assert.AreEqual(StopReason.StepIn, reason);
        }

        [Test]
        public void Debugger_Continue()
        {
            _debugger.Pause();
            Assert.IsTrue(_debugger.IsPaused);

            _debugger.Continue();
            Assert.IsFalse(_debugger.IsPaused);
        }

        [Test]
        public void Debugger_PathNormalization()
        {
            // 测试路径规范化（Windows/Unix 路径）
            _debugger.AddBreakpoint("C:\\test\\script.panda", 10);

            var hit = _debugger.CheckBreakpoint("c:/test/script.panda", 10, out _);
            Assert.IsTrue(hit);
        }

        [Test]
        public void DebugInfo_AddMapping()
        {
            var info = new DebugInfo { SourceFile = "test.panda" };
            info.AddMapping(0, 1);
            info.AddMapping(5, 2);
            info.AddMapping(10, 3);

            Assert.AreEqual(1, info.GetLine(0));
            Assert.AreEqual(2, info.GetLine(5));
            Assert.AreEqual(3, info.GetLine(10));
            Assert.AreEqual(-1, info.GetLine(100));
        }

        [Test]
        public void DebugInfo_GetFirstOffset()
        {
            var info = new DebugInfo();
            info.AddMapping(5, 10);
            info.AddMapping(8, 10);

            Assert.AreEqual(5, info.GetFirstOffset(10));
            Assert.AreEqual(-1, info.GetFirstOffset(20));
        }
    }
}
