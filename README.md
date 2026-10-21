# PCL2 Download Repair

第三方 PCL 插件加载器与下载页修复插件，当前为测试版。与 PCL、Mojang、Microsoft 均无隶属关系。包内不含 PCL 本体、游戏文件或用户配置。

## 安装一次，以后直接打开原 PCL

1. 从 [Releases](https://github.com/OMEGAU371/PCL2-Download-Repair/releases) 下载新版 `PCL2-Download-Repair.zip` 并完整解压到 **PCL2.exe 所在文件夹**。
2. 先关闭 PCL，再双击 **`plugins-loader\install.bat`**。
3. 出现安装成功提示后，以后照常双击原 **`PCL2.exe`**，无需每次打开辅助程序。
4. 进入原 PCL 的 **下载 → 原版游戏**。下载位置继续使用原 PCL 设置。

找不到 PCL 时会弹出文件选择框。安装不需要关闭杀毒软件、添加排除项或管理员权限；如果目录本身不可写，请使用自己有写权限的 PCL 目录。ZIP 解压本身不会执行代码，首次需要运行 `install.bat`。

请使用 **v0.3.1-preview.1 或更新版本**。v0.3.0 的安装器遗漏了浏览器下载标记的处理，可能安装成功却仍没有下载页，日志报 `0x80131515`。新版先校验包内文件哈希，再仅处理本组件已核验运行文件的 Internet 标记；不匹配时停止安装。旧版用户关闭 PCL，重新解压新版并运行 `install.bat` 修复即可。

解压包只有一个顶层文件夹：

```text
plugins-loader/
  install.bat
  uninstall.bat
  loader-setup.exe                 安装入口与管理界面
  PclShell.exe                    加载器运行模块
  PclShell.Api.dll
  PclShell.Bootstrap.dll
  plugins/
    download-repairer/
      plugin.json
      PclShell.DownloadRestore.dll
  README.md / SECURITY.md / TESTING.md / SHA256SUMS.txt
```

`install.bat` 自动把加载器安装到原 PCL 配置目录的独立子文件夹，实际运行结构为：

```text
PCL2.exe                          原程序，字节不修改
PCL2.exe.config                   启动接入配置，安装前备份
PclShell.Bootstrap.dll            小型启动引导
PCL/
  Setup.ini                       原设置文件，安装器不修改
  plugins-loader/
    install.bat                   重新接入／修复
    uninstall.bat                 卸载
    loader-setup.exe
    PclShell.exe / PclShell.Api.dll / ...
    plugins/
      download-repairer/
    shell.json                    加载器设置
    autoload-state.json           原启动配置的本地备份
    logs/
```

安装成功后，最初解压出来的安装包目录可以移走；保留已安装的 `PCL\plugins-loader`。也可打开其中 `loader-setup.exe` 管理插件或通过外层方式启动。

## 卸载

关闭 PCL，双击 **`PCL\plugins-loader\uninstall.bat`** 并确认即可，原解压包中的 `uninstall.bat` 也可使用。

卸载会先恢复启动配置，再删除已安装的加载器和插件文件夹；原 PCL、游戏目录和其他 PCL 设置保留。最初没有 `.exe.config` 时，卸载后恢复为没有该文件。如果启动配置在安装后有改动，则保留后续改动并移除本项目注册。自有且未被其他配置共享的根目录引导 DLL 也会清理。

不要先手动删除根目录引导 DLL 却保留启动注册，否则 PCL 可能无法启动。管理界面的“撤销自动加载”只关闭接入、保留加载器文件；彻底卸载请用 `uninstall.bat`。

## 更新与隔离

加载器在 `PCL\plugins-loader`，各插件在自己的子文件夹；PCL 主程序、加载器和下载适配器分别保存。下载插件的内部 ID 仍为 `download-restore`，以保留已有设置；所在文件夹改为 `download-repairer`。

- PCL EXE 更新不会要求重新编译整个加载器。每次启动重新计算文件指纹，未适配的下载插件自动跳过，保留原版行为。
- 如果更新覆盖或删除 `.exe.config`，再次运行已安装目录的 `install.bat` 修复接入。修复会以更新后的配置为撤销基线，避免卸载时覆盖新版配置。
- 若 PCL 改用其他运行时或改变启动机制，加载器本身也可能需要适配。尚未跨越真实官方版本升级测试，不承诺所有更新都完全无感。
- 从旧 `PCL-Shell-Auto` 自动加载版本升级时，先用旧版撤销接入，再安装新版；不强行覆盖没有对应备份的旧引导。

## 当前下载插件适配

仅支持官方 PCL **2.99.0.0**，SHA-256：

```text
5b64d6d540c376390470cea3ab99d47661e8bca81be2ba647ea83e7d2e9428fa
```

加载器和插件兼容性分开判断：加载器可接入所支持的 .NET Framework PCL，但下载插件只有匹配上述版本与指纹才启用。不要自行加入新哈希冒充适配。

2026-10-21 已在该官方 EXE 的隔离副本上通过安装脚本接入，**直接启动 PCL2.exe 本体**并确认原下载页显示版本列表，原 EXE 指纹不变。独立测试构建同样通过本体自动加载，显示 920 个版本；安装、配置被替换后的修复、未知指纹停用和脚本卸载已测试。详见 [TESTING.md](TESTING.md)。

## 源码、安全与构建

源码在 `src/`，MIT 许可。Windows 上安装 .NET SDK 10 后运行 `build.ps1`，输出 `artifacts/PCL2-Download-Repair.zip`。运行需要 .NET Framework 4.8。构建依赖固定为 `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3。

发布 ZIP 由 GitHub Actions 构建，提供日志、哈希和构建来源证明；它们不是“无毒认证”。目前没有独立安全审计或代码签名。插件和 PCL 同进程、同权限，不构成恶意插件沙箱或账号隔离。细节见 [SECURITY.md](SECURITY.md)。

```powershell
gh attestation verify .\PCL2-Download-Repair.zip -R OMEGAU371/PCL2-Download-Repair
```

原 PCL 作者龙腾猫跃：[原项目](https://github.com/Meloong-Git/PCL)。本仓库未复制原 PCL 源码，扩展通过 .NET Framework 启动引导、反射和 WPF 页面事件接入。
