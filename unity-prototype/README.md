# Unity Windows rendering prototype

第二阶段：导入网页压力场景的单位、事件流、棋盘几何和图集，验证原生客户端的渲染成本。尚未移植完整游戏。

## 当前场景

- 默认场景使用与网页相同的 40 个干员、80 个敌人、59 种真实 Spine 3.8 模型。
- 导入 act2autochess_m01 联防区域的 178 块棋盘、3924 个顶点/2292 个三角形、原有 UV 及 D/E/common 图集，使用相同相机位置、40°视野。
- 复用网页种子 7 的攻击/伤害/治疗/技能事件节奏；2400 个数据步覆盖 120 秒实际时间。验证器与真实网页连续 100 步、3223 条事件逐条对比通过。
- 飞行弹体、链条、技能光圈、伤害/治疗数字和血条使用固定容量池及一个动态合并网格；记录峰值和丢弃数量。
- 30 秒预热，然后默认记录 3600 帧；命令行 `-benchmark-samples 600` 可用于短窗口检查。
- 输出平均 FPS、p50/p95/p99 帧耗时、CPU/GPU、实际分辨率及模型数。
- 素材加载失败则不会生成有效成绩。
- 棋盘灯光和玻璃材质、弹体形状、技能特效、动画风向与动作混合仍为简化实现；未移植设备风机/出入口特效、完整战斗模拟、游戏 UI 或联网。不能当作与网页等画质的最终性能对比。

## 打开和构建

Unity Hub 添加本目录，使用 Unity 6000.6.5f1。菜单 `Stronghold/Create benchmark scene` 创建场景，然后播放。严肃性能评估使用 Windows 构建，不使用编辑器 FPS。

在仓库根目录执行 `node tools/export-unity-stress.mjs` 更新默认压力场景。它读取本地素材，缺少时从已授权的部署服务器下载；棋盘 WebP 通过本机 Edge 解码为 Unity 可读取的 PNG，几何直接调用网页 buildBoard 导出，不重新手写棋盘布局。设置 `EDGE_PATH` 可覆盖 Edge 路径。

`node tools/prepare-unity.mjs` 生成第一阶段的简单动画模型清单；运行程序加 `-simple` 切换简单场景。两个模式各自读取独立清单。

本机已安装官方 spine-unity 3.8 (2021-11-10) 运行库，位于 `Assets/Spine`，此目录和游戏素材均不提交 Git。全新检出执行 `tools/install-unity-spine.ps1` 安装匹配的运行库。该脚本保留 Runtime 和许可文件，省略旧版 Editor 集成，并将 SkeletonMecanim 的 GetInstanceID 比较改为 Unity 对象相等比较/哈希，以适配 Unity 6.6。不能直接改用 Spine 4.x 读取 3.8 二进制素材。

批处理构建：

```powershell
& 'F:\新建文件夹\6000.6.5f1\Editor\Unity.exe' -batchmode -quit -projectPath "$PWD\unity-prototype" -executeMethod PrototypeBuild.BuildWindows -logFile "$PWD\unity-build.log"
```

产物 `Builds/Windows/StrongholdBenchmark.exe`。结果和测试后导出的画面写入 `%USERPROFILE%\AppData\LocalLow\Rennkoo\Stronghold Rendering Prototype`。JSON 包含 focusedSamples：后台成绩不代表可见窗口的呈现流畅度。构建默认不锁帧、不启用 VSync，以便观察吞吐能力；实际显示流畅度需另测启用 VSync 的版本。

## 真实录像回放

执行 `node tools/export-unity-stress.mjs --replay`，生成独立的录像模型清单和普通作战棋盘。随后重新构建 Windows 程序，加 `-replay` 启动（默认仍为压力测试）。录像为仓库已有的 `public/dev/recordings/normal-m01.json`，55 秒游戏时间以 2 倍速播放一次，结束后停在最后状态。

支持按快照控制单位显示/退场、位置插值、血量、移动/待机及冻结；攻击、部署、技能开始和死亡事件驱动动画，并显示简化弹体和数字。所有单位预先加载，尚未实现运行时模型下载或多人网络；录像回放不运行战斗逻辑。技力和完整状态位保留在数据中，尚未显示技力条/状态图标；status/fx 事件目前仅计数，技能结束及 begin/loop/end 动画角色仍需补齐。

`node tools/verify-unity-replay.mjs` 逐条比较导出快照、事件关键字段及模型映射；`tools/measure-unity-replay.ps1` 启动原生程序播放整场并验证事件数量、最终可见单位、实际血量和位置。中场/最终截图及结果在 `.cache/unity-replay`。运行脚本会结束其启动的测试进程。

`PrototypeBuild.ValidateAndBuild` 可作为批处理入口，先执行真实 Spine 模型的动画验证，再构建 Windows。

2026-10-10 地形索引和特效包围盒优化后，120 单位、1920×1080、前台、30 秒预热 + 3600 帧平均 77.12 FPS，p95 18.29 ms，特效丢弃 0。这与前面的后台测试条件不同；部分帧仍超过 16.67 ms，尚未达到稳定 60 FPS。记录见 [前台压力数据](../docs/performance/unity-grid-stress-long-20261010.json)。

## 后续验收

1. 确认全部模型正确加载、画面正确、effectsDropped 为 0。
2. 启动本地网页服务后执行 `node tools/verify-unity-stress.mjs`，逐条验证单位与事件；`STRESS_BASE_URL` 可配置服务地址，默认 localhost:3137。
3. 同一设备、相同分辨率及画质，至少重复三次，比较平均 FPS 与 p95/p99，不将单次高 FPS 当成收益结论。
4. 验证性能有效后再迁移客户端 UI/协议/战斗表现，优先保留现有 Node 服务端。

`tools/measure-unity-stress.ps1 -Runs 3 -Samples 3600` 可执行三轮串行后台测试。每轮单独启动程序、预热 30 秒，结果与画面保存在 `.cache/unity-measurements`，未加载全部单位或特效池溢出则失败。该脚本用于可重复的后台吞吐验证；可见窗口/VSync 测试另行进行。

2026-10-10 本机最终构建三轮后台结果：101.28 / 100.31 / 100.62 FPS，p95 11.94 / 12.24 / 12.17 ms，特效丢弃均为 0。硬件 i7-13650HX / RTX 4050 Laptop，1920×1080，每轮 3600 帧。原始记录见 [性能数据](../docs/performance/unity-stress-20261010.json)。这是简化渲染内容的原生吞吐测试，不是完整游戏或等画质网页对比。

运行库遵守其自带许可；原项目和素材各自的许可不因移植而改变。
