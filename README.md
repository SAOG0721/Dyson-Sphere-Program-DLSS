# Dyson Sphere Program DLSS / 戴森球计划 DLSS

[English](#english) | [中文](#中文)

## English

Experimental NVIDIA DLSS Super Resolution and DLAA integration for **Dyson Sphere Program** on Direct3D 11.

### Features

- DLSS Ultra Performance, Performance, Balanced, Quality, and DLAA modes.
- `F8` enables or disables DLSS; `F9` opens the mouse-operated control panel.
- Uses the game's depth and motion-vector textures and zero-centered Halton jitter.
- Optional optimized isotropic 3×3 Gaussian USM sharpening on the raw DLSS output, adjustable from 0 to 1; zero is an exact bypass.
- Falls back to the game's anti-aliasing when required camera inputs are unavailable.
- Starts disabled by default.

### Compatibility

- Dyson Sphere Program `0.10.34.28529` / Unity `2022.3.62f3c1`, Mono, Built-in Render Pipeline
- Windows x64 and Direct3D 11; leave Steam launch options empty
- BepInEx 5 x64 (`5.4.17.0` validated)
- NVIDIA RTX GPU and a current NVIDIA driver

Other game builds and hardware configurations have not been validated.

### Download and installation

Download `DSP-DLSS-0.6.2-BepInEx5.zip` from the [v0.6.2 release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.6.2).

```text
4F02FAEBF513E29BE20E64B308B35C17D5028A2BCDB14837282B1E703CFA7B70  DSP-DLSS-0.6.2-BepInEx5.zip
```

1. Install BepInEx 5 x64 into the game directory and launch the game once.
2. Exit the game and preserve any existing `NVUnityPlugin.dll` and `nvngx_dlss.dll` so they can be restored.
3. Remove obsolete `BepInEx\plugins\DSPDLSSDetail.dll` and `DSPDLSSRcas.dll` from earlier experimental builds if present.
4. Extract the release ZIP beside `DSPGAME.exe`.
5. Leave Steam launch options empty, launch through Steam, load a save, and use `F8`/`F9`.

See [installation and removal](docs/INSTALLATION.md) for file placement and checks.

### Controls and configuration

- `F8`: enable or disable DLSS.
- `F9`: open or close the control panel.
- The panel selects mode, Gaussian USM strength (0–1 in 0.05 steps), and technical information.
- Settings are stored in `BepInEx\config\local.dsp.dlss.cfg`.

### Current limitations

- Lower-resolution DLSS inputs are prepared after the scene has rendered at full resolution. Reconstruction resolution and quality change, but preceding 3D rendering cost does not fall proportionally.
- Procedural and indirect GPU instances can lack complete object motion vectors. Conveyors, cargo, logistics units, enemies, rockets, Dyson structures, particles, and transparent objects may show temporal artifacts.
- Integration occurs after tonemapping and is limited to the main gameplay camera.
- The release is build- and package-verified; final visual quality and long-session stability still require in-game validation on each system.

### Build

Managed plugin requirements: .NET SDK targeting `netstandard2.1`, BepInEx 5, and a legally installed copy of the game.

```powershell
$env:DSP_GAME_DIR = 'C:\path\to\Dyson Sphere Program'
dotnet build .\src\DSPDLSSZeroMV\DSPDLSSZeroMV.csproj -c Release
```

Native sharpening bridge requirements: Visual Studio 2022 C++ tools, CMake, Windows SDK `fxc.exe`, and Unity NativeRenderingPlugin headers.

```powershell
cmake -S .\native\DSPDLSSSharpen -B .\native\DSPDLSSSharpen\build -G Ninja `
  -DUNITY_NATIVE_PLUGIN_ROOT='C:\path\to\Unity-NativeRenderingPlugin\PluginSource\source'
cmake --build .\native\DSPDLSSSharpen\build --config Release
```

The repository contains only authored mod source and documentation. It does not track build outputs, game assemblies, generated game-code output, or redistributable runtime DLLs.

### Third-party components and status

The binary release contains NVIDIA's DLSS runtime and the matching Unity NVIDIA native runtime required by this Unity player. See [third-party notices](THIRD_PARTY_NOTICES.md) and the license files inside the archive.

This is an independent community mod and is not affiliated with or endorsed by Youthcat Studio, Gamera Games, Unity, or NVIDIA. No open-source license has been selected for the mod source at this time.

---

## 中文

为《戴森球计划》Direct3D 11 提供实验性的 NVIDIA DLSS 超分辨率与 DLAA 集成。

### 功能

- 支持超级性能、性能、均衡、质量和 DLAA 档位。
- `F8` 启用或关闭 DLSS；`F9` 打开鼠标控制面板。
- 使用游戏深度、运动矢量和以零为中心的 Halton jitter。
- 可在 DLSS 原始输出上应用优化的各向同性 3×3 Gaussian USM 锐化，强度范围 0–1；设为 0 时完全绕过。
- 主相机输入不满足要求时回退到游戏自身抗锯齿。
- 默认启动时不启用 DLSS。

### 兼容性

- 《戴森球计划》`0.10.34.28529` / Unity `2022.3.62f3c1`、Mono、Built-in Render Pipeline
- Windows x64、Direct3D 11；Steam 启动选项保持为空
- BepInEx 5 x64（已验证 `5.4.17.0`）
- NVIDIA RTX 显卡和可用的新版本驱动

其他游戏版本和硬件配置尚未验证。

### 下载与安装

从 [v0.6.2 Release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.6.2) 下载 `DSP-DLSS-0.6.2-BepInEx5.zip`。

```text
4F02FAEBF513E29BE20E64B308B35C17D5028A2BCDB14837282B1E703CFA7B70  DSP-DLSS-0.6.2-BepInEx5.zip
```

1. 把 BepInEx 5 x64 安装到游戏根目录，并启动一次游戏。
2. 退出游戏，妥善保留已有的 `NVUnityPlugin.dll` 和 `nvngx_dlss.dll`，以便需要时恢复。
3. 若存在早期实验版的 `BepInEx\plugins\DSPDLSSDetail.dll` 或 `DSPDLSSRcas.dll`，请移除。
4. 把发布包内容解压到 `DSPGAME.exe` 所在目录。
5. Steam 启动选项保持为空；通过 Steam 启动并进入存档后使用 `F8` / `F9`。

文件位置及卸载步骤见[安装与移除说明](docs/INSTALLATION.md)。

### 操作与配置

- `F8`：启用或关闭 DLSS。
- `F9`：打开或关闭控制面板。
- 面板内可选择档位、Gaussian USM 锐化强度（0–1，步长 0.05）和技术信息显示。
- 配置保存在 `BepInEx\config\local.dsp.dlss.cfg`。

### 当前限制

- DLSS 低分辨率输入是在场景已经完成全分辨率渲染后准备的；重建分辨率和画质会变化，但前序 3D 渲染成本不会按比例降低。
- 大量程序化和 GPU 间接实例可能没有完整的物体运动矢量；传送带货物、物流单位、敌人、火箭、戴森结构、粒子和透明物体仍可能出现时域瑕疵。
- 集成位置在色调映射之后，且仅作用于主世界游戏相机。
- 发布包已完成构建与包体校验；不同系统上的最终画质和长时间稳定性仍需实际游戏验证。

### 构建

托管插件需要可编译 `netstandard2.1` 的 .NET SDK、BepInEx 5 和用户合法安装的游戏。原生锐化桥接还需要 Visual Studio 2022 C++ 工具、CMake、Windows SDK `fxc.exe` 和 Unity NativeRenderingPlugin 头文件。命令见上方英文部分。

仓库只包含作者编写的关键源码和说明，不跟踪构建输出、游戏程序集、代码生成结果或可再分发运行时 DLL。

### 第三方组件与项目状态

二进制发布包包含 NVIDIA DLSS 运行库，以及此 Unity Player 所需的匹配 Unity NVIDIA 原生运行库。详见[第三方说明](THIRD_PARTY_NOTICES.md)和压缩包内许可文件。

本项目是独立社区模组，与重庆柚子猫游戏、Gamera Games、Unity 或 NVIDIA 不存在隶属或背书关系。目前尚未为模组源码选择开源许可证。
