When to read: project setup, scenes/prefabs, hierarchy, inspector, git config. Read only the needed `##` section.
Sections: Setup · Folders · Scenes and prefabs · Hierarchy · Inspector · Git

# Scenes, folders, inspector, git

## Setup
`python <skill>/scripts/install_unity.py` → `Assets/_Project/Code/Editor/` (StudioSetup, StudioAsmdefs, StudioEditorUtil, StudioConsoleBridge,
SpriteImportRules, VfxBuilder, `Templates~/` git files; keeps existing) → `python <skill>/scripts/asmdefs.py` (asmdefs now, same as the click)
→ compile → **Tools > Studio > Setup Project**.
Idempotent. Does:
- folder tree below + asmdefs `<ns>.Core/Gameplay/UI/Editor/Tests.EditMode/Tests.PlayMode` (`ns` from `.studio/brief.md` `ns:`, else company + product)
  with package refs from `Packages/manifest.json`; existing asmdefs only get missing package refs. Type → asmdef reference:
  | Type | Reference (module) |
  |---|---|
  | `InputAction`, `PlayerInput` | `Unity.InputSystem` (Gameplay, UI) |
  | `Text`, `Image`, `Button` / `TMP_Text` | `UnityEngine.UI` / `Unity.TextMeshPro` (UI, Editor) |
  | `Light2D`, URP `PixelPerfectCamera` | `Unity.RenderPipelines.Universal.2D.Runtime` + `.Universal.Runtime` + `.Core.Runtime` (Gameplay, Editor) |
  | `TestRunnerApi` | `UnityEditor.TestRunner` + `UnityEngine.TestRunner` (Editor) |
- `Scenes/Bootstrap.unity` with separators + camera at build index 0;
- Unity's default `Assets/Scenes/SampleScene.unity` → moved to `_Project/Scenes/` (deleted if empty), empty `Assets/Scenes/` removed;
- Force Text + Visible Meta Files; copies `Templates~/gitignore.txt`/`gitattributes.txt` to root if missing; logs UnityYAMLMerge config.
**Tools > Studio > Add Hierarchy Separators** for the open scene. One click beats dozens of tool calls.

## Folders
```
Assets/_Project/
  Art/{Sprites,Animations,Materials,Shaders,VFX,UI,Fonts}
  Audio/{Music,SFX,Mixers}
  Code/{Core,Gameplay,UI,Editor,Tests/EditMode,Tests/PlayMode}   # each .asmdef
  Data/<Domain>/   Prefabs/<Domain>/   Scenes/   Settings/
Assets/ThirdParty/    # store imports that can't live in Packages/
```
- Nothing of ours outside `Assets/_Project/` (check_code flags it).
- Feature folders inside Code: `Gameplay/Player`, `Gameplay/Enemies`.
- Asset names `Type_Name_Variant`: `SPR_Player_Run`, `PF_Enemy_Bat`, `SO_Wave_01`, `MAT_Dissolve`, `SFX_Jump_01`.

## Scenes and prefabs
- Bootstrap (index 0) + additive content (Menu, Level_NN, UI); architecture § Scene flow.
- Everything in levels is a prefab; nested for parts, variants for flavors (Bat → BatElite).
- Root = adapter MonoBehaviour; visuals in child `Visual` (squash/flash/flip don't touch physics).
- No hand-edited `.unity`/`.prefab` YAML beyond typos; use MCP or Editor script.

## Hierarchy
`EditorOnly`-tagged empty objects, in order: `--- MANAGERS ---` · `--- ENVIRONMENT ---` · `--- GAMEPLAY ---` · `--- UI ---` · `--- CAMERAS ---` · `--- LIGHTING ---`.
Objects are siblings below their separator, not children.

## Inspector
`[Header]` groups · `[Tooltip]` on every designer field · `[Range]`/`[Min]` · `[field: SerializeField]` auto-props ·
`OnValidate()` cross-field rules (`Debug.LogWarning(msg, this)`) · `[RequireComponent]` over null checks.
Custom editors/drawers only when default inspector truly blocks designers (in `Code/Editor`).

## Git
- `gitignore.txt` → `.gitignore` (Library, Temp, Logs, UserSettings, builds, IDE). `.studio/` tracked.
- `gitattributes.txt` → `.gitattributes`: LFS for binaries (psd, png, aseprite, wav, ogg, mp3, fbx, ttf…), YAML as text `merge=unityyamlmerge`. `git lfs install` once per machine.
- Force Text + Visible Meta Files (StudioSetup sets).
- Smart merge (StudioSetup logs exact path):
  `git config merge.unityyamlmerge.name "Unity SmartMerge"`
  `git config merge.unityyamlmerge.driver "'<UnityEditorPath>/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A"`
- Commit `.meta` with assets; never `Library/`. Optional post-commit map refresh (token-economy § c).
