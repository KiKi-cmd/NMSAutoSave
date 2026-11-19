# NMSAutoSave · 无人深空存档自动备份

一个 .NET Framework 4.8 的 WinForms 小工具，用于给 **《无人深空》（No Man's Sky，Steam AppID 275850）** 自动备份存档。

游戏本体并不提供存档历史，一旦存档损坏或被覆盖就只能重来。这个工具在游戏运行期间定时把 `.hg` 存档文件复制到带时间戳的备份目录，出问题时可以从最近的备份回滚。

## 功能

- 启动时检测游戏进程，未运行则通过 `steam://rungameid/275850` 拉起游戏
- 自动定位存档目录 `%APPDATA%\HelloGames\NMS\<st_账号ID>`
- 每 **5 分钟**检查一次存档目录
- 只在检测到游戏确实在写盘时才备份（最新文件修改时间距今 ≤ 10 分钟），避免产生大量无意义副本
- 备份到 `BackUp\yyyy-MM-dd_HH_mm_ss\`，保留该目录下所有 `.hg` 文件
- 游戏进程退出后工具自动退出

## 工作原理

无人深空的存档由四个文件组成：

| 文件 | 说明 |
|---|---|
| `save.hg` / `save2.hg` | 主存档的两个槽位 |
| `mf_save.hg` / `mf_save2.hg` | 对应的元数据文件 |

工具每次备份会把当前存档目录下所有 `.hg` 文件整体复制到新的时间戳子目录，四个文件保持同一时刻的快照，回滚时可以整组替换。

## 存档目录结构

```
%APPDATA%\HelloGames\NMS\
└── st_76561198xxxxxxxxx\        <- Steam 账号 ID，工具自动选取最近写入的那个
    ├── save.hg
    ├── save2.hg
    ├── mf_save.hg
    ├── mf_save2.hg
    └── BackUp\                  <- 工具创建的备份目录
        ├── 2026-03-04_22_44_52\
        ├── 2026-03-04_22_49_52\
        └── ...
```

## 构建与运行

需要 Visual Studio 2022 与 .NET Framework 4.8 开发工具。

```powershell
git clone https://github.com/KiKi-cmd/NMSAutoSave.git
cd NMSAutoSave
msbuild NMSAutoSave.sln /p:Configuration=Release
```

或直接用 Visual Studio 打开 `NMSAutoSave.sln` 后按 F5。

生成的 `NMSAutoSave.exe` 可以放到任意位置运行，无外部依赖。

## 回滚存档

1. 完全退出游戏
2. 进入存档目录 `%APPDATA%\HelloGames\NMS\st_<你的ID>\`
3. 把当前四个 `.hg` 文件另存一份（万一要再退回来）
4. 从 `BackUp\<想要的时间戳>\` 复制全部 `.hg` 文件回去

## 已知限制

- 备份期间游戏若正在写入 `.hg`，可能复制到不完整文件（建议只在游戏内自动存档点附近回滚，或人工核对大小）
- 备份只增不减，没有自动轮转清理，长期使用需要手动删除旧目录
- 存档根目录硬编码为 `%APPDATA%`，不适用于移动存档或自定义路径的情况
- 仅支持单账号下"最近写入"的那个存档目录，多账号场景需要手动改

## 许可

[MIT](LICENSE)
