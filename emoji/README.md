# emoji 图标

本目录存放面板小图标：从 **Twemoji** 图标集中挑选的 39 个 PNG。
文件名即 Unicode 码点，例如 `1fac0.png` = U+1FAC0 🫀（解剖心脏）、`1fa7a.png` = U+1F97A 🩺（听诊器）。

## 授权与署名

- 来源：Twemoji —— <https://github.com/jdecked/twemoji>
- 图形授权：**CC-BY 4.0**（代码部分为 MIT）
- 按 CC-BY 4.0 要求，分发本插件（含这些图片）时需保留上述署名。
  插件「关于」窗口内已标注该署名。

## 构建方式

编译时这些图片会以逻辑名 `CUStatsHud.emoji.<码点>` 嵌入 DLL
（见 `CU-Stars-Hub.csproj` 的 `EmbeddedResource` 项），
运行期由 `UI/EmojiIcons.cs` 通过 `Assembly.GetManifestResourceStream` + `Texture2D.LoadImage` 读取。
因此插件仍然是**单文件分发**，不需要额外的图片目录，也不会被游戏资源卸载影响。

## 图标对应关系

码点 → 用途的映射集中写在 `UI/IconProvider.cs` 的 `KindToEmoji` 表里，
`Core/BodyReader.cs` 只填语义化的种类名（如 `blood`、`oxygen`、`thermo`）。
新增图标时：把 PNG 放进本目录，再在 `KindToEmoji` 里登记即可；
未登记的种类的会自动回退到程序化几何图标（`UI/IconFactory.cs`）。
