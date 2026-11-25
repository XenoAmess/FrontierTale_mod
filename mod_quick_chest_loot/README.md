# 开箱自动拾取

适用于《开拓物语》Windows 版 2.2.0（Steam 构建 26889260，模组 SDK v1）。

打开上锁的野外战利品箱时自动开锁，跳过开锁小游戏、扫灰和逐格揭晓。每次开箱后等待 0.7 秒再拾取第一格，之后每隔 0.35 秒取一格，箱子取空后自动关闭。能合并的物品会按原版规则堆叠。背包放不下剩余物品时，自动拾取停止，箱子保持打开。

## 安装

把 `quick-chest-loot` 文件夹放进游戏的 `FrontierTale_Data/Mods`，确认文件夹内直接包含 `manifest.json`、`preview.png` 和 `QuickChestLoot.dll`。在主菜单的「模组 → 本地模组」启用「开箱自动拾取」，然后重启游戏。

## 编译

在仓库根目录执行以下命令，把 `GameDir` 替换为游戏安装目录。编译会更新模组目录里的 `quick-chest-loot/QuickChestLoot.dll`。

```powershell
dotnet build mod_quick_chest_loot/Source/QuickChestLoot.csproj -c Release '-p:GameDir=Z:/SteamLibrary/steamapps/common/FrontierTale'
```

[通用模组开发说明](../docs/modding.md)
