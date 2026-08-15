# Dyson Sphere Program DLSS：实现误区与经验复盘

日期：2026-08-15
适用版本：Dyson Sphere Program `0.10.34.28529` / Unity `2022.3.62f3c1` / Mono / Built-in Render Pipeline
最终候选：DSP DLSS `0.5.2`，DLSS SR / DLAA only

## 1. 最重要的结论

这次实现最容易犯的错误，是把“某一层看起来工作”当成“DLSS 已正确输出”。正确判断必须区分以下证据层级：

1. DLL 存在；
2. 模块进入进程；
3. NVIDIA 设备与 Feature 创建成功；
4. 每帧 Execute 被提交；
5. 颜色、深度、运动矢量、jitter、reset 的尺寸与约定正确；
6. NVIDIA 原生侧读回与托管提交一致；
7. 最终画面确实消费 DLSS 输出；
8. 固定场景 A/B 没有不可接受的网格、拖影、闪烁和错误遮挡；
9. 性能测量证明前序 3D 渲染成本真的下降。

只有第 1～4 层时，最多能说“功能被加载或调用”。即使第 5～6 层通过，也仍不能替代第 7～9 层的输出证据。

## 2. 不要把其他 Unity 游戏的代码直接搬过来

Escape from Duckov 的经验有参考价值，但它与 DSP 的渲染管线并不相同。可以复用的是调查方法：寻找 jitter、Depth、Motion、历史 reset 与 UI 合成阶段；不能直接复用 URP Renderer Feature 或 CameraData 接入代码。

DSP 使用 Built-in Render Pipeline，并保留旧 Post Processing Stack。最合适的结构不是额外插入一套 URP pass，而是把旧 TAA 当作输入与时序外壳：

- 保留 `OnPreCull / TaaComponent.SetProjectionMatrix`；
- 继续请求 `DepthTextureMode.Depth | MotionVectors`；
- 保存 non-jittered projection；
- 只在 `TaaComponent.Render(source, destination)` 替换时域合成；
- 让 Bloom、DOF、Color Grading 与后置 UI Camera 继续走原游戏链。

这个“TAA shell”避免了自行重建完整相机时序，也让 DLSS 输出继续进入游戏原有后处理链。

## 3. DLL 与 ABI 误区

### 误区：有 `UnityEngine.NVIDIAModule.dll` 就能直接执行 DLSS

托管程序集只证明 Unity 暴露了 NVIDIA API 外壳。还必须存在版本匹配的 `NVUnityPlugin.dll`、`nvngx_dlss.dll`，并分别验证 capability、Create、Execute 与 Destroy。

### 误区：只要换成同版本原生 DLL，崩溃就会消失

游戏目录中早期使用的旧 `NVUnityPlugin.dll` 缐少 `NVUP_GetBaseEventId`。兼容旧事件号后，第一次 Execute 在 UnityPlayer 原生渲染线程触发 `0xc0000005`，说明 ABI 不匹配确实危险。

但换成 Unity `2022.3.62f3` 匹配后端后，旧 Execute 仍然崩溃。最终还发现：

- 把普通 PostFX destination 直接作为 DLSS `colorOutput`，没有 UAV 保证；
- MV scale 为 0；
- normalized jitter 被误当作 pixel jitter；
- `invertYAxis` 与 Unity 官方实现不一致。

因此“匹配 DLL”只是必要条件，资源属性和参数约定同样能造成渲染线程崩溃。

### 有效做法

- 使用独立 `enableRandomWrite=true` 的 DLSS 输出 RenderTexture；
- Execute 后再 Blit 回 PostFX destination；
- 与 Unity 官方 HDRP 同版本源码逐字段对照初始化和执行参数；
- 每次改变一个变量，保留崩溃日志、DLL 版本、SHA-256 与恢复副本。

## 4. 运动矢量误区

### 误区：先传全零 MV，画面能出就算接入成功

全零 MV 只能证明最基础 Execute 路径，不适合作为正式实现。0.3.0 起必须取得 TAA 请求的 `_CameraMotionVectorsTexture`；缺失或尺寸不匹配时拒绝 DLSS Execute 并回退游戏 TAA。

### 误区：全局 MotionVectors 纹理存在，所有物体就都有正确速度

DSP 大量对象使用 `DrawMeshInstancedIndirect` / `DrawProcedural` 与自定义 GPU 实例数据。扫描 484 个 Shader 未发现这些类别完整的 MotionVectors pass 或 previous transform。传送带货物、物流船/无人机、敌人、火箭、戴森结构等仍可能只有相机运动或零物体速度。

