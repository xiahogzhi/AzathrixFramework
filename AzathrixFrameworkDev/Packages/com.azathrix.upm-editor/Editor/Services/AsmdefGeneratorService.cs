#if UNITY_EDITOR
using System.IO;
using System.Text;
using Azathrix.UpmEditor.Editor.Core;

namespace  Azathrix.UpmEditor.Editor.Services
{
    /// <summary>
    /// Service for generating assembly definition files
    /// </summary>
    public static class AsmdefGeneratorService
    {
        /// <summary>
        /// Create Runtime assembly definition
        /// </summary>
        public static void CreateRuntimeAsmdef(string packagePath, string packageName)
        {
            var runtimePath = Path.Combine(Path.GetFullPath(packagePath), "Runtime");
            Directory.CreateDirectory(runtimePath);

            var asmdefName = UPMPackageValidator.PackageNameToNamespace(packageName);
            var asmdefPath = Path.Combine(runtimePath, $"{asmdefName}.asmdef");

            var content = GenerateAsmdef(asmdefName, new string[0], new string[0]);
            File.WriteAllText(asmdefPath, content, Encoding.UTF8);
        }

        /// <summary>
        /// Create Editor assembly definition
        /// </summary>
        public static void CreateEditorAsmdef(string packagePath, string packageName)
        {
            var editorPath = Path.Combine(Path.GetFullPath(packagePath), "Editor");
            Directory.CreateDirectory(editorPath);

            var baseNamespace = UPMPackageValidator.PackageNameToNamespace(packageName);
            var asmdefName = $"{baseNamespace}.Editor";
            var asmdefPath = Path.Combine(editorPath, $"{asmdefName}.asmdef");

            var content = GenerateAsmdef(asmdefName, new[] { baseNamespace }, new[] { "Editor" });
            File.WriteAllText(asmdefPath, content, Encoding.UTF8);
        }

        /// <summary>
        /// Create Tests assembly definition
        /// </summary>
        public static void CreateTestsAsmdef(string packagePath, string packageName, bool isEditMode)
        {
            var testsPath = Path.Combine(Path.GetFullPath(packagePath), "Tests");
            var subPath = isEditMode ? "Editor" : "Runtime";
            var fullPath = Path.Combine(testsPath, subPath);
            Directory.CreateDirectory(fullPath);

            var baseNamespace = UPMPackageValidator.PackageNameToNamespace(packageName);
            var suffix = isEditMode ? ".Editor.Tests" : ".Tests";
            var asmdefName = $"{baseNamespace}{suffix}";
            var asmdefPath = Path.Combine(fullPath, $"{asmdefName}.asmdef");

            var references = new[] { baseNamespace, "UnityEngine.TestRunner", "UnityEditor.TestRunner" };
            var platforms = isEditMode ? new[] { "Editor" } : new string[0];

            var content = GenerateTestAsmdef(asmdefName, references, platforms);
            File.WriteAllText(asmdefPath, content, Encoding.UTF8);
        }

        private static string GenerateAsmdef(string name, string[] references, string[] includePlatforms)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"name\": \"{name}\",");

