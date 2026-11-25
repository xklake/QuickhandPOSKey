# Quickhand POS LicenseKeyGenerator

独立的厂商授权工具，使用 .NET Framework 4.8。可单独打开、编译和运行，不需要先打开 QuickhandPOS solution。

## 在 Visual Studio 中使用

1. 打开本目录的 **LicenseKeyGenerator.sln**。
2. 安装 Visual Studio 的“.NET 桌面开发”组件及 .NET Framework 4.8 targeting pack；允许还原 NuGet 包。
3. 将 **LicenseKeyGenerator** 设为启动项目，选择 Debug 或 Release，点击启动。
4. 发布给内部操作人员时，复制 `LicenseKeyGenerator/bin/Release` **整个目录**。不要只复制 EXE。

已编译程序为 `LicenseKeyGenerator/bin/Release/LicenseKeyGenerator.exe`。

## 安装包

首次独立安装版为 **1.0.0.0**，替代从旧工程带来的历史文件版本号。安装包版本随主程序的 `AssemblyFileVersion` 更新。

- 制作安装包：双击根目录的 **Build installer.cmd**，自动编译 Release、运行检查并调用 Inno Setup；完成后打开 `Output` 文件夹。不需要手动输入命令。
- 已安装 Inno Setup 的开发人员也可以在 Release 编译完成后打开 `Installer/LicenseKeyGenerator.iss`，直接编译。建议使用一键脚本，避免误打包旧的 Release 文件。
- 输出文件为 `Output/LicenseKeyGenerator-Setup-版本号.exe`，同时生成 SHA-256 校验值及文件清单。构建使用独立目录，不覆盖当前正在运行的开发版程序。
- 运行安装包后，会为当前 Windows 用户安装程序并创建开始菜单入口；桌面快捷方式默认勾选。目标电脑不需要 Visual Studio、Inno Setup、POS 或 MySQL，只需要 Windows 10/11 和 .NET Framework 4.8 或兼容更新版本。
- 后续关闭授权工具并运行新版安装包即可覆盖升级。使用固定 AppId 和安装范围，保留原安装目录及用户设置；不允许用旧版本覆盖新版本。这里是手动安装包升级，未增加联网自动更新。
- 安装包按明确文件名单收录主程序、旧机器码工具、两份 DLL 和使用说明，不收录私钥、密码、客户授权、签发记录、测试图片或调试符号。安装及卸载都不删除 `%LocalAppData%\QuickhandPOS\LicenseIssuer` 中的原有配置。

安装后的操作说明位于开始菜单 **Quickhand POS LicenseKeyGenerator → Instructions**。旧授权的生成按钮现在弹出“另存为”，请将客户文件保存到专门的客户资料目录。

未配置 Windows 代码签名证书，因此当前安装包没有 Authenticode 签名。此签名与授权 PFX 的用途不同，不要使用授权私钥给安装包签名。

开发人员可运行 `Scripts/Test-Installer.ps1 -BinaryDirectory <本次打包编译目录>` 验证安装、覆盖升级、防降级及卸载；测试使用单独 AppId、不创建快捷方式或卸载注册项，只向 `TestResults` 的独立目录安装。

## 首次打开

在 **First-time setup** 中点击 **Use existing key...**，选择原来的 PFX 并输入密码。已有配置通常会自动读取。本次拆分不需要重新创建密钥。

日常使用 **Generate license**：加载客户请求，核对客户及功能，输入密钥密码，确认购买权益后签发。**Legacy...** 继续支持旧格式客户授权。

POS 源码目录只用于配置公钥和构建 POS 安装包；日常签发不需要这个目录。请阅读 [分离说明](docs/分离说明.md) 了解两个仓库的关系及兼容性测试方式。

## 开发验证

可在界面编译，也可使用：

```powershell
.\Scripts\Build.ps1 -Configuration Release -RunTests
```

测试使用临时模拟密钥、客户资料和输出文件，不访问客户授权或营业数据库。测试截图在 `TestResults` 中。

私钥、签发历史和客户授权必须存放在 Git 仓库外，并另行妥善备份。`.gitignore` 也排除了常见密钥、客户文件和编译输出。
