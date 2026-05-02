# Unity Hierarchy Level Organizer

Small **Unity Editor** script that groups messy root-level (and loose `Environment`) objects into empty GameObject “folders” under **`Environment`**:

`Terrain` · `Buildings` · `Roads` · `Fences` · `Nature` · `Props`

Name matching is prefix/rule-based (tile layers, houses, roads, fences, trees, props, etc.). **Undo** is supported.

**Requirements:** Unity 2021.3+ (uses `Undo.SetTransformParent`; adjust if you target older editors).

## Install

Copy the folder:

`Assets/Editor/HierarchyLevelOrganizer.cs`

into your Unity project (keep the `Editor` folder so it only runs in the Editor).

## Use

1. Open your scene.
2. Menu: **Tools → Hierarchy → Organize Level Under Environment**

If no `Environment` object exists at the scene root, one is created. Matching objects are parented under `Environment/<Category>`.

## Customize

Edit `HierarchyLevelOrganizer.cs`:

- **`SkipRoots`** — root objects that must never be moved (add your player root, managers, etc.).
- **`Categorize`** — add or change string rules for your asset naming.

## License

MIT — see [LICENSE](LICENSE).

---

## Deutsch

Skript für die **Hierarchy**: sortiert lose Objekte unter **`Environment`** in Unterordner (**Terrain**, **Buildings**, …). Menü: **Tools → Hierarchy → Organize Level Under Environment**. Anpassungen in `SkipRoots` und `Categorize`.
