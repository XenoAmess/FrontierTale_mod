# 强力追随者 超天才魔法少女 白绮

白绮（Vivhite）是独立随从，性格 INTJ。启用模组并进入领地后，可在原版村民面板以 0 金币招募，再设为跟随。与她交谈时会显示台词「……麻烦死了」。

- 基础战斗属性沿用米娅的数据，装备加成由原版随从系统计算。
- 招募时装备栏是空的。给白绮装备武器、防具或饰品，操作与原版装备栏一致。没有可用武器时，她会跟随但不会用默认武器攻击。
- 白绮使用弓、弩、枪时无需准备弹药。使用暗夜法杖等施法武器时，攻击仍会扣白绮的魔力值。
- 白绮存活并跟随时，主角受到致命伤害，她会牺牲自己，将剩余生命交给主角，回血受主角最大生命值限制。白绮通过原版随从复活流程恢复。

## 安装

适用于《开拓物语》Windows Mono 2.2.0（Steam 构建 26889260）。把 `vivhite` 放到 `FrontierTale_Data/Mods`，在主菜单的「模组 → 本地模组」启用后重启游戏。更新时覆盖同一个目录并重启。旧存档可以直接继续。读档时如果出现模组差异页面，确认白绮已加载，然后点击「继续」进入领地。在顶部「角色」列表找到白绮，点击底部按钮，0 金币招募后再点击「跟随」。保存过白绮的存档需要启用本模组加载她及其装备。

## 给白绮装备

1. 让白绮成为「当前随从」。
2. 打开顶部的「仓库」。在背包区点击主角装备页签旁边的随从装备按钮，切到她的八格装备栏。
3. 把背包或仓库里的装备拖进对应的武器、衣服、鞋子、手套或饰品槽。取下时拖回背包。

## 构建

```powershell
dotnet build mod_vivhite/Source/Vivhite.csproj -c Release '-p:GameDir=Z:/SteamLibrary/steamapps/common/FrontierTale'
```

构建把入口 DLL 复制到 `vivhite/`。入口 DLL 内嵌 Harmony 2.4.3 及其 MIT 许可。

## 美术素材

使用内置 imagegen，以白绮的角色参考图、Delta Live2D 和原版角色美术为参考生成对话立绘和正、背面地图头部贴图。头像按原版角色方格的近景比例从立绘裁切。地图贴图按实测的原版地图角色比例重画，并沿用原版 Spine 骨骼、动作和装备外观。空装备栏时显示斗篷、缠手和靴子作为外观。所有运行时素材在 [`vivhite/`](vivhite/) 内。

<details>
<summary>ImageGen 提示词</summary>

对话立绘

```
Use case: stylized-concept. Asset type: finished Frontier Tale custom follower dialogue portrait, from which its face icon and Workshop thumbnail will also be cropped. Create one wholly new character illustration of Vivhite 白绮. Image 1 is the art-direction reference only: the bundled Frontier Tale dialogue portraits of four character poses. Images 2 and 3 are the original user-provided character design references (painted crop and Live2D reference); preserve their identity. Match the game's very particular storybook super-deformed dialogue art: an oversized rounded head nearly half the entire character height, big very simple saturated eyes, small pointed chin, compact shoulders and shortened body, heavy clean deep-indigo outer contour, tighter thinner inner lines, bold mobile-readable contours and broad cel-shadow shapes, crisp creamy highlights, two tones per large shape. White-silver icy-blue short angular layered bob, prominent asymmetric center bangs, two black hair ribbons edged with understated gold like the Live2D, clear round thin warm-brass spectacles over magenta irises, tiny and slightly annoyed mouth, cool observant quiet expression as if saying “what a bother.” Dress her in her own closed charcoal-navy frontier mage short cape with simple muted gold trim and an ivory stand collar, restrained angular decorative pattern on cape trim. One hand touching temple of spectacles and other relaxed at side, hands empty. Chest facing nearly forward, slight three-quarter angle, framing from crown through mid-thigh like the stock references. Place single whole character cutout centered, with at least 7 percent empty outer margin to include both ribbons and cape sides. Make the body end cleanly at the bottom in the game's portrait cutout convention. Real transparent alpha background, clean silhouette; no background, scenery, floor shadow, props, magic glow, hats, text, watermarks, or frame. Readable as 128 px. Use heavier outlines and simpler block shading than the source Live2D. Preserve all build-friendly flat-color edges.
```

地图正面

~~~~
Use case: stylized-concept. Asset type: the isolated FRONT HEAD texture of Vivhite for Frontier Tale's gameplay sprite. Reference 1 is actual front and back views of the game's other map characters; especially match the red-haired character's compact head silhouette, oval face, very thick dark exterior ink, simple two-tone hard block colors and sparse interior detail. Reference 2 is Vivhite's original Live2D identity. Reference 3 is her new dialogue portrait, for identity and colors only. Make an entirely new gameplay head, straight-on with crown fully visible, explicitly more compact than the dialogue art. Tiny stylized light-silver short tapered bob, wider near the cheeks and rounded jaw. Choppy fringe cut above the large pink eyes so they read as one unit. Simple small round muted bronze spectacles integrated over the pink eyes, nearly black arcing top lash, simple single iris highlight, pale warm cream face. Keep her two tiny black-gold ribbon knots tucked into the hair profile just above the ears, protruding on each side by at most 4 percent of total head width. Use simplified large shapes and recognizable angry-neutral face. At game scale the art should look like the Frontier Tale reference and be clear at 70 pixels high. Use a thick soft-cornered charcoal outer line, thick but clean brow and hair edge, limited mid-gray-lavender cel-shadow under bangs, pale ivory highlight. Hide no part of her face below the glasses; leave chin distinctly defined and enough room for one extremely small mouth if necessary. Only crown, hair, face, and little ribbons; no neck, no shoulders, hands, clothing, props, text, or outline glow. Center one head on a square canvas with generous transparent margins, true alpha background. The cropped head silhouette should be close to round, around 1.1 width to 1 height.
~~~~

地图背面

~~~~
Use case: stylized-concept. Asset type: the isolated BACK HEAD texture for the same gameplay sprite. The first reference is the finalized new FRONT sprite of Vivhite; redraw that exact sprite from the rear, rotated 180 degrees around a vertical axis. The second reference shows the rear texture line and shape treatment of the existing Frontier Tale game actors. Need the same squat compact helmet-like icy silver bob, the same crown notch, the same exact overall dimensions and silhouette, including two small black cloth knots with gold edges attached flush to her hair. No face, skin, ears, neck, clothing or hands; only the silhouette of the back of hair and ribbons. Use the same very thick simplified charcoal contour, main pale cream-silver hair area, cold lavender tidy tapering bottom shadows, a short broad highlight broken by a couple of tufts, a small number of thick interior lines. Crop frame centered with ample transparent border and true alpha. No shadows outside the shape, no extras, no labels. Should pair seamlessly with the front asset when a game sprite turns.
~~~~

</details>
