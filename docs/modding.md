# 《开拓物语》模组开发笔记

## 目录结构

每个模组使用独立的 `mod_<名称>/`。模组目录内的 `Source/` 存放 C# 项目，可安装文件放在含有 `manifest.json` 的独立子目录。该模组自己的安装、构建和兼容版本写在同级 `README.md`。

## SDK v1 和本地安装

Windows Mono 版自带 `FrontierTale_Data/Managed/FrontierTale.ModSDK.dll`。C# 模组通过 `GameMod` 实现生命周期。构建时引用游戏安装目录内的程序集。发布包包含 UTF-8 清单、模组入口 DLL 和 `preview.png`。封面需要小于 1 MiB，宽高分别不超过 2048 像素。

将含 `manifest.json` 的目录复制到 `FrontierTale_Data/Mods`，在游戏主菜单的「模组 → 本地模组」启用，退出并重启游戏。首次使用时，游戏会显示模组使用说明。启动日志在 `%USERPROFILE%\AppData\LocalLow\APOPHIS\FrontierTale\Player.log`。[官方 Steam 公告](https://steamcommunity.com/games/4829780/announcements/detail/698781869448103244) · [官方开发套件](https://github.com/aholic11122/FrontierTale-Modding)

## 查证游戏接口

用 REA 的 `inspect-managed-members` 对当前安装的程序集按类型查验签名、字段和 CIL 调用关系。模组依赖游戏内部成员时，升级游戏后应重新检查这些绑定并验证对应玩法。可将相关签名和最小源码交由本地模型复核，再用程序集元数据和行为测试核实结论。[REA](https://github.com/morluto/rea) · [本地模型调用文档](https://github.com/XenoAmess/local-model-operations)
