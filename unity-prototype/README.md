# Unity Windows rendering prototype

第一阶段：验证真实 Spine 3.8 动画在 Unity 原生客户端中的成本。不是完整游戏移植，也不是现有网页压力场景的等价复现。

## 当前场景

- 从本地网页项目素材中加载 60 个不同角色模型，生成 120 个独立骨骼实例。
- 合成棋盘、真实动画、30 秒预热，然后记录 3600 帧。
- 输出平均 FPS、p50/p95/p99 帧耗时、CPU/GPU、实际分辨率及模型数。
- 素材加载失败则不会生成有效成绩。
- 尚未实现原版 3D 棋盘、技能特效、伤害数字、战斗模拟、UI 和联网，因此不能直接用本原型帧数证明完整移植后的收益。

## 打开和构建

Unity Hub 添加本目录，使用 Unity 6000.6.5f1。菜单 `Stronghold/Create benchmark scene` 创建场景，然后播放。严肃性能评估使用 Windows 构建，不使用编辑器 FPS。

在仓库根目录执行 `node tools/prepare-unity.mjs` 刷新真实模型。本机已安装官方 spine-unity 3.8 (2021-11-10) 运行库，位于 `Assets/Spine`，此目录和游戏素材均不提交 Git。全新检出执行 `tools/install-unity-spine.ps1` 安装匹配的运行库。该脚本保留 Runtime 和许可文件，省略旧版 Editor 集成，并将 SkeletonMecanim 的 GetInstanceID 比较改为 Unity 对象相等比较/哈希，以适配 Unity 6.6。不能直接改用 Spine 4.x 读取 3.8 二进制素材。

批处理构建：

```powershell
& 'F:\新建文件夹\6000.6.5f1\Editor\Unity.exe' -batchmode -quit -projectPath "$PWD\unity-prototype" -executeMethod PrototypeBuild.BuildWindows -logFile "$PWD\unity-build.log"
```

产物 `Builds/Windows/StrongholdBenchmark.exe`。结果写入 `%USERPROFILE%\AppData\LocalLow\Rennkoo\Stronghold Rendering Prototype`。构建默认不锁帧、不启用 VSync，以便观察吞吐能力；实际显示流畅度需另测启用 VSync 的版本。

## 后续验收

1. 先确认全部模型正确加载且视觉正确。
2. 接入网页压力场景的同一模型名单、动画事件和相机，迁移真实棋盘/特效后再做公平对比。
3. 同一设备、相同分辨率及画质，至少重复三次，比较平均 FPS 与 p95/p99，不将单次高 FPS 当成收益结论。
4. 验证性能有效后再迁移客户端 UI/协议/战斗表现，优先保留现有 Node 服务端。

运行库遵守其自带许可；原项目和素材各自的许可不因移植而改变。
