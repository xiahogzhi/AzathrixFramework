#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;

namespace  Azathrix.UpmEditor.Editor.Core
{
    /// <summary>
    /// Validation utilities for UPM packages
    /// </summary>
    public static class UPMPackageValidator
    {
        private static readonly Regex PackageNameRegex = new Regex(@"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$");
        private static readonly Regex SemverRegex = new Regex(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?(\+[a-zA-Z0-9.-]+)?$");

        /// <summary>
        /// Check if directory contains a valid package.json
        /// </summary>
        public static bool HasValidPackageJson(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return false;

            var fullPath = Path.GetFullPath(directoryPath);
            var packageJsonPath = Path.Combine(fullPath, UPMConstants.PackageJsonFileName);
            return File.Exists(packageJsonPath);
        }

        /// <summary>
        /// Validate package name format (reverse domain notation)
        /// </summary>
        public static bool IsValidPackageName(string name, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(name))
            {
                error = "Package name cannot be empty";
                return false;
            }

            if (name != name.ToLowerInvariant())
            {
                error = "Package name must be lowercase";
                return false;
            }

            if (!PackageNameRegex.IsMatch(name))
            {
                error = "Package name must follow reverse domain notation (e.g., com.company.package)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate version format (semver), allows empty
        /// </summary>
        public static bool IsValidVersion(string version, out string error)
        {
            error = null;

            // 允许空版本
            if (string.IsNullOrEmpty(version))
                return true;

            if (!SemverRegex.IsMatch(version))
            {
                error = "Version must follow semantic versioning (e.g., 1.0.0)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check if path is in Assets folder
        /// </summary>
        public static bool IsInAssetsFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.Replace("\\", "/");
            return normalized.StartsWith(UPMConstants.AssetsPath + "/") || normalized == UPMConstants.AssetsPath;
        }

        /// <summary>
        /// Check if path is in Packages folder
        /// </summary>
        public static bool IsInPackagesFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var normalized = path.Replace("\\", "/");
            return normalized.StartsWith(UPMConstants.PackagesPath + "/") || normalized == UPMConstants.PackagesPath;
        }

        /// <summary>
        /// Check if a package is a local (embedded) package
        /// </summary>
        public static bool IsLocalPackage(string packagePath)
        {
            if (!IsInPackagesFolder(packagePath)) return false;

            var fullPath = Path.GetFullPath(packagePath).Replace("\\", "/").TrimEnd('/');
            var packagesFullPath = Path.GetFullPath(UPMConstants.PackagesPath).Replace("\\", "/").TrimEnd('/');

            // Local packages are directories directly under Packages/
            var parentDir = Path.GetDirectoryName(fullPath)?.Replace("\\", "/");
            return Directory.Exists(fullPath) && parentDir == packagesFullPath;
        }

        /// <summary>
        /// Validate entire package data
        /// </summary>
        public static ValidationResult ValidatePackageData(UPMPackageData data)
        {
            var result = new ValidationResult();

            if (!IsValidPackageName(data.name, out string nameError))
                result.Errors.Add(nameError);

            if (string.IsNullOrEmpty(data.displayName))
                result.Warnings.Add("Display name is empty");

            if (!IsValidVersion(data.version, out string versionError))
                result.Errors.Add(versionError);

            if (string.IsNullOrEmpty(data.unity))
                result.Warnings.Add("Unity version is not specified");

            if (string.IsNullOrEmpty(data.description))
                result.Warnings.Add("Description is empty");

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        /// <summary>
        /// Convert package name to namespace
        /// com.company.name1.name2 → Company.Name1.Name2
        /// com.company.name1-name2 → Company.Name1Name2
        /// </summary>
        public static string PackageNameToNamespace(string packageName)
        {
            if (string.IsNullOrEmpty(packageName)) return "";

            var parts = packageName.Split('.');

            // 移除 com/org/net 前缀
            int startIndex = 0;
            if (parts.Length > 1 && (parts[0] == "com" || parts[0] == "org" || parts[0] == "net"))
                startIndex = 1;

            var result = new System.Collections.Generic.List<string>();
            for (int i = startIndex; i < parts.Length; i++)
            {
                // 处理 - 分隔的单词，合并并首字母大写
                var words = parts[i].Split('-');
                var combined = "";
                foreach (var word in words)
                {
                    if (word.Length > 0)
                        combined += char.ToUpper(word[0]) + word.Substring(1);
                }
                if (!string.IsNullOrEmpty(combined))
                    result.Add(combined);
            }

            return string.Join(".", result);
        }
    }

    /// <summary>
    /// Result of package validation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid = true;
        public System.Collections.Generic.List<string> Errors = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> Warnings = new System.Collections.Generic.List<string>();
    }
}
#endif
