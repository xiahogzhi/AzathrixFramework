# MiniPanda Rider Plugin

JetBrains Rider 的 MiniPanda 语言支持插件。

## 功能

- 语法高亮
- 括号匹配
- 代码折叠
- 注释支持 (Ctrl+/)
- 颜色设置页面

## 构建

需要 JDK 17+ 和 Gradle。

```bash
cd minipanda-rider-plugin
./gradlew buildPlugin
```

构建产物在 `build/distributions/` 目录。

## 安装

1. 打开 Rider
2. Settings → Plugins → ⚙️ → Install Plugin from Disk...
3. 选择 `minipanda-rider-plugin-1.0.0.zip`
4. 重启 Rider

## 开发

```bash
# 运行带插件的 IDE 实例
./gradlew runIde
```
