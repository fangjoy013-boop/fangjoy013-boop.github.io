# 拾光 · 个人生活助手

一个可离线运行的 Windows 桌面待办应用。当前版本支持添加、编辑、搜索、分类、优先级、截止日期、完成/恢复、删除和本地自动保存。

## 新增功能怎么用

- **编辑**：点击任务右侧“编辑”，在上方修改内容，再点“保存修改”。“取消”会保留原任务；编辑不会改变完成状态。
- **搜索**：在列表上方输入标题关键词，即时显示结果，英文不区分大小写。
- **分类**：可选生活、学习、工作、其他；右侧分类筛选可以与关键词和完成状态一起使用。
- **优先级**：可选低、普通、高。未完成任务排在前面，同一状态下高优先级优先，再按截止日期从早到晚排列，无截止日期排后，最后按新任务优先。
- **截止日期**：按 `yyyy-MM-dd` 输入，例如 `2026-11-01`；留空表示不设截止日期。不合法的日期不能保存。未完成且日期早于今天的任务显示红色“已逾期”，今天截止不算逾期。
- **筛选**：可切换全部、待完成、已完成、已逾期；“重置筛选”清除关键词、分类和状态筛选。

升级旧版本时，原有任务自动保留，默认分类为“生活”、优先级为“普通”、无截止日期。日期提示只在应用中显示，不会弹出系统通知。

## 立即启动

双击本文件旁的 **LifeAssistant.exe**。也可双击 **Start.cmd**。

从 GitHub 获取时，点击仓库的 **Code → Download ZIP**，完整解压后进入 `LifeAssistant` 文件夹运行。仓库不包含个人待办数据库，首次运行会自动创建空的 `data/tasks.db`。

无需安装 Node.js、npm、Visual Studio 或数据库服务器。程序已经在当前电脑编译成功。适用环境为带 .NET Framework 4.x 和系统 `winsqlite3.dll` 的 64 位 Windows；本次已在当前电脑验证。

请将整个文件夹放在可写位置，例如“文档”。不要放到需要管理员权限的 Program Files。重复打开同一份程序时，会提示已有窗口运行。

## 现场演示（约 2 分钟）

1. 不输入任何文字，点击“添加任务”：展示空标题提示；只输入空格同样不能添加。
2. 输入“阅读 20 页 C++ 课本”，按 Enter：任务出现，待完成数量增加。
3. 再添加“晚饭后散步 15 分钟”：展示多条任务。
4. 点击第一条任务的“完成”：显示绿色已完成标签、删除线，按钮变为“恢复”。
5. 点击“已完成”筛选，再点击“恢复”：任务回到待完成；切换“全部”可以看到它。
6. 点击“删除”：先选择“否”展示取消，再次删除并选择“是”展示真正删除。
7. 留下一条任务，关闭窗口，然后双击程序重新打开：任务、标题和完成状态仍保留。

任务按“未完成优先 → 优先级 → 截止日期 → 新任务优先”排列。标题最长 120 个 UTF-16 字符单位（大多数中文、英文各占 1 个，部分 emoji 占 2 个）。

## 数据在哪里

数据保存在程序旁的 **data/tasks.db**，是一个真正的 SQLite 数据库文件。每次操作成功后立即写入磁盘，不需要点击保存。底部提示上悬停可查看完整路径。

备份或换文件夹：先关闭应用，再复制整个 LifeAssistant 文件夹，或单独备份 `data/tasks.db`。只复制 exe 不会带走旧任务。删除任务有确认提示，但确认后没有撤销功能。

## 为什么没有采用 Vue + Electron

检查时，PATH 中有 Node.js 24.22.1，但没有可直接调用的 npm、C++ 编译器或 .NET SDK。补查后在 Code::Blocks 的 MinGW 目录中找到了未加入 PATH 的 `g++.exe`，所以电脑也具备 C++ 编译条件。Windows 现有的 .NET Framework 编译器、WPF 和 SQLite DLL 可直接使用，本版选择 **C# + WPF + SQLite**，减少桌面界面开发、依赖下载和打包时间。

它是原生桌面窗口，不需要启动本地网页服务器。界面和数据库都在本机，不包含账号、云同步、记账、日程或日记。

## 给 C++ 初学者的文件说明

| 文件 | 作用 | 用 C++ 的思路理解 |
| --- | --- | --- |
| App.cs | 程序入口、窗口启动、按钮事件、刷新列表 | `Main` 类似 `main()`；点击按钮会调用一个函数 |
| MainWindow.xaml | 中文文字、颜色、布局、任务卡片样式 | 一份“窗口布局说明书”，把外观与处理逻辑分开 |
| TaskStore.cs | 任务数据类、数据库查询和修改 | `TodoTask` 类似 `struct`；`TaskStore` 是管理数据的类 |
| StoreTests.cs | 自动检查增删改查、输入验证和重开保存 | 用条件判断检查结果，不符合就报错 |
| build.ps1 | 调用 Windows 的 C# 编译器 | 类似执行 `g++`，把源代码编译成 exe |
| Start.cmd | 双击启动；exe 缺失时先编译 | 一个启动快捷脚本 |
| LifeAssistant.exe | 已编译好的应用 | 可以直接运行的程序 |
| data/tasks.db | 用户任务 | 相当于保存在磁盘上的数据集合 |

### 最重要的一条调用链

点击“添加任务” → `TodoController.Add()` → `TaskStore.Add()` 校验并写入 SQLite → `Reload()` 读取列表 → 窗口更新。

`List<TodoTask>` 可以先理解为 C++ 的 `vector<TodoTask>`；`using` 会在结束时自动释放数据库资源，类似 RAII；`delegate` 和 `Action` 可以先理解为“可以传给别人的函数”。

SQLite 的 SQL 使用参数绑定。标题即使带引号，也只会被当作普通文字。底部只有在数据库操作成功后才会提示已保存。

## 修改和重新编译

先关闭应用。在这个文件夹打开 PowerShell，执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\LifeAssistant.exe
```

`ExecutionPolicy Bypass` 只作用于这次脚本进程，不修改系统执行策略。编译会把 XAML 嵌入 exe；因此修改界面后也需要重新编译。重新编译不会删除 `data/tasks.db`。

## 运行自动测试

在项目文件夹内执行：

```powershell
$testDir = Join-Path $env:TEMP 'LifeAssistant-tests'
$p = Start-Process -FilePath .\LifeAssistant.exe -ArgumentList ('--self-test "' + $testDir + '"') -Wait -PassThru
Get-Content (Join-Path $testDir 'test-results.txt')
$p.ExitCode
```

退出码 0 表示全部通过。测试会创建独立数据库，不修改你的正式任务。

## 后续学习顺序

先看 `TodoTask` 和 `TaskStore.Add()`，再看 `TodoController.Add()`，最后看 XAML 中的按钮和任务模板。调用系统 SQLite 的 DLL 声明属于进阶内容，第一轮演示时不必逐行掌握。

技术参考：[微软 WPF XamlReader 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.markup.xamlreader)。本项目使用它读取嵌入程序的界面布局。
