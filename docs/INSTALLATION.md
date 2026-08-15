# DSP DLSS 0.5.2 安装说明

适用于《戴森球计划》Steam `0.10.34.28529` / Unity `2022.3.62f3c1` 的 BepInEx 5 模组候选。

本包只包含 DLSS Super Resolution / DLAA，不包含 DLSS Frame Generation、Streamline、Reflex 或代理 DLL。

## 运行要求

- Windows 10/11 64 位。
- 支持 DLSS 的 NVIDIA RTX 显卡及可用驱动。
- 《戴森球计划》Steam 版 `0.10.34.28529`。其他版本未经验证。
- BepInEx 5 x64；本包验证版本为 `5.4.17.0`。
- 使用 Direct3D 12 启动游戏。

## 包内文件与放置位置

把压缩包内容直接解压到游戏根目录，也就是与 `DSPGAME.exe` 同一目录。

默认 Steam 路径示例：

`C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program`

| 包内文件 | 最终位置 | 用途 |
|---|---|---|
| `NVUnityPlugin.dll` | `Dyson Sphere Program\NVUnityPlugin.dll` | 与游戏 Unity 版本匹配的 NVIDIA Unity 原生后端 |
| `nvngx_dlss.dll` | `Dyson Sphere Program\nvngx_dlss.dll` | NVIDIA DLSS SR 运行库 3.1.11 |
| `BepInEx\plugins\DSPDLSSZeroMV.dll` | `Dyson Sphere Program\BepInEx\plugins\DSPDLSSZeroMV.dll` | 模组主体 |
| `README-DSP-DLSS.md` | 可保留在游戏根目录或放到任意位置 | 本说明，不参与运行 |
| `SHA256SUMS.txt` | 可保留在游戏根目录或放到任意位置 | 文件完整性校验，不参与运行 |
| `THIRD_PARTY_NOTICES.md` | 可保留在游戏根目录或放到任意位置 | 第三方组件说明，不参与运行 |
| `licenses\*` | 可保留在游戏根目录或放到任意位置 | NVIDIA 许可与 Unity 运行库来源说明，不参与运行 |

正确结构应类似：

```text
Dyson Sphere Program\
├─ DSPGAME.exe
├─ NVUnityPlugin.dll
├─ nvngx_dlss.dll
├─ README-DSP-DLSS.md
├─ SHA256SUMS.txt
├─ THIRD_PARTY_NOTICES.md
├─ licenses\
│  ├─ nvngx_dlss.license.txt
│  └─ UNITY-NATIVE-RUNTIME-NOTICE.txt
└─ BepInEx\
   ├─ core\
   │  └─ BepInEx.dll
   ├─ config\
   └─ plugins\
      └─ DSPDLSSZeroMV.dll
```

## 安装步骤

1. 正常退出游戏。
2. 若尚未安装 BepInEx 5 x64，先将其安装到游戏根目录并启动游戏一次。确认根目录存在 `winhttp.dll`、`doorstop_config.ini`，且存在 `BepInEx\core\BepInEx.dll`。
3. 备份根目录中已有的 `NVUnityPlugin.dll` 和 `nvngx_dlss.dll`。如果它们来自其他图形模组，不要无备份覆盖。
4. 将本 ZIP 的全部内容直接解压到游戏根目录；允许创建 `BepInEx\plugins`。
5. 在 Steam → 游戏属性 → 启动选项中加入：

   ```text
   -force-d3d12
   ```

6. 从 Steam 正常启动游戏，不要直接双击 `DSPGAME.exe`。
7. 进入存档后按 F8 打开控制面板，用鼠标开启 DLSS 并选择档位。

首次运行会自动生成：

`Dyson Sphere Program\BepInEx\config\local.dsp.dlss.cfg`

发布包不附带也不覆盖该配置，避免破坏已有用户设置。安全默认值为不开机自动启用；进入游戏后使用 F8 面板开启。

## 控制方式

- F8：打开或关闭鼠标控制面板。
- 面板内：开启/关闭 DLSS、选择 Ultra Performance / Performance / Balanced / Quality / DLAA、开启/关闭信息显示。
- 关闭面板时会恢复进入面板前的鼠标锁定状态。

