# Unity Hierarchy Level Organizer

Small **Unity Editor** utility that:

1. **Sorts** loose level objects under an **`Environment`** root into empty GameObject “folders”:
   `Terrain` · `Buildings` · `Roads` · `Fences` · `Nature` · `Props` (rules in `Categorize`, easy to change).
2. **Groups duplicate names** – siblings like `Tree_0`, `Tree_0 (1)`, `Tree_0 (2)` (and `… (Clone)`) are parented under a shared empty object: **`BaseName · Group`**.

**Undo** is fully supported. **Requirements:** Unity 2021.3+ (uses `Undo.SetTransformParent`).

## Install

Copy:

`Assets/Editor/HierarchyLevelOrganizer.cs`

into your project (keep it under an `Editor` folder).

## Menus (Tools → Hierarchy)

| Command | What it does |
|--------|----------------|
| **Organize Level Under Environment** | (1) Groups duplicate names at the **scene root** (optional pass), (2) moves objects into `Environment/<Category>`, (3) groups duplicates again **inside** `Environment` and each category (several passes, max 16, for nested copies). |
| **Group Duplicate Siblings In Scene** | Only step (1) + full tree under **Environment** – no category moves. Use if you only want `… · Group` folders. |

## Tuning (other games / assets)

At the top of the script:

- **`SkipRoots`** – never move or group these object **names** (add your player rig, `GameManager`, etc.).
- **`FolderNames` / `Categorize(...)`** – change prefixes and rules to match your naming.  
  Group folders like `foo · Group` are recognized: categorization uses the name **without** ` · Group` and without ` (n)`.
- **`GroupSuffix`**, **`EnvironmentRootName`** – constants if you need different labels.

## License

MIT — see [LICENSE](LICENSE).

---

## Deutsch

- **Level unter Environment ordnen** – Kategorien + Duplikate (Root + unter `Environment`).
- **Nur doppelte Geschwister gruppieren** – nur `Name`/`Name (1)`/… unter `… · Group`, **ohne** in Terrain/Buildings/… zu sortieren.
- Anpassung: `SkipRoots`, `Categorize`, `GroupSuffix` in `HierarchyLevelOrganizer.cs`.
