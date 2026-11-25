# Quickhand POS LicenseKeyGenerator

独立的厂商授权工具，使用 .NET Framework 4.8。可单独打开、编译和运行，不需要先打开 QuickhandPOS solution。

## 在 Visual Studio 中使用

1. 打开本目录的 **LicenseKeyGenerator.sln**。
2. 安装 Visual Studio 的“.NET 桌面开发”组件及 .NET Framework 4.8 targeting pack；允许还原 NuGet 包。
3. 将 **LicenseKeyGenerator** 设为启动项目，选择 Debug 或 Release，点击启动。
4. 发布给内部操作人员时，复制 `LicenseKeyGenerator/bin/Release` **整个目录**。不要只复制 EXE。

已编译程序为 `LicenseKeyGenerator/bin/Release/LicenseKeyGenerator.exe`。

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