            // References
            sb.Append("    \"references\": [");
            if (references.Length > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < references.Length; i++)
                {
                    var comma = i < references.Length - 1 ? "," : "";
                    sb.AppendLine($"        \"{references[i]}\"{comma}");
                }
                sb.Append("    ");
            }
            sb.AppendLine("],");

            // Include platforms
            sb.Append("    \"includePlatforms\": [");
            if (includePlatforms.Length > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < includePlatforms.Length; i++)
                {
                    var comma = i < includePlatforms.Length - 1 ? "," : "";
                    sb.AppendLine($"        \"{includePlatforms[i]}\"{comma}");
                }
                sb.Append("    ");
            }
            sb.AppendLine("],");

            sb.AppendLine("    \"excludePlatforms\": [],");
            sb.AppendLine("    \"allowUnsafeCode\": false,");
            sb.AppendLine("    \"overrideReferences\": false,");
            sb.AppendLine("    \"precompiledReferences\": [],");
            sb.AppendLine("    \"autoReferenced\": true,");
            sb.AppendLine("    \"defineConstraints\": [],");
            sb.AppendLine("    \"versionDefines\": [],");
            sb.AppendLine("    \"noEngineReferences\": false");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateTestAsmdef(string name, string[] references, string[] includePlatforms)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"    \"name\": \"{name}\",");

            // References
            sb.Append("    \"references\": [");
            if (references.Length > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < references.Length; i++)
                {
                    var comma = i < references.Length - 1 ? "," : "";
                    sb.AppendLine($"        \"{references[i]}\"{comma}");
                }
                sb.Append("    ");
            }
            sb.AppendLine("],");

            // Include platforms
            sb.Append("    \"includePlatforms\": [");
            if (includePlatforms.Length > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < includePlatforms.Length; i++)
                {
                    var comma = i < includePlatforms.Length - 1 ? "," : "";
                    sb.AppendLine($"        \"{includePlatforms[i]}\"{comma}");
                }
                sb.Append("    ");
            }
            sb.AppendLine("],");

            sb.AppendLine("    \"excludePlatforms\": [],");
            sb.AppendLine("    \"allowUnsafeCode\": false,");
            sb.AppendLine("    \"overrideReferences\": true,");

            // Precompiled references for NUnit
            sb.AppendLine("    \"precompiledReferences\": [");
            sb.AppendLine("        \"nunit.framework.dll\"");
            sb.AppendLine("    ],");

            sb.AppendLine("    \"autoReferenced\": false,");

            // Define constraints for tests
            sb.AppendLine("    \"defineConstraints\": [");
            sb.AppendLine("        \"UNITY_INCLUDE_TESTS\"");
            sb.AppendLine("    ],");

            sb.AppendLine("    \"versionDefines\": [],");
            sb.AppendLine("    \"noEngineReferences\": false");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Create README.md file
        /// </summary>
        public static void CreateReadme(string packagePath, UPMPackageData data)
        {
            var readmePath = Path.Combine(Path.GetFullPath(packagePath), "README.md");
            var sb = new StringBuilder();

            sb.AppendLine($"# {data.displayName}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(data.description))
            {
                sb.AppendLine(data.description);
                sb.AppendLine();
            }
            sb.AppendLine("## Installation");
            sb.AppendLine();
            sb.AppendLine("Add this package to your Unity project via the Package Manager.");
            sb.AppendLine();
            sb.AppendLine("## Usage");
            sb.AppendLine();
            sb.AppendLine("TODO: Add usage instructions here.");

            File.WriteAllText(readmePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Create CHANGELOG.md file
        /// </summary>
        public static void CreateChangelog(string packagePath, UPMPackageData data)
        {
            var changelogPath = Path.Combine(Path.GetFullPath(packagePath), "CHANGELOG.md");
            var sb = new StringBuilder();

            sb.AppendLine("# Changelog");
            sb.AppendLine();
            sb.AppendLine("All notable changes to this project will be documented in this file.");
            sb.AppendLine();
            sb.AppendLine($"## [{data.version}] - {System.DateTime.Now:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine("### Added");
            sb.AppendLine("- Initial release");

            File.WriteAllText(changelogPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Create LICENSE.md file (MIT License)
        /// </summary>
        public static void CreateLicense(string packagePath, UPMPackageData data)
        {
            var licensePath = Path.Combine(Path.GetFullPath(packagePath), "LICENSE.md");
            var year = System.DateTime.Now.Year;
            var author = !string.IsNullOrEmpty(data.author?.name) ? data.author.name : "Author";

            var sb = new StringBuilder();
            sb.AppendLine("MIT License");
            sb.AppendLine();
            sb.AppendLine($"Copyright (c) {year} {author}");
            sb.AppendLine();
            sb.AppendLine("Permission is hereby granted, free of charge, to any person obtaining a copy");
            sb.AppendLine("of this software and associated documentation files (the \"Software\"), to deal");
            sb.AppendLine("in the Software without restriction, including without limitation the rights");
            sb.AppendLine("to use, copy, modify, merge, publish, distribute, sublicense, and/or sell");
            sb.AppendLine("copies of the Software, and to permit persons to whom the Software is");
            sb.AppendLine("furnished to do so, subject to the following conditions:");
            sb.AppendLine();
            sb.AppendLine("The above copyright notice and this permission notice shall be included in all");
            sb.AppendLine("copies or substantial portions of the Software.");
            sb.AppendLine();
            sb.AppendLine("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR");
            sb.AppendLine("IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,");
            sb.AppendLine("FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE");
            sb.AppendLine("AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER");
            sb.AppendLine("LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,");
            sb.AppendLine("OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE");
            sb.AppendLine("SOFTWARE.");

            File.WriteAllText(licensePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Create Documentation~ directory
        /// </summary>
        public static void CreateDocumentationFolder(string packagePath)
        {
            var docPath = Path.Combine(Path.GetFullPath(packagePath), "Documentation~");
            Directory.CreateDirectory(docPath);

            // Create a placeholder file
            var indexPath = Path.Combine(docPath, "index.md");
            var sb = new StringBuilder();
            sb.AppendLine("# Documentation");
            sb.AppendLine();
            sb.AppendLine("Add your documentation here.");

            File.WriteAllText(indexPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Update all asmdef files when package name changes
        /// </summary>
        public static void UpdateAsmdefs(string packagePath, string oldPackageName, string newPackageName)
        {
            var fullPath = Path.GetFullPath(packagePath);
            var asmdefFiles = Directory.GetFiles(fullPath, "*.asmdef", SearchOption.AllDirectories);

            var oldNamespace = UPMPackageValidator.PackageNameToNamespace(oldPackageName);
            var newNamespace = UPMPackageValidator.PackageNameToNamespace(newPackageName);

            foreach (var asmdefPath in asmdefFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(asmdefPath);
                var dir = Path.GetDirectoryName(asmdefPath);

                // 检查是否是这个包的 asmdef
                if (!fileName.StartsWith(oldNamespace))
                    continue;

                // 确定新文件名
                var newFileName = fileName.Replace(oldNamespace, newNamespace);

                // 读取并更新内容
                var content = File.ReadAllText(asmdefPath);
                content = content.Replace($"\"{oldNamespace}", $"\"{newNamespace}");

                // 写入新内容
                var newPath = Path.Combine(dir, $"{newFileName}.asmdef");
                File.WriteAllText(newPath, content, Encoding.UTF8);

                // 重命名 meta 文件（保留 GUID）
                if (asmdefPath != newPath)
                {
                    var oldMetaPath = asmdefPath + ".meta";
                    var newMetaPath = newPath + ".meta";
                    if (File.Exists(oldMetaPath))
                    {
                        if (File.Exists(newMetaPath))
                            File.Delete(newMetaPath);
                        File.Move(oldMetaPath, newMetaPath);
                    }

                    // 删除旧 asmdef 文件
                    if (File.Exists(asmdefPath))
                        File.Delete(asmdefPath);
                }
            }
        }
    }
}
#endif