所以：

- `supportsMotionVectors=true` 是引擎能力证据，不是场景覆盖证据；
- RGHalf 全局纹理存在也不是每类动态对象正确的证明；
- 必须通过运动矢量 Debug View 和具体对象运动 A/B 验证拖影、解遮挡和历史拒绝。

### Unity Built-in / Direct NGX 约定

当前 Unity motion vector 输入按官方 Unity DLSS 路径使用：

- `mvScaleX = -inputWidth`；
- `mvScaleY = -inputHeight`；
- `invertYAxis = 1`；
- motion buffer 与 DLSS input/render 尺寸一致；
- reversed-Z 时启用 `DepthInverted`。

Direct NGX 与 Streamline 的职责不同，不能把另一条集成路径中的 motion scale 常量原样套用。

## 5. Jitter：本次最关键的两层误判

### 第一层：审计时点错误造成假阴性

最初在 `TaaComponent.Render` 读取 `Camera.projectionMatrix`，只有 `2/16` 匹配。反编译后确认时序为：

1. `OnPreCull` 调用 `SetProjectionMatrix`；
2. 场景使用 jittered projection 渲染；
3. `OnPostRender` 调用 `ResetProjectionMatrix`；
4. `OnRenderImage` 才进入 `TaaComponent.Render`。

因此 Render 阶段看到的是已重置投影。正确审计点是 `SetProjectionMatrix` Postfix，也就是场景真正使用 jitter 之前。调整后 projection、托管 submit、NVIDIA native DebugView 均达到 `16/16`。

### 第二层：数值传递一致仍可能是错误 jitter

第一次全链 PASS 后，玩家截图仍显示：

- Ultra Performance 约 3 像素周期网格；
- Performance 约 2 像素周期网格。

伪影周期与缩放倍率一致，是输入网格 phase coverage 不足的重要线索。

DSP 旧 TAA 使用 8 相 Halton(2,3)，但 `GenerateRandomOffset` 直接返回 `[0,1)`，随后乘 `jitterSpread=0.75` 并按输出尺寸归一化。这有三个问题：

- 相位全部偏正，不以零为中心；
- 包含 Halton index 0 的 `(0,0)`；
- full-resolution scene 后再降采样时，输入网格中的振幅被缩小为 `input/output`。

旧实现把同一个错误值正确写入了投影和 DLSS，所以参数审计会 PASS，但时域重建没有获得符合输入网格的完整相位覆盖。

### 正确转换

Unity 2022.3 HDRP 的 DLSS 路径同样使用 8 相 Halton，但会跳过 index 0、减去 `0.5`，并且 DLSS 开启时不乘普通 TAA jitter scale。

0.5.2 的转换是：

1. 从 DSP 的 `Halton * jitterSpread` 恢复原始 Halton；
2. 将 `(0,0)` 槽映射为 Halton index 8，使循环等价于 1..8；
3. 减去 `0.5`，得到完整、零中心的一输入像素相位；
4. 按当前 DLSS input width / height 写回相机投影；
5. 把同一个实际投影偏移以负的 input/render pixel 单位提交给 DLSS。

各档倍率按 NVIDIA 返回的实际输入尺寸动态计算，不硬编码：1440p 实测 DLAA `1x`、Quality `1.5x`、Balanced 约 `1.72x`、Performance `2x`、Ultra Performance 约 `3x`。五档 projection / submit / native 审计全部通过，X/Y 都覆盖正负相位。

可复用原则：不能只放大提交给 DLSS 的 jitter 常量；相机实际投影必须同步改变，否则颜色、深度、MV 与 jitter 会互相矛盾。

## 6. 分辨率与性能误区

### 误区：输入尺寸变小就代表性能版 DLSS 已完成

当前 SR 档把已经完成的全分辨率场景颜色、深度与 MV 准备为低分辨率输入，再由 DLSS 输出到 1440p。它确实执行了低输入到高输出的 DLSS 重建，适合验证参数和画质，但前序几何、阴影、光照与场景着色已经按全分辨率支付成本。

因此不能把模式切换、输入 RenderTexture 变小或 DLSS Execute 成功写成“GPU 渲染成本已下降”。真正性能版还需要：

- 低分辨率主世界 3D 渲染；
- 匹配的低分辨率 Depth/Motion；
- 原生分辨率 UI 后置合成；
- GPU 时间与场景成本 A/B。

D3D12 是 Unity 2022.3 Built-in 动态分辨率的优先方向；D3D11 通常需要额外的 offscreen camera 方案。

## 7. DLSS Frame Generation 误区

