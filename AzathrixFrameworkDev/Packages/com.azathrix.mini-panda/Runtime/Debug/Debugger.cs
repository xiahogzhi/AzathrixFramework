using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Azathrix.MiniPanda.Core;

namespace Azathrix.MiniPanda.Debug
{
    /// <summary>
    /// 调试停止原因
    /// </summary>
    public enum StopReason
    {
        Entry,          // 程序入口
        Breakpoint,     // 命中断点
        Step,           // 单步执行
        StepIn,         // 步入
        StepOut,        // 步出
        Pause,          // 暂停
        Exception       // 异常
    }

    /// <summary>
    /// 调试事件参数
    /// </summary>
    public class DebugEventArgs : EventArgs
    {
        public StopReason Reason { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// 调试器 - 管理断点和调试状态（线程安全）
    /// </summary>
    public class Debugger
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, Breakpoint>> _breakpoints =
            new ConcurrentDictionary<string, ConcurrentDictionary<int, Breakpoint>>(StringComparer.OrdinalIgnoreCase);
        private int _nextBreakpointId = 1;
        private volatile bool _isPaused;
        private volatile bool _stepMode;
        private volatile StepType _stepType;
        private volatile int _stepFrameDepth;
        private readonly object _stepLock = new object();

        /// <summary>是否启用调试</summary>
        public bool Enabled { get; set; }

        /// <summary>是否暂停</summary>
        public bool IsPaused => _isPaused;

        /// <summary>调试停止事件</summary>
        public event EventHandler<DebugEventArgs> Stopped;

        /// <summary>调试继续事件</summary>
        public event EventHandler Continued;

        /// <summary>输出事件</summary>
        public event EventHandler<string> Output;

        private enum StepType { None, Over, In, Out }

        /// <summary>
        /// 添加断点
        /// </summary>
        public Breakpoint AddBreakpoint(string file, int line, string condition = null)
        {
            var normalizedFile = NormalizePath(file);
            var fileBreakpoints = _breakpoints.GetOrAdd(normalizedFile, _ => new ConcurrentDictionary<int, Breakpoint>());

            var bp = new Breakpoint
            {
                Id = Interlocked.Increment(ref _nextBreakpointId),
                File = normalizedFile,
                Line = line,
                Condition = condition
            };
            fileBreakpoints[line] = bp;
            return bp;
        }

        /// <summary>
        /// 移除断点
        /// </summary>
        public bool RemoveBreakpoint(string file, int line)
        {
            var normalizedFile = NormalizePath(file);

            if (_breakpoints.TryGetValue(normalizedFile, out var fileBreakpoints))
            {
                return fileBreakpoints.TryRemove(line, out _);
            }
            return false;
        }

        /// <summary>
        /// 清除文件的所有断点
        /// </summary>
        public void ClearBreakpoints(string file)
        {
            var normalizedFile = NormalizePath(file);
            _breakpoints.TryRemove(normalizedFile, out _);
        }

        public void ClearAllBreakpoints()
        {
            _breakpoints.Clear();
        }

        /// <summary>
        /// 获取文件的所有断点
        /// </summary>
        public IEnumerable<Breakpoint> GetBreakpoints(string file)
        {
            var normalizedFile = NormalizePath(file);
            if (_breakpoints.TryGetValue(normalizedFile, out var fileBreakpoints))
            {
                return fileBreakpoints.Values;
            }
            return Array.Empty<Breakpoint>();
        }

        /// <summary>
        /// 检查是否命中断点
        /// </summary>
        public bool CheckBreakpoint(string file, int line, out Breakpoint breakpoint)
        {
            breakpoint = null;
            if (!Enabled) return false;

            var normalizedFile = NormalizePath(file);

            // 快路径：精确匹配文件
            if (_breakpoints.TryGetValue(normalizedFile, out var exactBreakpoints))
            {
                if (exactBreakpoints.TryGetValue(line, out breakpoint) && breakpoint.Enabled)
                {
                    breakpoint.HitCount++;
                    return true;
                }
            }

            foreach (var kvp in _breakpoints)
            {
                if (PathsMatch(kvp.Key, normalizedFile))
                {
                    if (kvp.Value.TryGetValue(line, out breakpoint) && breakpoint.Enabled)
                    {
                        breakpoint.HitCount++;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 检查是否应该停止（断点或单步）
        /// </summary>
        public bool ShouldStop(string file, int line, int frameDepth, out StopReason reason)
        {
            reason = StopReason.Step;
            if (!Enabled) return false;

            // 检查断点
            if (CheckBreakpoint(file, line, out _))
            {
                reason = StopReason.Breakpoint;
                return true;
            }

            // 检查单步模式（需要锁保护读取一致性）
            lock (_stepLock)
            {
                if (_stepMode)
                {
                    switch (_stepType)
                    {
                        case StepType.In:
                            reason = StopReason.StepIn;
                            return true;
                        case StepType.Over:
                            if (frameDepth <= _stepFrameDepth)
                            {
                                reason = StopReason.Step;
                                return true;
                            }
                            break;
                        case StepType.Out:
                            if (frameDepth < _stepFrameDepth)
                            {
                                reason = StopReason.StepOut;
                                return true;
                            }
                            break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 暂停执行
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// 继续执行
        /// </summary>
        public void Continue()
        {
            lock (_stepLock)
            {
                _isPaused = false;
                _stepMode = false;
                _stepType = StepType.None;
            }
            Continued?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 单步执行（步过）
        /// </summary>
        public void StepOver(int currentFrameDepth)
        {
            lock (_stepLock)
            {
                _isPaused = false;
                _stepMode = true;
                _stepType = StepType.Over;
                _stepFrameDepth = currentFrameDepth;
            }
        }

        /// <summary>
        /// 单步执行（步入）
        /// </summary>
        public void StepIn()
        {
            lock (_stepLock)
            {
                _isPaused = false;
                _stepMode = true;
                _stepType = StepType.In;
            }
        }

        /// <summary>
        /// 单步执行（步出）
        /// </summary>
        public void StepOut(int currentFrameDepth)
        {
            lock (_stepLock)
            {
                _isPaused = false;
                _stepMode = true;
                _stepType = StepType.Out;
                _stepFrameDepth = currentFrameDepth;
            }
        }

        /// <summary>
        /// 触发停止事件
        /// </summary>
        public void OnStopped(StopReason reason, string file, int line, string message = null)
        {
            _isPaused = true;
            _stepMode = false;
            Stopped?.Invoke(this, new DebugEventArgs
            {
                Reason = reason,
                File = file,
                Line = line,
                Message = message
            });
        }

        /// <summary>
        /// 触发输出事件
        /// </summary>
        public void OnOutput(string message)
        {
            Output?.Invoke(this, message);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            // 移除 ./ 前缀
            if (path.StartsWith("./")) path = path.Substring(2);
            if (path.StartsWith(".\\")) path = path.Substring(2);

            // 统一使用正斜杠并转小写
            path = path.Replace('\\', '/').ToLowerInvariant();

            // 提取文件名用于匹配（处理绝对路径和相对路径的差异）
            // 返回完整路径，但在匹配时会尝试多种方式
            return path;
        }

        /// <summary>
        /// 检查两个路径是否匹配（支持绝对路径和相对路径的比较）
        /// </summary>
        private static bool PathsMatch(string path1, string path2)
        {
            if (path1 == path2) return true;

            // 尝试用文件名 + 部分路径匹配
            // 例如: "g:/projects/.../samples/example.panda" 和 "packages/.../samples/example.panda"
            if (path1.EndsWith(path2) || path2.EndsWith(path1)) return true;

            // 提取最后几级目录进行匹配
            var parts1 = path1.Split('/');
            var parts2 = path2.Split('/');

            // 至少匹配文件名和上两级目录
            var minParts = Math.Min(Math.Min(parts1.Length, parts2.Length), 3);
            if (minParts < 1) return false;

            for (int i = 1; i <= minParts; i++)
            {
                if (parts1[parts1.Length - i] != parts2[parts2.Length - i])
                    return false;
            }
            return true;
        }
    }
}










