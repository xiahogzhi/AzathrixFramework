# MiniPanda VSCode Extension

VSCode 的 MiniPanda 语言支持插件。

## 功能

- 语法高亮
- 括号匹配和自动补全
- 代码折叠
- 注释快捷键 (Ctrl+/)

## 安装

### 方法 1：从 VSIX 安装

1. 打包插件：
   ```bash
   cd minipanda-vscode
   npm install -g @vscode/vsce
   vsce package
   ```

2. 在 VSCode 中：
   - Extensions → ... → Install from VSIX...
   - 选择生成的 `.vsix` 文件

### 方法 2：开发模式

1. 复制 `minipanda-vscode` 文件夹到 VSCode 扩展目录：
   - Windows: `%USERPROFILE%\.vscode\extensions\`
   - macOS: `~/.vscode/extensions/`
   - Linux: `~/.vscode/extensions/`

2. 重启 VSCode

### 方法 3：调试运行

1. 用 VSCode 打开 `minipanda-vscode` 文件夹
2. 按 F5 启动扩展开发主机

## 支持的语法

- 关键字: `var`, `func`, `class`, `if`, `else`, `while`, `for`, `in`, `return`, `break`, `continue`, `import`, `as`
- 常量: `true`, `false`, `null`
- 特殊变量: `this`, `super`
- 注释: `//` 和 `/* */`
- 字符串: `"..."` 支持转义和插值 `{}`
