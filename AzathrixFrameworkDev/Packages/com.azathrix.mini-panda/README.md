# MiniPanda

轻量级脚本语言解释器，专为 Unity 设计。支持变量、函数、类、数组、对象以及 C# 双向互操作。

## 特性

- 字节码编译执行（非 AST 解释）
- 编译缓存（相同代码只编译一次）
- 变量、函数、Lambda、类、继承
- 数组、对象/字典
- 控制流（if/else, while, for, break, continue）
- 全局变量（`global` 关键字、`_G` 全局表）
- 模块系统（import）
- C# 互操作（注册/获取变量、调用函数）
- 垃圾回收（标记-清除）
- 自定义文件加载

## 安装

通过 Unity Package Manager 添加此包。

## 快速开始

```csharp
using Azathrix.MiniPanda;

// 创建虚拟机
var vm = new MiniPanda();
vm.Start();

// 执行代码
vm.Run("var x = 10");
vm.Run("print(x)");

// 求值表达式
var result = vm.Eval("x + 5");  // 15

// 关闭
vm.Shutdown();
```

## 语法

### 变量

```javascript
var x = 10
var name = "hello"
var flag = true
var empty = null

// 复合赋值
x += 5   // x = x + 5
x -= 3   // x = x - 3
x *= 2   // x = x * 2
x /= 4   // x = x / 4
x %= 3   // x = x % 3

// 自增自减
++x      // 前置自增
x++      // 后置自增
--x      // 前置自减
x--      // 后置自减
```

### 字符串

```javascript
var name = "World"
var greeting = "Hello {name}!"  // 字符串插值
print(greeting)  // Hello World!

// 表达式插值
print("Result: {10 + 5}")  // Result: 15

// 转义大括号
print("\{literal}")  // {literal}
```

### 运算符

```javascript
// 三元运算符
var result = x > 0 ? "positive" : "negative"

// 空值合并
var value = null ?? "default"  // "default"
var name = userName ?? "Guest"  // 如果 userName 为 null，使用 "Guest"

// 链式空值合并
var x = a ?? b ?? c ?? "fallback"
```

### 数组与对象

```javascript
var arr = [1, 2, 3]
arr[0] = 100
print(arr[0])

var obj = {name: "test", value: 42}
obj.name = "new"
obj["key"] = 123
```

### 控制流

```javascript
// 单行（无大括号）
if x > 0 print("positive")

// 多行
if x > 0 {
    print("positive")
} else {
    print("negative")
}

// 循环
while x > 0 {
    x = x - 1
}

for item in arr {
    print(item)
}

for i in range(10) {
    print(i)
}
```

### 函数

```javascript
// 标准函数
func add(a, b) {
    return a + b
}

// 单行函数
func double(x) return x * 2

// 默认参数
func greet(name, greeting = "Hello") {
    return greeting + ", " + name
}
greet("World")           // "Hello, World"
greet("World", "Hi")     // "Hi, World"

// Lambda
var triple = (x) => x * 3
var add = (a, b = 10) => a + b  // Lambda 也支持默认参数
```

### 类

```javascript
class Player {
    Player(name) {
        this.name = name
        this.hp = 100
    }

    func takeDamage(amount) {
        this.hp = this.hp - amount
    }
}

var player = Player("Hero")
player.takeDamage(30)
print(player.hp)  // 70

// 继承
class Boss : Player {
    Boss(name) {
        super.Player(name)
        this.hp = 500
    }
}
```

### 全局变量

```javascript
// 使用 global 关键字声明全局变量
global var config = {debug: true}

// 在任何作用域都可以访问
func test() {
    print(config.debug)  // true
}

// 使用 _G 表直接访问全局作用域
_G.newGlobal = 100
print(_G.newGlobal)  // 100
print(_G.abs(-5))    // 5 (访问内置函数)
```

## C# 互操作

### 注册全局变量

