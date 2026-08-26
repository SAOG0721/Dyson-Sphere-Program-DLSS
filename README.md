# Dyson Sphere Program DLSS / 《戴森球计划》DLSS 模组

[中文](#中文) | [English](#english)

## 中文

这是为《戴森球计划》提供 NVIDIA DLSS 超分辨率与 DLAA 的社区模组。

### 0.6.3 一体版

`0.6.3` 是自带前置依赖的完整 ZIP。压缩包已包含 BepInEx 5.4.17 x64、DLSS 运行库以及模组所需组件，不需要另外下载或安装 BepInEx。

从 [v0.6.3 Release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.6.3) 下载 `DSP-DLSS-0.6.3-All-in-One.zip`。

```text
99867758212CA025E5E2482E3CC24667E5E136CAA0AF2EED20F3FE53C846AA23  DSP-DLSS-0.6.3-All-in-One.zip
```

支持环境：

- 《戴森球计划》`0.10.34.28529`（Steam build `23109513`）
- Windows 64 位
- NVIDIA RTX 显卡及较新的 NVIDIA 驱动
- Steam 启动选项保持默认

其他游戏版本尚未验证。

### 安装

1. 完全退出游戏。
2. 如果游戏已安装 BepInEx 或其他模组，请先备份游戏根目录中的 `winhttp.dll`、`doorstop_config.ini`、`NVUnityPlugin.dll`、`nvngx_dlss.dll` 和整个 `BepInEx` 文件夹。
3. 把 ZIP 内的全部内容解压到 `DSPGAME.exe` 所在目录，并允许合并文件夹及覆盖同名文件。
4. 保持 Steam 启动选项为默认状态，通过 Steam 启动游戏并进入存档。
5. 按 `F8` 开启 DLSS，按 `F9` 打开控制面板。

不要把 ZIP 解压到 `BepInEx\plugins`：压缩包中的目录结构已经安排好。

### 操作

- `F8`：开启或关闭 DLSS。
- `F9`：打开或关闭鼠标控制面板。
- 控制面板可选择超级性能、性能、均衡、质量和 DLAA，并可调整锐化强度及信息显示。
- 模组首次启动默认关闭；设置保存在 `BepInEx\config\local.dsp.dlss.cfg`。

### 更新与卸载

从旧版更新时，请退出游戏、备份现有文件，然后把新版 ZIP 重新解压到游戏目录。安装包不包含用户配置，因此通常会保留原设置。

只移除本模组时，删除：

```text
BepInEx\plugins\DSPDLSSZeroMV.dll
BepInEx\plugins\DSPDLSSSharpen.dll
```

随后恢复安装前备份的 `NVUnityPlugin.dll` 和 `nvngx_dlss.dll`。如果没有其他模组需要 BepInEx，也可以一并移除本安装包加入的 `winhttp.dll`、`doorstop_config.ini` 和 `BepInEx` 文件夹；否则不要删除整个 BepInEx。

### 常见问题

- 启动即退出：确认 Steam 启动选项为默认状态，更新 NVIDIA 驱动，并检查是否仍有旧版或冲突的插件 DLL。
- `F8` 没有效果：先进入实际游戏存档，再尝试开启。
- 需要反馈问题时，请附上 `BepInEx\LogOutput.log`、游戏版本、显卡型号和驱动版本。

### 已知限制

- 切换 DLSS 档位会改变重建分辨率和画面表现，但目前不会按比例降低此前的场景渲染开销。
- 部分传送带货物、物流单位、敌人、火箭、戴森球结构、粒子及透明物体可能出现时域残影或抖动。

详细的文件位置与恢复方法见[安装说明](docs/INSTALLATION.md)。第三方组件信息见[第三方声明](THIRD_PARTY_NOTICES.md)。

本项目是独立社区模组，与重庆柚子猫游戏、Gamera Games、Unity 或 NVIDIA 不存在隶属或背书关系。

---

## English

This community mod adds NVIDIA DLSS Super Resolution and DLAA to **Dyson Sphere Program**.

### Version 0.6.3 all-in-one package

Version `0.6.3` is a self-contained ZIP. It includes BepInEx 5.4.17 x64, the DLSS runtime, and every runtime component required by the mod. No separate BepInEx download or installation is required.

Download `DSP-DLSS-0.6.3-All-in-One.zip` from the [v0.6.3 release](https://github.com/SAOG0721/Dyson-Sphere-Program-DLSS/releases/tag/v0.6.3).

```text
99867758212CA025E5E2482E3CC24667E5E136CAA0AF2EED20F3FE53C846AA23  DSP-DLSS-0.6.3-All-in-One.zip
```

Supported environment:

- Dyson Sphere Program `0.10.34.28529` (Steam build `23109513`)
- 64-bit Windows
- NVIDIA RTX GPU with a recent NVIDIA driver
- Default Steam launch options

Other game versions have not been validated.

### Installation

1. Close the game completely.
2. If BepInEx or other mods are already installed, back up `winhttp.dll`, `doorstop_config.ini`, `NVUnityPlugin.dll`, `nvngx_dlss.dll`, and the entire `BepInEx` folder from the game directory.
3. Extract everything from the ZIP beside `DSPGAME.exe`, allowing folder merging and replacement of matching files.
4. Keep Steam launch options at their defaults, start the game through Steam, and load a save.
5. Press `F8` to enable DLSS and `F9` to open the control panel.

Do not extract the ZIP directly into `BepInEx\plugins`; the archive already contains the correct directory structure.

### Controls

- `F8`: enable or disable DLSS.
- `F9`: open or close the mouse-operated control panel.
- The panel provides Ultra Performance, Performance, Balanced, Quality, and DLAA modes, plus sharpening strength and information display controls.
- The mod starts disabled on first launch. Settings are stored in `BepInEx\config\local.dsp.dlss.cfg`.

### Updating and removal

To update from an earlier version, close the game, back up the existing files, and extract the new ZIP into the game directory. The package does not contain a user configuration file, so existing settings are normally preserved.

To remove only this mod, delete:

```text
BepInEx\plugins\DSPDLSSZeroMV.dll
BepInEx\plugins\DSPDLSSSharpen.dll
```

Then restore the `NVUnityPlugin.dll` and `nvngx_dlss.dll` files backed up before installation. If no other mods need BepInEx, you may also remove the package's `winhttp.dll`, `doorstop_config.ini`, and `BepInEx` folder. Do not remove the whole BepInEx installation when other mods use it.

### Troubleshooting

- Game exits during startup: restore default Steam launch options, update the NVIDIA driver, and check for obsolete or conflicting plugin DLLs.
- `F8` has no effect: load an actual gameplay save before enabling DLSS.
- When reporting a problem, include `BepInEx\LogOutput.log`, the game version, GPU model, and driver version.

### Known limitations

- DLSS modes change reconstruction resolution and image presentation, but currently do not proportionally reduce earlier scene-rendering work.
- Some conveyor cargo, logistics units, enemies, rockets, Dyson structures, particles, and transparent objects may show temporal ghosting or jitter.

See the [installation guide](docs/INSTALLATION.md) for file placement and recovery details. See [third-party notices](THIRD_PARTY_NOTICES.md) for bundled components.

This is an independent community mod and is not affiliated with or endorsed by Youthcat Studio, Gamera Games, Unity, or NVIDIA.
