# EasyDisk

> 用 WinUI 3 写的 Windows 磁盘分析 + 秒搜工具。  
> 扫盘、看谁占空间、建索引、秒级搜文件，界面跟吃了德芙一样丝滑。

![Platform](https://img.shields.io/badge/Platform-Windows%2010%201809%2B%20/%20Windows%2011-blue)
![Framework](https://img.shields.io/badge/Framework-.NET%208%20%2B%20WinUI%203-8A2BE2)
![License](https://img.shields.io/badge/License-MIT-green)

---

## ✨ 功能

- 📊 **磁盘分析**：扫描任意盘符 / 文件夹，按大小排序，一眼看懂空间被谁吃了
- ⚡ **秒搜**：建立索引后，文件名搜索基本即时返回
- 🎨 **WinUI 3 原生界面**：Mica 背景、圆角、深色模式、动画顺滑
- 🧵 **多线程遍历**：扫描时不卡 UI（正在打磨稳定性）
- 🧹 **大文件 / 缓存定位**：快速找到 PS、Adobe、NuGet、VS Temp 占空间的东西
- 📁 **只读分析**：默认不删文件，安全

---

## 🚀 快速使用（普通用户）

框架依赖版（体积最小）
先装：
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/deploying/project-reunion-deploy-runtime)

再运行 `EasyDisk.exe`。

---
