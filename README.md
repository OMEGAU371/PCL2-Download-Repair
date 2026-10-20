# PCL2 Download Repair

第三方 PCL 下载页临时扩展，**当前为测试版**。与 PCL、Mojang、Microsoft 均无隶属关系。包内不含 PCL 本体或 Minecraft 客户端。

## 普通用户：解压后点两下

1. 从本仓库 [Releases](https://github.com/OMEGAU371/PCL2-Download-Repair/releases) 下载 `PCL2-Download-Repair.zip`，先完整解压。
2. 把 `PCL下载助手.exe`、它的 `.config` 和整个 `PCL-Shell-Auto` 文件夹放到 **PCL2.exe 所在文件夹**。这里指启动器 EXE 的目录，不是 `.minecraft/versions`。
3. 关闭已打开的 PCL，双击 `PCL下载助手.exe`，点击 **启动下载扩展**。没有自动找到时，点击“选择 PCL”。
4. 在原 PCL 窗口进入 **下载 → 原版游戏**。下载位置继续使用 PCL 原设置。

ZIP 解压本身不会执行程序。普通启动无需安装、管理员权限或命令行；以后每次需要扩展时，从下载助手启动。若不使用实验性自动加载，停止使用助手即可停止接入。

```text
PCL2.exe                     你已有的原 PCL，包内不包含
PCL下载助手.exe              双击这个
PCL下载助手.exe.config
PCL-Shell-Auto/              完整保留这个文件夹
```

## 双击原 PCL 本体自动加载（实验）

助手里有 **安装自动加载（实验）** 和 **撤销自动加载** 两个按钮。此模式会备份原启动配置，添加旁置引导 DLL，并修改 `PCL2.exe.config`，不改原 EXE 字节。

**自动加载模式尚未完成实际启动和完整安装/撤销测试，不建议作为目前面向普通用户的默认方式。** 已通过的是代码编译、配置的内存合并/移除检查。此前测试副本的启动配置安装命令被执行环境拦截，未完成端到端验证。请不要把“编译通过”理解为自动加载已验证可用。

撤销前不要直接删除原 PCL 旁的 `PclShell.Bootstrap.dll`；保留启动注册却删除它可能使 PCL 启动失败。撤销按钮会先解除注册；不再使用的文件可在关闭 PCL 后清理。安装不影响已运行实例，下一次启动才生效。

## 当前适配与测试边界

仅支持官方 PCL **2.99.0.0**，SHA-256：

```text
5b64d6d540c376390470cea3ab99d47661e8bca81be2ba647ea83e7d2e9428fa
```

版本号和完整指纹都要匹配；其他文件不会被强行适配。PCL 更新后旧插件可能停用，等待新适配。不要自行添加新哈希冒充兼容。原程序更新可能覆盖引导配置，未来运行时变化也可能要求重做宿主。

外层模式已在独立、仍禁用下载的 PCL 测试构建中恢复原下载页，显示 919 个版本，且通过原安装引擎安装 `rd-132211`，客户端 JAR 校验匹配。正式版接口元数据检查通过；**尚未完成这份正式版的完整启动、所有组件组合、微软登录或真实跨版本自更新测试**。详见 [TESTING.md](TESTING.md)。

## 安全与构建来源

完整源码在 `src/`，构建脚本在 `build.ps1`。发布 ZIP 由本仓库 GitHub Actions 构建，提供构建日志、SHA-256 和 GitHub 构建来源证明。来源证明用于验证产物对应的构建流程，不是“无毒认证”；目前没有独立安全审计或签名证书。细节见 [SECURITY.md](SECURITY.md)。

有 GitHub CLI 的用户可验证发布包来源：

```powershell
gh attestation verify .\PCL2-Download-Repair.zip -R OMEGAU371/PCL2-Download-Repair
```

本扩展不提供遥测、远程脚本、自动下载执行的插件更新或账号收集功能。插件运行在 PCL 同一进程内，权限相同，不构成账号隔离。只安装自己信任的插件。

## 开发构建

Windows、.NET SDK 10、.NET Framework 4.8。在仓库根目录运行：

```powershell
.\build.ps1
```

输出 `artifacts/PCL2-Download-Repair.zip`。NuGet 使用固定版本的 `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3，仅作为构建依赖。未承诺本地与 CI 的 ZIP 逐字节一致。

外层宿主在原 PCL 目录语境中执行原入口；下载插件使用反射与 WPF 页面事件，把原下载页重新绑定到程序保留的加载器。没有复制原 PCL 源码到本仓库。实验性本体引导使用 .NET Framework `AppDomainManager`，不是官方插件接口。

## 归属

PCL 原作者龙腾猫跃：[原项目](https://github.com/Meloong-Git/PCL)。本项目独立编写的代码采用 MIT 许可；PCL、Minecraft 及各第三方组件的名称、代码和权利归各自权利人所有。
