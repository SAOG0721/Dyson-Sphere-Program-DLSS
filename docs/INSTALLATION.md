# DSP DLSS 0.6.3 安装说明 / Installation Guide

## 中文

### 安装位置

把 `DSP-DLSS-0.6.3-All-in-One.zip` 的全部内容解压到 `DSPGAME.exe` 所在目录。正确结构如下：

```text
Dyson Sphere Program\
├─ DSPGAME.exe
├─ winhttp.dll
├─ doorstop_config.ini
├─ NVUnityPlugin.dll
├─ nvngx_dlss.dll
└─ BepInEx\
   ├─ core\
   └─ plugins\
      ├─ DSPDLSSZeroMV.dll
      └─ DSPDLSSSharpen.dll
```

本版本已经包含 BepInEx 5.4.17 x64 和所需运行组件，不需要单独安装前置。

### 安装前备份

如果游戏已经安装 BepInEx 或其他模组，请先备份游戏根目录中的 `winhttp.dll`、`doorstop_config.ini`、`NVUnityPlugin.dll`、`nvngx_dlss.dll` 和整个 `BepInEx` 文件夹。关闭游戏后再复制或覆盖文件。

### 启动

Steam 启动选项保持默认。通过 Steam 启动游戏、进入存档，然后按 `F8` 开启 DLSS；按 `F9` 打开控制面板。第一次启动默认关闭 DLSS。

### 更新

退出游戏并备份现有文件，然后把新版 ZIP 重新解压到游戏目录并覆盖同名文件。压缩包不包含 `BepInEx\config\local.dsp.dlss.cfg`，已有设置通常会保留。

### 卸载

只卸载本模组时，删除：

```text
BepInEx\plugins\DSPDLSSZeroMV.dll
BepInEx\plugins\DSPDLSSSharpen.dll
```

恢复安装前备份的 `NVUnityPlugin.dll` 和 `nvngx_dlss.dll`。如果没有其他模组使用 BepInEx，可再删除本安装包加入的 `winhttp.dll`、`doorstop_config.ini` 和 `BepInEx` 文件夹。

---

## English

### Install location

Extract all contents of `DSP-DLSS-0.6.3-All-in-One.zip` beside `DSPGAME.exe`. The resulting layout should be:

```text
Dyson Sphere Program\
├─ DSPGAME.exe
├─ winhttp.dll
├─ doorstop_config.ini
├─ NVUnityPlugin.dll
├─ nvngx_dlss.dll
└─ BepInEx\
   ├─ core\
   └─ plugins\
      ├─ DSPDLSSZeroMV.dll
      └─ DSPDLSSSharpen.dll
```

This release already includes BepInEx 5.4.17 x64 and all required runtime components. No separate prerequisite installation is needed.

### Back up before installation

If BepInEx or other mods are already installed, back up `winhttp.dll`, `doorstop_config.ini`, `NVUnityPlugin.dll`, `nvngx_dlss.dll`, and the entire `BepInEx` folder from the game directory. Close the game before copying or replacing files.

### Start the mod

Keep Steam launch options at their defaults. Start the game through Steam, load a save, press `F8` to enable DLSS, and press `F9` to open the control panel. DLSS starts disabled on first launch.

### Update

Close the game, back up the current files, then extract the new ZIP into the game directory and replace matching files. The archive does not contain `BepInEx\config\local.dsp.dlss.cfg`, so existing settings are normally preserved.

### Remove

To remove only this mod, delete:

```text
BepInEx\plugins\DSPDLSSZeroMV.dll
BepInEx\plugins\DSPDLSSSharpen.dll
```

Restore the `NVUnityPlugin.dll` and `nvngx_dlss.dll` files backed up before installation. If no other mods use BepInEx, you may also remove the package's `winhttp.dll`, `doorstop_config.ini`, and `BepInEx` folder.
