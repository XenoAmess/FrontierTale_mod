# 强力追随者 超天才魔法少女 白绮

白绮（Vivhite）是独立随从，性格 INTJ。启用模组并进入领地后，可在原版村民面板以 0 金币招募，再设为跟随。与她交谈时会显示台词「……麻烦死了」。

- 基础战斗属性沿用米娅的数据，装备加成由原版随从系统计算。
- 招募时装备栏是空的。给白绮装备武器、防具或饰品，操作与原版装备栏一致。没有可用武器时，她会跟随但不会用默认武器攻击。
- 白绮持有的弓、弩和枪使用无限弹药。法杖沿用原有的魔力消耗。
- 白绮存活并跟随时，主角受到致命伤害，她会牺牲自己，将剩余生命交给主角，回血受主角最大生命值限制。白绮通过原版随从复活流程恢复。

## 安装

适用于《开拓物语》Windows Mono 2.2.0（Steam 构建 26889260）。把 `vivhite` 放到 `FrontierTale_Data/Mods`，在主菜单的「模组 → 本地模组」启用后重启游戏。保存过白绮的存档需要启用本模组加载角色及其装备。

## 构建

```powershell
dotnet build mod_vivhite/Source/Vivhite.csproj -c Release '-p:GameDir=Z:/SteamLibrary/steamapps/common/FrontierTale'
```

构建把入口 DLL 复制到 `vivhite/`。入口 DLL 内嵌 Harmony 2.4.3 及其 MIT 许可。

## 美术素材

使用内置 imagegen，以白绮的角色参考图、Delta Live2D 和原版角色美术为参考生成对话立绘和正、背面地图头部贴图。头像和预览图从立绘裁切。地图角色沿用原版 Spine 骨骼和动作，装备后会使用对应的原版换装外观。所有运行时素材在 [`vivhite/`](vivhite/) 内。

<details>
<summary>ImageGen 提示词</summary>

对话立绘

```
Use case: stylized-concept. Asset type: finished Frontier Tale custom follower dialogue portrait, from which its face icon and Workshop thumbnail will also be cropped. Create one wholly new character illustration of Vivhite 白绮. Image 1 is the art-direction reference only: the bundled Frontier Tale dialogue portraits of four character poses. Images 2 and 3 are the original user-provided character design references (painted crop and Live2D reference); preserve their identity. Match the game's very particular storybook super-deformed dialogue art: an oversized rounded head nearly half the entire character height, big very simple saturated eyes, small pointed chin, compact shoulders and shortened body, heavy clean deep-indigo outer contour, tighter thinner inner lines, bold mobile-readable contours and broad cel-shadow shapes, crisp creamy highlights, two tones per large shape. White-silver icy-blue short angular layered bob, prominent asymmetric center bangs, two black hair ribbons edged with understated gold like the Live2D, clear round thin warm-brass spectacles over magenta irises, tiny and slightly annoyed mouth, cool observant quiet expression as if saying “what a bother.” Dress her in her own closed charcoal-navy frontier mage short cape with simple muted gold trim and an ivory stand collar, restrained angular decorative pattern on cape trim. One hand touching temple of spectacles and other relaxed at side, hands empty. Chest facing nearly forward, slight three-quarter angle, framing from crown through mid-thigh like the stock references. Place single whole character cutout centered, with at least 7 percent empty outer margin to include both ribbons and cape sides. Make the body end cleanly at the bottom in the game's portrait cutout convention. Real transparent alpha background, clean silhouette; no background, scenery, floor shadow, props, magic glow, hats, text, watermarks, or frame. Readable as 128 px. Use heavier outlines and simpler block shading than the source Live2D. Preserve all build-friendly flat-color edges.
```

地图正面

```
Use case: stylized-concept. Asset type: isolated FACE-SIDE head sprite for the Frontier Tale shared Spine game rig. Create a game-ready isolated head cutout of Vivhite, the same character as image 3, a finalized dialogue portrait. Image 1 shows actual stock Frontier Tale game-rig head texture shapes and their strong clean flat-vector outlines and graphic shading: reference the graphic style only. Image 2 is the character design/live rig reference: preserve her recognizable icy-silver blue bob with a heavy angular central fringe and two big dark navy hair ribbons trimmed muted gold, vivid magenta eyes, warm brass round glasses. Match the same quiet mildly annoyed intelligent expression in image 3. Show only the compact rounded oversized chibi head from its crown down to the chin, directly facing toward the screen with very slight three-quarter asymmetry, essentially the same near-front view as image 2. All hair ends near the chin. Both ribbons fully visible and tucked behind the temples. Use a bold charcoal exterior outline, very limited 2 to 3 blocks of cold blue shadow, one creamy swoosh of hair highlight, clearly readable magenta eyes and circular glasses at 75 px in-game. No photographic textures, no gradients. No neck, no clothing, no hand, no shoulders, no torso, no props, no detached sprite pieces, no badge frame or text. Top, ribbons, and chin all uncut, leave an even clear margin around the single piece. Real transparent background and alpha.
```

地图背面

```
Use case: stylized-concept. Asset type: isolated BACK-SIDE head sprite for the Frontier Tale shared Spine game rig. This sheet must pair exactly with reference image 1, the approved Vivhite front head. Imagine rotating that head a full 180 degrees to show the back of the exact same head; it is viewed straight from behind with crown up. Match the same proportions, outer outline, overall width:height and hair length, enough to replace the face-side game sprite on turn. Keep both gold-trimmed black/navy ribbons at the same temple height and size, and the icy silver blue compact angular bob. Show the hair seen from the back with the part at crown, tapered layers, a bottom full mass of hair, and strong clean dark charcoal contour. Only the back of the head is visible, with no sliver of face, no eyes, no glasses, no skin, no ears. Match asset style: striking deep charcoal outlines, graphic cel color blocks and a pale icy blue-white highlight. No neck, torso, collar, props, lettering, decorative background, or drop shadow. Use truly transparent background. Only one single head silhouette, with the crown and both ribbons entirely within canvas and an even clear margin; overall aligned centered exactly like the front view.
```

</details>
