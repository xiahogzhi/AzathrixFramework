# UPM Editor

Unity Package Manager 编辑器工具，用于创建、编辑和发布 UPM 包。

## 功能

- **创建 UPM 包**: 通过向导创建新的 UPM 包，支持自定义模板
- **Inspector 编辑**: 选中 UPM 包目录后在 Inspector 中直接编辑 package.json
- **发布到 npm**: 支持发布到 Verdaccio 或其他 npm 仓库
- **右键菜单**: 在 Project 窗口中右键快速操作
- **自动同步 asmdef**: 修改包名时自动更新所有 asmdef 文件

## 使用方法

### 创建新包

菜单: `Para Cross Games > UPM Editor > 创建 UPM`

创建包时可选择生成：
- Runtime/ 目录（含 .asmdef）
- Editor/ 目录（含 .asmdef）
- Tests/ 目录（含测试 .asmdef）
- Documentation~/ 目录
- README.md / CHANGELOG.md / LICENSE.md

### 编辑现有包

在 Project 窗口中选中 UPM 包目录，Inspector 会显示编辑界面：
- 修改包名、版本、描述等基本信息
- 管理依赖项和关键词
- 添加/删除目录和文件
- 点击"保存"按钮保存更改

修改包名后保存会自动更新所有 asmdef 文件的名称和命名空间。

### 发布包

1. 在 Inspector 中点击"发布"按钮
2. 配置 npm 仓库地址和认证 Token
3. 选择版本号递增方式（Patch/Minor/Major）
4. 点击"发布"

### 右键菜单

在 Project 窗口中右键文件夹：
- **Move to Packages**: 将 Assets 下的包移动到 Packages
- **Move to Assets**: 将 Packages 下的包移动到 Assets
- **Create Package Here**: 在当前目录创建新包

## 命名规范

包名和程序集名的转换规则：
- `com.company.name1.name2` → `Company.Name1.Name2`
- `com.company.name1-name2` → `Company.Name1Name2`

## 要求

- Unity 6000.3 或更高版本
- npm（用于发布功能）
