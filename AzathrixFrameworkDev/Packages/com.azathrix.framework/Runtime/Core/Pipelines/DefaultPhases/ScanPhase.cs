using System;
using System.Diagnostics;
using Azathrix.Framework.Core.Pipelines.Phases;
using Azathrix.Framework.Tools;
using Cysharp.Threading.Tasks;

namespace Azathrix.Framework.Core.Pipelines.DefaultPhases
{
    /// <summary>
    /// 系统扫描阶段
    /// </summary>
    [PhaseOrder(300)]
    public class ScanPhase : IScanPhase
    {
        public async UniTask ExecuteAsync(PhaseContext context)
        {
            Log.Separator("Scan 阶段");
            Log.Info("[Scan] 开始扫描系统类型...");

            var scanner = new SystemScanner(AzathrixFramework.Logger, AzathrixFramework.ScannerConfig);
            var watch = Stopwatch.StartNew();
            var scannedTypes = await scanner.ScanAsync();
            watch.Stop();

            Log.Info($"[Scan] 完成，发现 {scannedTypes.Length} 个系统，耗时: {watch.Elapsed.TotalMilliseconds:F2}ms");

            // 合并手动指定的类型
            var runtimeConfig = AzathrixFramework.RuntimeConfig;
            if (runtimeConfig.ManualSystemTypes.Count > 0)
            {
                Log.Info($"[Scan] 合并 {runtimeConfig.ManualSystemTypes.Count} 个手动指定的系统");
                var allTypes = new Type[scannedTypes.Length + runtimeConfig.ManualSystemTypes.Count];
                scannedTypes.CopyTo(allTypes, 0);
                runtimeConfig.ManualSystemTypes.CopyTo(allTypes, scannedTypes.Length);
                scannedTypes = allTypes;
            }

            context.ScannedSystemTypes = scannedTypes;
        }
    }
}
