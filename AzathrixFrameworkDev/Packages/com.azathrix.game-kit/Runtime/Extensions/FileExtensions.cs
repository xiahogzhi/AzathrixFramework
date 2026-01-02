using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Azathrix.GameKit.Runtime.Extensions
{
    /// <summary>
    /// 文件路径扩展方法
    /// </summary>
    public static class FileExtensions
    {
        #region 文件获取

        /// <summary>
        /// 递归获取目录下所有文件（排除 .meta 文件）
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <returns>文件列表，目录不存在返回 null</returns>
        public static List<FileInfo> GetAllFiles(this string path)
        {
            var di = new DirectoryInfo(path);
            return di.Exists ? di.GetAllFiles() : null;
        }

        /// <summary>
        /// 递归获取目录下所有文件（排除 .meta 文件）
        /// </summary>
        /// <param name="di">目录信息</param>
        /// <param name="fi">文件列表（可选，用于递归）</param>
        /// <returns>文件列表</returns>
        public static List<FileInfo> GetAllFiles(this DirectoryInfo di, List<FileInfo> fi = null)
        {
            fi ??= new List<FileInfo>();

            foreach (var dir in di.GetDirectories())
                dir.GetAllFiles(fi);

            foreach (var file in di.GetFiles())
            {
                if (!file.Name.EndsWith(".meta"))
                    fi.Add(file);
            }

            return fi;
        }

        #endregion

        #region 路径转换

        /// <summary>
        /// 将 Windows 绝对路径转换为 Unity Assets 相对路径
        /// </summary>
        public static string ToUnityPath(this string windowsPath)
        {
            var path = windowsPath.Replace("\\", "/");
            return path.Replace(Application.dataPath, "Assets");
        }

        /// <summary>
        /// 将 Asset 路径转换为 Resources 路径
        /// </summary>
        /// <param name="assetPath">Asset 路径（如 Assets/Resources/Prefabs/Player.prefab）</param>
        /// <returns>Resources 路径（如 Prefabs/Player），非 Resources 路径返回 null</returns>
        public static string AssetPathToResourcesPath(this string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return assetPath;

            assetPath = assetPath.Replace("\\", "/");

            if (!assetPath.StartsWith("Assets/Resources/"))
                return null;

            assetPath = assetPath.Replace("Assets/Resources/", "");
            if (Path.HasExtension(assetPath))
                assetPath = assetPath.Replace(Path.GetExtension(assetPath), "");

            return assetPath;
        }

        /// <summary>
        /// 获取不带扩展名的文件名
        /// </summary>
        public static string GetFileNameWithoutExtension(this string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        public static string GetExtension(this string path)
        {
            return Path.GetExtension(path);
        }

        /// <summary>
        /// 获取目录路径
        /// </summary>
        public static string GetDirectoryName(this string path)
        {
            return Path.GetDirectoryName(path);
        }

        /// <summary>
        /// 合并路径
        /// </summary>
        public static string CombinePath(this string path, string path2)
        {
            return Path.Combine(path, path2);
        }

        #endregion

        #region 文件操作

        /// <summary>
        /// 确保目录存在，不存在则创建
        /// </summary>
        public static void EnsureDirectoryExists(this string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 安全删除文件
        /// </summary>
        public static bool TryDeleteFile(this string path)
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        #endregion
    }
}