当前版本不使用 F9/F10；旧配置中残留的 `ModeCycle`、`InfoToggle` 或 `EnableToggle` 字段会被忽略。

## 验证是否加载

打开：

`Dyson Sphere Program\BepInEx\LogOutput.log`

应能看到类似内容：

```text
Loading [DSP DLSS 0.5.2]
Unity=2022.3.62f3c1, API=Direct3D12
NVIDIA device version=4; DLSS is available
```

进入游戏并开启 DLSS 后，信息框会显示当前档位与输入/输出尺寸。运行日志中的 `DLSS jitter audit: PASS` 表示投影、托管提交和 NVIDIA 原生读回一致；它不能单独代替最终画质检查。

## 升级

1. 退出游戏。
2. 备份旧的 `BepInEx\plugins\DSPDLSSZeroMV.dll`。
3. 覆盖包内三个运行文件。
4. 保留 `BepInEx\config\local.dsp.dlss.cfg` 即可继承设置；遇到异常时可先备份并删除该配置，让 0.5.2 重新生成。

不要在游戏运行时覆盖 DLL。

## 卸载与恢复

1. 正常退出游戏。
2. 删除 `BepInEx\plugins\DSPDLSSZeroMV.dll`。
3. 如果安装前根目录已有 `NVUnityPlugin.dll` 或 `nvngx_dlss.dll`，还原自己的备份。
4. 如果这两个文件原本不存在，而且当前哈希与本包一致，可以删除它们。
5. 可选删除 `BepInEx\config\local.dsp.dlss.cfg`。
6. 从 Steam 启动选项删除 `-force-d3d12`，即可回到原图形 API 选择。

不要删除整个 BepInEx 目录，除非确定其中没有其他模组。

## 本包文件哈希

```text
EEB883CD39EE4D785065D8F4452C588BC962B0B60CCAF084C8404D6D2EACF6F6  BepInEx\plugins\DSPDLSSZeroMV.dll
3B7E9765351BA3B38CE76B431EDF7E5CA3BB07FACC6F193203DE877A0ECA6E6C  NVUnityPlugin.dll
6A12400BEEDEF26AD8A3BACA8DDE2C6A5859942CE9AE14F78A07F7A87F883321  nvngx_dlss.dll
3027F23CA5A46DD9CB8183FBD522983A86F64D7DAAC5982912BF9F214671F294  licenses\nvngx_dlss.license.txt
```

## 已知限制

- 当前低分辨率输入由已经完成的全分辨率场景准备，因此可以改变 DLSS 重建档位与画质，但不会等比例降低前序 3D 渲染成本。
- 游戏大量 GPU 间接实例对象可能缺少完整物体运动矢量；传送带货物、物流单位、敌人、火箭、戴森结构、粒子和透明物体仍需观察拖影。
- 模组只对主世界游戏相机工作；菜单、特殊相机或非正常游戏场景会回退到游戏自身抗锯齿。
- 本版明确不包含 DLSS FG。不要与旧实验版的 `DSPStreamlineBootstrap.dll`、`nvngx_dlssg.dll`、`sl.*`、根目录 `dxgi.dll` 或 `d3d12.dll` 混用。

## 故障排查

- 没有 BepInEx 日志：先检查 BepInEx 5 x64 是否安装正确。
- 日志显示不是 Direct3D12：确认 Steam 启动选项包含 `-force-d3d12`。
- DLSS unavailable：检查 RTX 显卡、驱动、`NVUnityPlugin.dll` 和 `nvngx_dlss.dll` 是否位于游戏根目录。
- 启动崩溃：先移走 `BepInEx\plugins\DSPDLSSZeroMV.dll`；若恢复，再检查其他图形代理或旧 FG/Streamline 文件冲突。
- 画面出现拖影或网格：记录档位、输出分辨率和同场景截图，并附上 `BepInEx\LogOutput.log`。

此包是特定游戏版本的实验性图形模组。覆盖原生 DLL 前务必保留备份。
