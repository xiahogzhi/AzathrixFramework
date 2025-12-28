#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Azathrix.UpmEditor.Editor.Core;
using Azathrix.UpmEditor.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace  Azathrix.UpmEditor.Editor.UI
{
    /// <summary>
    /// UPM 包创建窗口
    /// </summary>
    public class UPMEditorWindow : EditorWindow
    {
        private UPMPackageData _packageData;
        private PackageTemplateOptions _templateOptions;
        private string _targetDirectory;
        private Vector2 _scrollPosition;
        private ValidationResult _validationResult;

        // Foldout states
        private bool _foldBasicInfo = true;
        private bool _foldAuthor = true;
        private bool _foldDependencies = false;
        private bool _foldKeywords = false;
        private bool _foldTemplate = true;

        // Dependency editing
        private string _newDepName = "";
        private string _newDepVersion = "1.0.0";

        // Keywords editing
        private string _newKeyword = "";

        [MenuItem(UPMConstants.ToolsMenuRoot + "创建 UPM")]
        public static void ShowWindow()
        {
            var window = GetWindow<UPMEditorWindow>("创建 UPM");
            window.minSize = new Vector2(380, 450);
            window.Initialize();
        }

        public static void CreatePackageAt(string targetPath)
        {
            var window = GetWindow<UPMEditorWindow>("创建 UPM");
            window.minSize = new Vector2(380, 450);
            window.Initialize(targetPath);
        }

        private void OnEnable()
        {
            if (_packageData == null)
            {
                Initialize();
            }
        }

        private void Initialize(string targetPath = null)
        {
            _packageData = PackageJsonService.CreateDefaultPackageData("com.company.package", "My Package");
            _templateOptions = new PackageTemplateOptions();
            _targetDirectory = targetPath ?? "Packages";
            _validationResult = null;
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(5);

            DrawBasicInfoSection();
            DrawAuthorSection();
            DrawDependenciesSection();
            DrawKeywordsSection();
            DrawTemplateSection();
            DrawValidationSection();

            EditorGUILayout.Space(10);
            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        #region UI Sections

        private void DrawBasicInfoSection()
        {
            _foldBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBasicInfo, "基本信息");

            if (_foldBasicInfo)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                _packageData.name = EditorGUILayout.TextField(
                    new GUIContent("包名", "UPM 包名，格式: com.company.package-name"),
                    _packageData.name);

                _packageData.displayName = EditorGUILayout.TextField(
                    new GUIContent("显示名称", "在 Package Manager 中显示的名称"),
                    _packageData.displayName);

                _packageData.version = EditorGUILayout.TextField(
                    new GUIContent("版本", "语义化版本号，如 1.0.0"),
                    _packageData.version);

                _packageData.unity = EditorGUILayout.TextField(
                    new GUIContent("Unity 版本", "最低支持的 Unity 版本"),
                    _packageData.unity);

                EditorGUILayout.LabelField(new GUIContent("描述", "包的详细描述"));
                _packageData.description = EditorGUILayout.TextArea(_packageData.description, GUILayout.Height(50));

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAuthorSection()
        {
            _foldAuthor = EditorGUILayout.BeginFoldoutHeaderGroup(_foldAuthor, "作者信息");

            if (_foldAuthor)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                _packageData.author.name = EditorGUILayout.TextField(
                    new GUIContent("姓名", "作者姓名"),
                    _packageData.author.name);

                _packageData.author.email = EditorGUILayout.TextField(
                    new GUIContent("邮箱", "联系邮箱"),
                    _packageData.author.email);

                _packageData.author.url = EditorGUILayout.TextField(
                    new GUIContent("网址", "作者主页或项目地址"),
                    _packageData.author.url);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDependenciesSection()
        {
            _foldDependencies = EditorGUILayout.BeginFoldoutHeaderGroup(_foldDependencies,
                $"依赖项 ({_packageData.dependencies.Count})");

            if (_foldDependencies)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                var toRemove = new List<string>();
                foreach (var dep in _packageData.dependencies)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(dep.Key, GUILayout.MinWidth(120));
                    EditorGUILayout.LabelField(dep.Value, GUILayout.Width(60));
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                    {
                        toRemove.Add(dep.Key);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                foreach (var key in toRemove)
                {
                    _packageData.dependencies.Remove(key);
                }

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                _newDepName = EditorGUILayout.TextField(_newDepName, GUILayout.MinWidth(120));
                _newDepVersion = EditorGUILayout.TextField(_newDepVersion, GUILayout.Width(60));
                GUI.enabled = !string.IsNullOrEmpty(_newDepName) && !_packageData.dependencies.ContainsKey(_newDepName);
                if (GUILayout.Button("+", GUILayout.Width(22)))
                {
                    _packageData.dependencies[_newDepName] = _newDepVersion;
                    _newDepName = "";
                    _newDepVersion = "1.0.0";
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawKeywordsSection()
        {
            _foldKeywords = EditorGUILayout.BeginFoldoutHeaderGroup(_foldKeywords,
                $"关键词 ({_packageData.keywords.Count})");

            if (_foldKeywords)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                var toRemoveIdx = -1;
                for (int i = 0; i < _packageData.keywords.Count; i++)
                {
                    if (GUILayout.Button($"{_packageData.keywords[i]} ×", EditorStyles.miniButton))
                    {
                        toRemoveIdx = i;
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (toRemoveIdx >= 0)
                {
                    _packageData.keywords.RemoveAt(toRemoveIdx);
                }

                EditorGUILayout.BeginHorizontal();
                _newKeyword = EditorGUILayout.TextField(_newKeyword);
                GUI.enabled = !string.IsNullOrEmpty(_newKeyword) && !_packageData.keywords.Contains(_newKeyword);
                if (GUILayout.Button("添加", GUILayout.Width(50)))
                {
                    _packageData.keywords.Add(_newKeyword);
                    _newKeyword = "";
                }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawTemplateSection()
        {
            _foldTemplate = EditorGUILayout.BeginFoldoutHeaderGroup(_foldTemplate, "模板选项");

            if (_foldTemplate)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.LabelField("目录", EditorStyles.miniBoldLabel);
                _templateOptions.createRuntime = EditorGUILayout.ToggleLeft(
                    new GUIContent("Runtime/", "运行时代码目录，包含 .asmdef"),
                    _templateOptions.createRuntime);
                _templateOptions.createEditor = EditorGUILayout.ToggleLeft(
                    new GUIContent("Editor/", "编辑器代码目录，包含 .asmdef"),
                    _templateOptions.createEditor);
                _templateOptions.createTests = EditorGUILayout.ToggleLeft(
                    new GUIContent("Tests/", "测试代码目录"),
                    _templateOptions.createTests);
                _templateOptions.createDocumentation = EditorGUILayout.ToggleLeft(
                    new GUIContent("Documentation~/", "文档目录（不会被 Unity 导入）"),
                    _templateOptions.createDocumentation);

                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("文件", EditorStyles.miniBoldLabel);
                _templateOptions.createReadme = EditorGUILayout.ToggleLeft(
                    new GUIContent("README.md", "说明文档"),
                    _templateOptions.createReadme);
                _templateOptions.createChangelog = EditorGUILayout.ToggleLeft(
                    new GUIContent("CHANGELOG.md", "更新日志"),
                    _templateOptions.createChangelog);
                _templateOptions.createLicense = EditorGUILayout.ToggleLeft(
                    new GUIContent("LICENSE.md", "MIT 许可证"),
                    _templateOptions.createLicense);

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("目标路径", "包将创建在此目录下"), GUILayout.Width(60));
                EditorGUILayout.LabelField(Path.Combine(_targetDirectory, _packageData.name), EditorStyles.textField);
                if (GUILayout.Button("...", GUILayout.Width(25)))
                {
                    var path = EditorUtility.OpenFolderPanel("选择目标目录", _targetDirectory, "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _targetDirectory = ConvertToRelativePath(path);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawValidationSection()
        {
            if (_validationResult == null) return;

            EditorGUILayout.Space(5);

            foreach (var error in _validationResult.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (var warning in _validationResult.Warnings)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            if (_validationResult.IsValid && _validationResult.Warnings.Count == 0)
            {
                EditorGUILayout.HelpBox("包数据验证通过", MessageType.Info);
            }
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("验证", "验证包数据是否有效"), GUILayout.Height(28)))
            {
                _validationResult = UPMPackageValidator.ValidatePackageData(_packageData);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("创建包", "创建新的 UPM 包"), GUILayout.Height(28), GUILayout.Width(70)))
            {
                CreatePackage();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Actions

        private void CreatePackage()
        {
            _validationResult = UPMPackageValidator.ValidatePackageData(_packageData);
            if (!_validationResult.IsValid)
            {
                EditorUtility.DisplayDialog("验证错误", "请先修复错误再创建包", "确定");
                return;
            }

            var packagePath = Path.Combine(_targetDirectory, _packageData.name);
            var fullPath = Path.GetFullPath(packagePath);

            if (Directory.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("目录已存在",
                    $"目录已存在:\n{packagePath}\n\n是否覆盖?", "覆盖", "取消"))
                {
                    return;
                }
            }

            try
            {
                Directory.CreateDirectory(fullPath);
                PackageJsonService.WritePackageJson(packagePath, _packageData);

                if (_templateOptions.createRuntime)
                    AsmdefGeneratorService.CreateRuntimeAsmdef(packagePath, _packageData.name);

                if (_templateOptions.createEditor)
                    AsmdefGeneratorService.CreateEditorAsmdef(packagePath, _packageData.name);

                if (_templateOptions.createTests)
                {
                    AsmdefGeneratorService.CreateTestsAsmdef(packagePath, _packageData.name, false);
                    AsmdefGeneratorService.CreateTestsAsmdef(packagePath, _packageData.name, true);
                }

                if (_templateOptions.createReadme)
                    AsmdefGeneratorService.CreateReadme(packagePath, _packageData);

                if (_templateOptions.createChangelog)
                    AsmdefGeneratorService.CreateChangelog(packagePath, _packageData);

                if (_templateOptions.createLicense)
                    AsmdefGeneratorService.CreateLicense(packagePath, _packageData);

                if (_templateOptions.createDocumentation)
                    AsmdefGeneratorService.CreateDocumentationFolder(packagePath);

                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("成功", $"包已创建:\n{packagePath}", "确定");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("错误", $"创建失败:\n{e.Message}", "确定");
            }
        }

        private string ConvertToRelativePath(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace("\\", "/");
            var projectPath = Path.GetDirectoryName(dataPath).Replace("\\", "/");
            absolutePath = absolutePath.Replace("\\", "/");
            if (absolutePath.StartsWith(projectPath))
            {
                return absolutePath.Substring(projectPath.Length + 1);
            }
            return absolutePath;
        }

        #endregion
    }
}
#endif
