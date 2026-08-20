# DSP DLSS 0.6.2 installation / 安装说明

## Install / 安装

1. Close the game. / 退出游戏。
2. Install BepInEx 5 x64 and launch the game once. / 安装 BepInEx 5 x64 并启动一次游戏。
3. Preserve existing `NVUnityPlugin.dll` and `nvngx_dlss.dll` so they can be restored. / 妥善保留已有的两个同名 DLL，以便恢复。
4. Remove obsolete `BepInEx\plugins\DSPDLSSDetail.dll` and `DSPDLSSRcas.dll` if present. / 若存在早期实验版的这两个插件，请移除。
5. Extract the ZIP beside `DSPGAME.exe`. / 把 ZIP 内容解压到 `DSPGAME.exe` 所在目录。
6. Leave Steam launch options empty and launch through Steam. / Steam 启动选项保持为空，并通过 Steam 启动。

Expected runtime files / 运行文件位置：

```text
Dyson Sphere Program\
├─ NVUnityPlugin.dll
├─ nvngx_dlss.dll
└─ BepInEx\
   └─ plugins\
      ├─ DSPDLSSZeroMV.dll
      └─ DSPDLSSSharpen.dll
```

Use `F8` to enable or disable DLSS and `F9` to open the panel. The mod starts disabled. / 使用 `F8` 启用或关闭 DLSS，使用 `F9` 打开面板；模组默认不启用。

## Upgrade / 升级

Close the game, remove the two obsolete plugin DLLs listed above, and overwrite the four runtime files from the new archive. Keep `BepInEx\config\local.dsp.dlss.cfg` to retain settings. / 退出游戏，移除上述两个旧插件 DLL，再覆盖新版压缩包中的四个运行文件；保留配置文件即可继承设置。

## Remove / 卸载

Close the game, remove `DSPDLSSZeroMV.dll` and `DSPDLSSSharpen.dll`, then restore your previous root-level `NVUnityPlugin.dll` and `nvngx_dlss.dll`. Optionally remove `BepInEx\config\local.dsp.dlss.cfg`. Do not remove the entire BepInEx directory if other mods use it. / 退出游戏，删除两个 DSP DLSS 插件 DLL，并恢复原有的两个根目录运行库；配置文件可按需删除。若其他模组仍使用 BepInEx，请勿删除整个 BepInEx 目录。

## Verify / 校验

Compare files with `SHA256SUMS.txt` inside the archive. The outer ZIP digest is published as a separate `.sha256` release asset. / 使用包内 `SHA256SUMS.txt` 校验各文件；ZIP 整包哈希作为独立 `.sha256` Release 附件发布。
