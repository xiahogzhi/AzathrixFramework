using System;
using System.IO;
using UnityEngine;
using Azathrix.MiniPanda;
using Azathrix.MiniPanda.Core;
using Azathrix.MiniPanda.VM;
using UnityEditor;

/// <summary>
/// </summary>
public class MiniPandaDemo : MonoBehaviour
{
    private MiniPanda _panda;

    void Start()
    {
        _panda = new MiniPanda();
        _panda.Start();


        LoadModules();

        BasicExample();
        FunctionExample();
        ClassExample();
        InteropExample();
        ImportExample();
    }

    void OnDestroy()
    {
        _panda?.Shutdown();
    }

    void RegisterUnityFunctions()
    {
        // _panda.SetGlobal("print", NativeFunction.CreateWithVM((vm, args) =>
        // {
        //     var msg = args.Length > 0 ? args[0].AsString() : "";
        //     var location = vm.GetCurrentLocation();
        //     Debug.Log($"{msg}\n(at {location})");
        //     return Value.Null;
        // }));
        //
        // _panda.SetGlobal("time", NativeFunction.Create(() => Value.FromNumber(Time.time)));
        //
        // _panda.SetGlobal("deltaTime", NativeFunction.Create(() => Value.FromNumber(Time.deltaTime)));
    }

    void BasicExample()
    {
        Debug.Log("=== 基础示例 ===");

        _panda.Run(@"
            var x = 10
            var y = 20
            print(""x + y = "" + (x + y))
        ");

        _panda.Run(@"
            var arr = [1, 2, 3, 4, 5]
            var sum = 0
            for n in arr {
                sum = sum + n
            }
            print(""数组求和: "" + sum)
        ");

        _panda.Run(@"
            var player = {name: ""Hero"", hp: 100, mp: 50}
            print(""玩家: "" + player.name + "", HP: "" + player.hp)
        ");
    }

    void FunctionExample()
    {
        Debug.Log("=== 函数示例 ===");

        _panda.Run(@"
            func factorial(n) {
                if n <= 1 return 1
                return n * factorial(n - 1)
            }
            print(""5! = "" + factorial(5))
        ");

        // Lambda
        _panda.Run(@"
            var numbers = [1, 2, 3, 4, 5]
            var double = (x) => x * 2

            for n in numbers {
                print(n + "" * 2 = "" + double(n))
            }
        ");
    }

    void ClassExample()
    {
        Debug.Log("=== 类示例 ===");

        _panda.Run(@"
            class Vector2 {
                Vector2(x, y) {
                    this.x = x
                    this.y = y
                }

                func add(other) {
                    return Vector2(this.x + other.x, this.y + other.y)
                }

                func magnitude() {
                    return sqrt(this.x * this.x + this.y * this.y)
                }

                func toString() {
                    return ""("" + this.x + "", "" + this.y + "")""
                }
            }

            var v1 = Vector2(3, 4)
            var v2 = Vector2(1, 2)
            var v3 = v1.add(v2)

            print(""v1 = "" + v1.toString())
            print(""v2 = "" + v2.toString())
            print(""v1 + v2 = "" + v3.toString())
            print(""v1 长度 = "" + v1.magnitude())
        ");
    }

    void InteropExample()
    {
        Debug.Log("=== C# 互操作示例 ===");

        _panda.SetGlobal("gameVersion", "1.0.0");
        _panda.SetGlobal("maxPlayers", 4);

        _panda.Run(@"
            print(""游戏版本: "" + gameVersion)
            print(""最大玩家数: "" + maxPlayers)
        ");

       var func =  _panda.Run<Func<float,float,float>>(@"
             func calculateDamage(baseDamage, multiplier) {
                return baseDamage * multiplier
            }
            return calculateDamage
        ");

        var damage = func(100, 1.5f);
        Debug.Log($"计算伤害 (C# 调用): {damage}");

        var result = _panda.Eval("hp - damage", new {hp = 100, damage = 30});
        Debug.Log($"剩余 HP: {result.AsNumber()}");
        damage = func(100, 1.5f);
        Debug.Log($"计算伤害 (C# 调用2): {damage}");
    }

    void LoadModules()
    {
        var samplesPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(this)));

        var utilsPath = Path.Combine(samplesPath, "utils.panda");
        if (File.Exists(utilsPath))
        {
            _panda.LoadModule(File.ReadAllBytes(utilsPath), "utils", utilsPath);
        }

        var vectorPath = Path.Combine(samplesPath, "math", "vector.panda");
        if (File.Exists(vectorPath))
        {
            _panda.LoadModule(File.ReadAllBytes(vectorPath), "math.vector", vectorPath);
        }

        var examplePath = Path.Combine(samplesPath, "example.panda");
        if (File.Exists(examplePath))
        {
            _panda.LoadModule(File.ReadAllBytes(examplePath), "example", "./" + examplePath.Replace("\\", "/"));
        }
    }

    void ImportExample()
    {
        Debug.Log("=== 模块导入示例 ===");

        _panda.Run(@"
             import ""example""
            import ""utils"" as u
            print(""Utils VERSION: "" + u.VERSION)
            u.helper()
            print(""clamp(15, 0, 10) = "" + u.clamp(15, 0, 10))

            import ""math.vector"" as vec
            var v1 = vec.create(3, 4, 0)
            var v2 = vec.create(1, 2, 0)
            var v3 = vec.add(v1, v2)
            print(""v1 + v2 = ("" + v3.x + "", "" + v3.y + "", "" + v3.z + "")"")
            print(""length(v1) = "" + vec.length(v1))
        ");
    }
}
