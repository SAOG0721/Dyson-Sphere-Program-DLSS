# Dyson Sphere Program DLSS / 戴森球计划 DLSS

[English](#english) | [中文](#chinese)

<a id="english"></a>
## English

A DLSS Super Resolution and DLAA mod for **Dyson Sphere Program**.

### Features

- DLSS Ultra Performance, Performance, Balanced, Quality, and DLAA modes.
- F8 mouse-operated control panel for the DLSS switch, quality mode, and information overlay.
- Uses the game's depth and motion-vector textures.
- Applies zero-centered Halton jitter in DLSS input-pixel units to both the camera projection and DLSS submission.
- Preserves the game's post-processing order and native-resolution UI composition.
- Falls back to the game's anti-aliasing when the required camera inputs are unavailable.

### Compatibility

Validated with:

- Dyson Sphere Program `0.10.34.28529` (Steam build `23109513`)
- Unity `2022.3.62f3c1`, Mono, Built-in Render Pipeline
- Direct3D 12
- BepInEx 5.4.17 x64
- NVIDIA GeForce RTX 5070 Ti at 2560×1440

Other game builds and hardware configurations have not been validated.

### Download and installation

Download `DSP-DLSS-0.5.2-BepInEx5.zip` from the [v0.5.2 release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.5.2).

1. Install BepInEx 5 x64 into the game directory and launch the game once.
2. Exit the game and back up any existing `NVUnityPlugin.dll` and `nvngx_dlss.dll`.
3. Extract the release ZIP beside `DSPGAME.exe`.
4. Add `-force-d3d12` to the Steam launch options.
5. Launch through Steam, load a save, and press F8.

The archive places the runtime files here:

```text
Dyson Sphere Program\
├─ NVUnityPlugin.dll
├─ nvngx_dlss.dll
└─ BepInEx\
   └─ plugins\
      └─ DSPDLSSZeroMV.dll
```

See the [installation, upgrade, removal, and recovery guide](docs/INSTALLATION.md) for full file placement and checksum details.

### Controls

- F8: open or close the control panel.
- Enable DLSS: enable or disable DLSS for the main gameplay camera.
- Quality mode: select Ultra Performance, Performance, Balanced, Quality, or DLAA.
- Show info: display the current mode, input/output resolution, and runtime status.

The panel restores the previous cursor lock state when closed. Settings are stored in:

`BepInEx\config\local.dsp.dlss.cfg`

### Current limitations

- The lower-resolution DLSS inputs are prepared after the scene has rendered at full resolution. The modes perform low-input/high-output reconstruction, but they do not proportionally reduce the preceding 3D rendering cost.
- Dyson Sphere Program uses many procedural and indirect GPU instances. Some conveyors, cargo, logistics units, enemies, rockets, Dyson structures, particles, or transparent objects may not provide complete object motion vectors and can show temporal artifacts.
- DLSS is enabled only for the main gameplay camera. Menus, special cameras, and unsupported scenes use the game's anti-aliasing path.
- Compatibility is currently validated only for the game and Unity versions listed above.

Runtime validation details are available in [Release validation](docs/RELEASE_VALIDATION.md).

### Build

Requirements:

- A .NET SDK capable of targeting `netstandard2.1`
- A matching Dyson Sphere Program installation
- BepInEx 5 installed in that game directory

Set `DSP_GAME_DIR` or pass `DSPGameDir` explicitly:

```powershell
$env:DSP_GAME_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program'
dotnet build .\src\DSPDLSSZeroMV\DSPDLSSZeroMV.csproj -c Release
```

### Release checksum

```text
9F0216E1443A7F817C76D8B26370150BA3DE261391434D4920F2AFBC8BFD195D  DSP-DLSS-0.5.2-BepInEx5.zip
```

### Third-party components

The release archive contains the NVIDIA DLSS runtime and the matching Unity NVIDIA native runtime required by this Unity player. These components remain the property of their respective owners and are distributed subject to their own terms. See [Third-party notices](THIRD_PARTY_NOTICES.md) and the license files included in the release archive.

This is an independent community mod and is not affiliated with or endorsed by Youthcat Studio, Gamera Games, Unity, or NVIDIA. No open-source license has been selected for the mod source at this time.

---

<a id="chinese"></a>
## 中文

这是为《戴森球计划》提供 DLSS 超分辨率和 DLAA 的社区模组。

### 功能

- 支持 DLSS 超级性能、性能、均衡、质量和 DLAA 五个档位。
- 按 F8 打开鼠标控制面板，可操作 DLSS 开关、画质档位和信息显示。
- 使用游戏提供的深度纹理和运动矢量纹理。
- 以 DLSS 输入像素为单位生成以零为中心的 Halton jitter，并将相同数值同时应用到相机投影与 DLSS 提交。
- 保持游戏原有后处理顺序，并以原生分辨率合成 UI。
- 主相机输入不满足要求时自动回退到游戏自身的抗锯齿路径。

### 兼容性

已验证环境：

- 《戴森球计划》`0.10.34.28529`（Steam build `23109513`）
- Unity `2022.3.62f3c1`、Mono、Built-in Render Pipeline
- Direct3D 12
- BepInEx 5.4.17 x64
- NVIDIA GeForce RTX 5070 Ti，输出分辨率 2560×1440

其他游戏版本和硬件配置尚未验证。

### 下载与安装

从 [v0.5.2 Release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.5.2) 下载 `DSP-DLSS-0.5.2-BepInEx5.zip`。

1. 将 BepInEx 5 x64 安装到游戏根目录，并启动一次游戏。
2. 退出游戏，备份已有的 `NVUnityPlugin.dll` 和 `nvngx_dlss.dll`。
3. 将发布 ZIP 的内容解压到 `DSPGAME.exe` 所在目录。
4. 在 Steam 启动选项中加入 `-force-d3d12`。
5. 通过 Steam 启动游戏，进入存档后按 F8。

运行文件应位于：

```text
Dyson Sphere Program\
├─ NVUnityPlugin.dll
├─ nvngx_dlss.dll
└─ BepInEx\
   └─ plugins\
      └─ DSPDLSSZeroMV.dll
```

完整文件位置、升级、卸载、恢复和哈希校验说明见[安装指南](docs/INSTALLATION.md)。

### 控制方式

- F8：打开或关闭控制面板。
- 启用 DLSS：控制主游戏相机是否使用 DLSS。
- 画质档位：选择超级性能、性能、均衡、质量或 DLAA。
- 显示信息：显示当前档位、输入/输出分辨率和运行状态。

关闭面板后会恢复打开面板前的鼠标锁定状态。设置保存在：

`BepInEx\config\local.dsp.dlss.cfg`

### 当前限制

- DLSS 的低分辨率输入在场景完成全分辨率渲染后生成。各档位会执行低分辨率输入到高分辨率输出的重建，但不会按比例降低前序 3D 渲染成本。
- 《戴森球计划》使用了大量程序化和 GPU 间接实例。部分传送带、货物、物流单位、敌人、火箭、戴森结构、粒子或透明物体可能没有完整的物体运动矢量，因此仍可能出现时域瑕疵。
- DLSS 只对主世界游戏相机启用；菜单、特殊相机和不支持的场景会使用游戏自身的抗锯齿路径。
- 当前只验证了上方列出的游戏版本与 Unity 版本。

运行验证结果见[版本验证记录](docs/RELEASE_VALIDATION.md)。

### 构建

需要：

- 可编译 `netstandard2.1` 的 .NET SDK
- 与目标版本匹配的《戴森球计划》安装
- 已安装到游戏目录的 BepInEx 5

设置 `DSP_GAME_DIR`，或在构建时显式传入 `DSPGameDir`：

```powershell
$env:DSP_GAME_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program'
dotnet build .\src\DSPDLSSZeroMV\DSPDLSSZeroMV.csproj -c Release
```

### 发布包校验值

```text
9F0216E1443A7F817C76D8B26370150BA3DE261391434D4920F2AFBC8BFD195D  DSP-DLSS-0.5.2-BepInEx5.zip
```

### 第三方组件

发布包包含此 Unity Player 所需的 NVIDIA DLSS 运行库和匹配的 Unity NVIDIA 原生运行库。相关组件仍归各自权利方所有，并遵循其各自条款。详见[第三方说明](THIRD_PARTY_NOTICES.md)以及发布压缩包内的许可文件。

本项目是独立社区模组，与重庆柚子猫游戏、Gamera Games、Unity 或 NVIDIA 不存在隶属或背书关系。目前尚未为模组源码选择开源许可证。