```csharp
vm.SetGlobal("PI", 3.14159);
vm.SetGlobal("playerName", "Hero");
vm.SetGlobal("isDebug", true);
```

### 注册原生函数

```csharp
vm.SetGlobal("square", NativeFunc.Create((Value v) =>
    Value.FromNumber(v.AsNumber() * v.AsNumber())));

vm.SetGlobal("log", NativeFunc.Create((Value[] args) => {
    Debug.Log(args[0].AsString());
    return Value.Null;
}));
```

### 获取变量

```csharp
var x = vm.GetGlobal("x").AsNumber();
var name = vm.GetGlobal("name").AsString();
```

### 调用脚本函数

```csharp
vm.Run("func multiply(a, b) return a * b");
var result = vm.Call("multiply", 6, 7);  // 42

// 带临时作用域调用
vm.Run("func greet(name) { return prefix + name + suffix }");
var result = vm.Call(new { prefix = "Hello, ", suffix = "!" }, "greet", "World");
// result = "Hello, World!"
```

### 带临时环境求值

```csharp
// 匿名对象
var result = vm.Eval("x + y", new { x = 10, y = 20 });  // 30

// Dictionary
var env = new Dictionary<string, object> { ["x"] = 10, ["y"] = 20 };
var result = vm.Eval("x + y", env);
```

### 自定义文件加载

```csharp
// 从 Unity Resources 加载
vm.FileLoader = path => Resources.Load<TextAsset>(path)?.text;

// 从 StreamingAssets 加载
vm.FileLoader = path => {
    var fullPath = Path.Combine(Application.streamingAssetsPath, path);
    return File.ReadAllText(fullPath);
};

vm.RunFile("scripts/main.panda");
```

## 内置函数

| 函数 | 说明 |
|------|------|
| `print(value)` | 打印值 |
| `type(value)` | 返回类型名 |
| `len(arr/str)` | 返回长度 |
| `range(n)` | 生成 0 到 n-1 的数组 |
| `abs(n)` | 绝对值 |
| `floor(n)` | 向下取整 |
| `ceil(n)` | 向上取整 |
| `round(n)` | 四舍五入 |
| `sqrt(n)` | 平方根 |
| `pow(a, b)` | a 的 b 次方 |
| `min(...)` | 最小值 |
| `max(...)` | 最大值 |
| `push(arr, val)` | 向数组末尾添加元素 |
| `pop(arr)` | 移除并返回数组末尾元素 |
| `keys(obj)` | 返回对象所有键的数组 |
| `values(obj)` | 返回对象所有值的数组 |
| `contains(col, item)` | 检查数组/对象/字符串是否包含元素 |
| `slice(arr/str, start, end)` | 切片，支持负索引 |
| `join(arr, sep)` | 数组连接成字符串 |
| `split(str, sep)` | 字符串分割成数组 |
| `_G` | 全局表，可读写全局变量 |

### JSON

```javascript
// 解析 JSON 字符串
var obj = JSON.parse("{\"name\":\"test\",\"value\":42}")
print(obj.name)  // test

// 转换为 JSON 字符串
var json = JSON.stringify({name: "test", value: 42})
print(json)  // {"name":"test","value":42}
```

### 调试函数

```javascript
// trace - 打印值并显示位置信息
trace("debug info")  // [TRACE] debug info (at script.panda:10)

// debug - 同 trace
debug("message")

// stacktrace - 返回调用栈字符串
func inner() { return stacktrace() }
func outer() { return inner() }
print(outer())

// assert - 断言，条件为 false 时抛出错误
assert(x > 0)
assert(x > 0, "x must be positive")
```

## 编译缓存

```csharp
// 相同代码只编译一次
vm.Run("print(1)");  // 编译 + 执行
vm.Run("print(1)");  // 直接执行（从缓存）

// 禁用缓存
vm.CacheEnabled = false;

// 清除缓存
vm.ClearCache();
```

## License

MIT
