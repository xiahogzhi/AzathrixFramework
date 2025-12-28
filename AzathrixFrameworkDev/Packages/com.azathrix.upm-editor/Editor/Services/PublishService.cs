#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace  Azathrix.UpmEditor.Editor.Services
{
    /// <summary>
    /// Service for packing and publishing UPM packages
    /// </summary>
    public static class PublishService
    {
        private const string PrefsRegistryKey = "UPMEditor_Registry";
        private const string DefaultRegistry = "http://localhost:4873";

        /// <summary>
        /// Get saved registry URL
        /// </summary>
        public static string GetRegistry()
        {
            return EditorPrefs.GetString(PrefsRegistryKey, DefaultRegistry);
        }

        /// <summary>
        /// Save registry URL
        /// </summary>
        public static void SetRegistry(string registry)
        {
            EditorPrefs.SetString(PrefsRegistryKey, registry);
        }

        /// <summary>
        /// Publish a .tgz file to registry using npm publish
        /// </summary>
        public static PublishResult PublishTgz(string tgzPath, string registry = null)
        {
            var result = new PublishResult { PackagePath = tgzPath };

            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                result.Success = false;
                result.ErrorMessage = "npm not found. Please install Node.js and restart Unity.";
                return result;
            }

            if (!File.Exists(tgzPath))
            {
                result.Success = false;
                result.ErrorMessage = $"tgz file not found: {tgzPath}";
                return result;
            }

            registry = registry ?? GetRegistry();

            try
            {
                var args = $"publish \"{tgzPath}\" --registry {registry}";
                Debug.Log($"[npm] {args}");

                var (exitCode, stdout, stderr) = RunNpmCommand(npmPath, args, Path.GetDirectoryName(tgzPath));

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log(stdout);

                if (exitCode == 0)
                {
                    result.Success = true;
                    Debug.Log($"<color=green>Package published successfully to {registry}</color>");

                    // 删除 tgz 文件
                    try { File.Delete(tgzPath); } catch { }
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = !string.IsNullOrEmpty(stderr) ? stderr : $"npm publish failed with code {exitCode}";
                    Debug.LogError($"Publish failed: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Publish error: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Unpublish package version from registry
        /// </summary>
        public static PublishResult Unpublish(string packageName, string version, string registry = null)
        {
            var result = new PublishResult();

            var npmPath = FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                result.Success = false;
                result.ErrorMessage = "npm not found. Please install Node.js and restart Unity.";
                return result;
            }

            registry = registry ?? GetRegistry();

            try
            {
                var args = $"unpublish {packageName}@{version} --registry {registry}";
                Debug.Log($"[npm] {args}");

                var (exitCode, stdout, stderr) = RunNpmCommand(npmPath, args, Directory.GetCurrentDirectory());

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log(stdout);

                if (exitCode == 0)
                {
                    result.Success = true;
                    Debug.Log($"<color=green>Package {packageName}@{version} unpublished from {registry}</color>");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = !string.IsNullOrEmpty(stderr) ? stderr : $"npm unpublish failed with code {exitCode}";
                    Debug.LogError($"Unpublish failed: {result.ErrorMessage}");
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"Unpublish error: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Generate scoped registry config for manifest.json
        /// </summary>
        public static string GenerateScopedRegistryConfig(string packageName, string registry = null)
        {
            registry = registry ?? GetRegistry();
            var scope = packageName.Contains(".")
                ? string.Join(".", packageName.Split('.')[0], packageName.Split('.')[1])
                : packageName;

            return $@"{{
  ""scopedRegistries"": [
    {{
      ""name"": ""Private Registry"",
      ""url"": ""{registry}"",
      ""scopes"": [""{scope}""]
    }}
  ]
}}";
        }

        /// <summary>
        /// Check if npm is available
        /// </summary>
        public static bool IsNpmAvailable()
        {
            return !string.IsNullOrEmpty(FindNpmPath());
        }

        #region Unity Signing

        private const string PrefsUnityUsernameKey = "UPMEditor_UnityUsername";
        private const string PrefsCloudOrgIdKey = "UPMEditor_CloudOrgId";

        /// <summary>
        /// Get saved Unity username
        /// </summary>
        public static string GetUnityUsername()
        {
            return EditorPrefs.GetString(PrefsUnityUsernameKey, "");
        }

        /// <summary>
        /// Save Unity username
        /// </summary>
        public static void SetUnityUsername(string username)
        {
            EditorPrefs.SetString(PrefsUnityUsernameKey, username);
        }

        /// <summary>
        /// Get saved Cloud Organization ID
        /// </summary>
        public static string GetCloudOrgId()
        {
            return EditorPrefs.GetString(PrefsCloudOrgIdKey, "");
        }

        /// <summary>
        /// Save Cloud Organization ID
        /// </summary>
        public static void SetCloudOrgId(string orgId)
        {
            EditorPrefs.SetString(PrefsCloudOrgIdKey, orgId);
        }

        /// <summary>
        /// Pack package with Unity signature using -upmPack command
        /// Requires Unity 6.3+
        /// </summary>
        public static PackResult PackWithSignature(string packagePath, string outputDirectory, string username, string password, string cloudOrgId)
        {
            var result = new PackResult { PackagePath = packagePath };

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                result.Success = false;
                result.ErrorMessage = "Unity ID 用户名和密码不能为空";
                return result;
            }

            if (string.IsNullOrEmpty(cloudOrgId))
            {
                result.Success = false;
                result.ErrorMessage = "Cloud Organization ID 不能为空";
                return result;
            }

            var fullPackagePath = Path.GetFullPath(packagePath);
            if (!Directory.Exists(fullPackagePath))
            {
                result.Success = false;
                result.ErrorMessage = $"包目录不存在: {fullPackagePath}";
                return result;
            }

            // 使用系统临时目录，避免 Unity 自动添加 repository
            var tempDir = Path.Combine(Path.GetTempPath(), $"upm_pack_{Guid.NewGuid():N}");
            var tempPackageDir = Path.Combine(tempDir, "package");
            var tempOutputDir = Path.Combine(tempDir, "output");
            Directory.CreateDirectory(tempPackageDir);
            Directory.CreateDirectory(tempOutputDir);

            var outputDir = string.IsNullOrEmpty(outputDirectory)
                ? fullPackagePath
                : Path.GetFullPath(outputDirectory);

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var unityPath = EditorApplication.applicationPath;
            var projectPath = Path.GetDirectoryName(Application.dataPath);

            Debug.Log($"[Unity Sign] Unity 路径: {unityPath}");
            Debug.Log($"[Unity Sign] 包路径: {fullPackagePath}");
            Debug.Log($"[Unity Sign] 临时目录: {tempDir}");
            Debug.Log($"[Unity Sign] 输出路径: {outputDir}");

            try
            {
                // 复制包内容到临时目录（不包含 .git）
                CopyDirectory(fullPackagePath, tempPackageDir);

                var logFile = Path.Combine(tempDir, "upm_pack_log.txt");
                var args = $"-batchmode -projectPath \"{projectPath}\" " +
                           $"-username \"{username}\" -password \"{password}\" " +
                           $"-upmPack \"{tempPackageDir}\" \"{tempOutputDir}\" " +
                           $"-cloudOrganization \"{cloudOrgId}\" " +
                           $"-logFile \"{logFile}\"";

                var (exitCode, stdout, stderr) = RunCommand(unityPath, args);

                // Find the generated tgz file
                var packageData = PackageJsonService.ReadPackageJson(packagePath);
                var expectedTgz = $"{packageData.name}-{packageData.version}.tgz";
                var tempTgzPath = Path.Combine(tempOutputDir, expectedTgz);

                if (File.Exists(tempTgzPath))
                {
                    // 移动到目标目录
                    var finalTgzPath = Path.Combine(outputDir, expectedTgz);
                    if (File.Exists(finalTgzPath))
                        File.Delete(finalTgzPath);
                    File.Move(tempTgzPath, finalTgzPath);

                    result.Success = true;
                    result.TgzPath = finalTgzPath;
                    Debug.Log($"<color=green>签名包已生成:</color> {finalTgzPath}");
                }
                else
                {
                    // Try to find any tgz file
                    var tgzFiles = Directory.GetFiles(tempOutputDir, "*.tgz");
                    if (tgzFiles.Length > 0)
                    {
                        var finalTgzPath = Path.Combine(outputDir, Path.GetFileName(tgzFiles[0]));
                        if (File.Exists(finalTgzPath))
                            File.Delete(finalTgzPath);
                        File.Move(tgzFiles[0], finalTgzPath);

                        result.Success = true;
                        result.TgzPath = finalTgzPath;
                        Debug.Log($"<color=green>签名包已生成:</color> {result.TgzPath}");
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"打包完成但未找到 .tgz 文件\n退出码: {exitCode}\n{stderr}";
                    }
                }
            }
            catch (Exception e)
            {
                result.Success = false;
                result.ErrorMessage = e.Message;
                Debug.LogError($"签名打包错误: {e.Message}");
            }
            finally
            {
                // 清理临时目录
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }

            return result;
        }

        /// <summary>
        /// 复制目录（排除 .git）
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == ".git") continue;
                CopyDirectory(dir, Path.Combine(destDir, dirName));
            }
        }

        private static (int exitCode, string stdout, string stderr) RunCommand(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            // 异步读取避免死锁
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit(60000); // 60秒超时

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;

            return (process.ExitCode, stdout, stderr);
        }

        #endregion

        private static (int exitCode, string stdout, string stderr) RunNpmCommand(string npmPath, string args, string workDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = npmPath,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            // 异步读取避免死锁
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit(60000); // 60秒超时

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;

            return (process.ExitCode, stdout, stderr);
        }

        private static string FindNpmPath()
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd"),
                @"C:\Program Files\nodejs\npm.cmd",
                "/usr/local/bin/npm",
                "/usr/bin/npm",
                "/opt/homebrew/bin/npm"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var p in pathEnv.Split(Path.PathSeparator))
            {
                var npm = Path.Combine(p, Application.platform == RuntimePlatform.WindowsEditor ? "npm.cmd" : "npm");
                if (File.Exists(npm)) return npm;
            }

            return null;
        }
    }

    /// <summary>
    /// Result of pack operation
    /// </summary>
    public class PackResult
    {
        public bool Success;
        public string PackagePath;
        public string TgzPath;
        public string ErrorMessage;
    }

    /// <summary>
    /// Result of publish operation
    /// </summary>
    public class PublishResult
    {
        public bool Success;
        public string PackagePath;
        public string ErrorMessage;
    }
}
#endif
