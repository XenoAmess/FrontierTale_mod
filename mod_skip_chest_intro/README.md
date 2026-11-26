# 开箱跳过开锁和扫灰

跳过野外战利品箱的开锁小游戏和扫灰动画，立即显示全部物品。物品由玩家手动拾取，取空后仍可自己决定何时关箱。

## 安装

适用于《开拓物语》Windows 版 2.2.0（Steam 构建 26889260，模组 SDK v1）。把 `skip-chest-intro` 文件夹放进游戏的 `FrontierTale_Data/Mods`。在主菜单的「模组 → 本地模组」启用「开箱跳过开锁和扫灰」，停用「开箱自动拾取」，然后重启游戏。

## 编译

```powershell
dotnet build mod_skip_chest_intro/Source/SkipChestIntro.csproj -c Release '-p:GameDir=Z:/SteamLibrary/steamapps/common/FrontierTale'
```

构建会更新 `skip-chest-intro/SkipChestIntro.dll`。[通用模组开发与安装说明](../docs/modding.md)