### 误区：Streamline 加载、状态为 OK 或资源标签成功就等于 FG 生效

FG 必须接管实际交换链与 Present。实验中先后观察到：

- 过早通过根目录代理调用 `slInit`，会在 Unity/Mono 初始化阶段访问冲突；
- 延迟初始化后可取得 D3D12 device / swap chain / command queue，并完成 `slSetD3DDevice`；
- FG 状态、资源标签和常量可以无错误；
- 但 `numFramesActuallyPresented` 始终为 `0`。

这只能说明后端部分初始化，不能说明生成帧进入显示队列。只有 `DLSSGState.status=eOk` 且统计窗口内 `numFramesActuallyPresented>1`，再结合 Present 计数和外部帧时间证据，才能称为 FG 生效。

由于候选部署出现直接退出且没有实际生成帧证据，正式 0.5.2 已移除 FG/Streamline 部署，恢复官方 `NVUnityPlugin.dll`，并从游戏目录移走 bootstrap、`nvngx_dlssg.dll` 和 `sl.*` 组件。

## 8. Unity 模组工程与测试经验

- 不要直接运行 `DSPGAME.exe` 做正式验证；该游戏会因 SteamAPI 失败退出。使用 Steam `-applaunch 1366540 -force-d3d12`。
- 修改已加载 DLL 前先让游戏正常退出；不要在用户可能尚未保存时强制结束进程。
- 切场景、切档、resize 和 Feature 重建时必须 reset history。
- 输入缺失或异常时优先回退游戏 TAA，不要继续提交不完整 DLSS 帧。
- Render thread 成功路径避免每帧 P/Invoke 状态读取、字符串格式化和日志写盘；诊断应低频采样。
- 每次部署记录游戏版本、API、GPU、DLL 版本与 SHA-256，并保留可恢复备份。
- 源码和文档也要备份。本轮曾出现 `Plugin.cs` 与 README 被意外覆盖/写成 `[object Object]`；二进制备份不能替代可审计源文件。

## 9. 正式模组与公开发布经验

- 发布包应镜像游戏根目录，让用户直接解压，并逐文件说明目标路径。
- 不覆盖用户配置；让 BepInEx 首次运行生成安全默认配置。
- 明确 BepInEx、D3D12、游戏版本和 RTX GPU 前置条件。
- 提供安装、升级、卸载、恢复、日志位置与 SHA-256。
- SR-only 包不得混入旧 FG、Streamline、Reflex、`dxgi.dll` 或 `d3d12.dll`。
- 公开仓库不上传用户存档、日志、崩溃转储、备份、个人绝对路径或研究工作区历史。
- 二进制 Release 与源码历史分离；第三方 NVIDIA/Unity 组件应记录来源，并在公开分发前确认适用许可。
- GitHub Contributors 来自提交历史。要保持单一贡献者，应创建全新非 Fork 仓库、使用账号关联邮箱、避免 `Co-authored-by`、机器人提交和继承历史。

## 10. 当前证据与尚未完成的工作

已确认：

- Unity NVIDIA Feature 创建与逐帧 Execute；
- 五档 NVIDIA 推荐输入尺寸；
- 真实 Depth 与 Motion texture 接入；
- jitter 在实际投影、托管提交与 native DebugView 三层一致；
- 五档输入网格倍率与正负相位覆盖；
- 多档切换超过一万帧，额外稳定检查无 native jitter mismatch；
- FG 正式部署已移除。

仍需谨慎表述：

- 最终同场景 A/B 是否彻底消除倍率网格；
- GPU 间接实例对象的物体 MV 覆盖；
- 粒子、透明层、解遮挡与高速运动拖影；
- 真正低分辨率 3D 渲染带来的 GPU 性能收益。

## 11. 可复用验收清单

在下一款游戏中，应按顺序确认：

1. 引擎、管线、API、游戏版本与反作弊边界；
2. 插入点位于正确后处理阶段且 UI 合成顺序明确；
3. 输出资源具备 API 要求的 UAV/格式/尺寸；
4. Depth 与 Motion 覆盖、方向、单位、分辨率和动态物体质量；
5. jitter 的生成相位、实际投影、输入像素单位、符号与 native 读回；
6. reset、切场景、切档、resize 与失败回退；
7. Feature/Execute 证据；
8. 最终输出 A/B；
9. 稳定性、GPU 时间与 Present 证据；
10. 可恢复部署、哈希、文档与干净发布包。

最值得保留的一句话是：**“同一个错误参数被完整传递”也能通过链路审计；时域重建必须同时验证数学约定、资源语义和最终画面。**
