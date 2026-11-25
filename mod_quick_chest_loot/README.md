# 开箱自动拾取

适用于《开拓物语》Windows 版 2.2.0（Steam 构建 26889260，模组 SDK v1）。

打开野外战利品箱时直接显示箱内物品，跳过扫灰和逐格揭晓。每次开箱后等待 0.7 秒再拾取第一格，之后每隔 0.35 秒取一格。能合并的物品会按原版规则堆叠，超出的数量留在箱里。箱子空了、剩余物品都放不下或关箱时，自动拾取停止。

## 安装

把 `quick-chest-loot` 文件夹放进游戏的 `FrontierTale_Data/Mods`，确认文件夹内直接包含 `manifest.json`、`preview.png` 和 `QuickChestLoot.dll`。在主菜单的「模组 → 本地模组」启用「开箱自动拾取」，然后重启游戏。

## 编译

在仓库根目录执行以下命令，把 `GameDir` 替换为游戏安装目录。编译会更新模组目录里的 `quick-chest-loot/QuickChestLoot.dll`。

```powershell
dotnet build mod_quick_chest_loot/Source/QuickChestLoot.csproj -c Release '-p:GameDir=Z:/SteamLibrary/steamapps/common/FrontierTale'
```

[通用模组开发说明](../docs/modding.md)
