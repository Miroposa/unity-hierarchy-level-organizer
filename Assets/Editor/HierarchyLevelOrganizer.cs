#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Groups level objects under an <see cref="EnvironmentRootName"/> parent into category folders,
/// and optionally collapses Unity duplicate names (Foo, Foo (1), …) under a shared parent.
/// </summary>
public static class HierarchyLevelOrganizer
{
    /// <summary>Suffix for empty parents that hold duplicates. Change if it clashes with your assets.</summary>
    public const string GroupSuffix = " · Group";

    /// <summary>Name of the root folder GameObject for level art.</summary>
    public const string EnvironmentRootName = "Environment";

    // ── Tune for your project: roots that are never moved or grouped ─────────────────
    static readonly HashSet<string> SkipRoots = new HashSet<string>(StringComparer.Ordinal)
    {
        "Main Camera", "Virtual Camera", "Canvas", "Lights", "EventSystem",
        "LevelManager", "Prefabs", "DontDestroyOnLoad",
        EnvironmentRootName,
    };

    static readonly string[] FolderNames = { "Terrain", "Buildings", "Roads", "Fences", "Nature", "Props" };

    static readonly Regex DuplicateIndexSuffix = new Regex(@"^(.+?)\s+\(\d+\)\s*$", RegexOptions.Compiled);

    [MenuItem("Tools/Hierarchy/Organize Level Under Environment")]
    static void OrganizeLevelUnderEnvironment()
    {
        RunPipeline(groupDuplicatesAtRootsFirst: true, groupDuplicatesInCategories: true);
    }

    /// <summary>
    /// Only merges sibling objects that share the same base name (Foo / Foo (1) / …).
    /// Does not move objects into Environment categories.
    /// </summary>
    [MenuItem("Tools/Hierarchy/Group Duplicate Siblings In Scene")]
    static void GroupDuplicatesOnlyMenu()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Hierarchy", "No valid scene is loaded.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        var skip = BuildSkipSetForGrouping(null);
        int n = GroupDuplicatesAmongSceneRoots(scene, skip);
        var envTf = FindEnvironmentTransform(scene);
        if (envTf != null)
            n += GroupDuplicatesUnderEnvironmentTree(envTf, skip);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[HierarchyLevelOrganizer] Duplicate grouping: created {n} group folder(s).");
    }

    static void RunPipeline(bool groupDuplicatesAtRootsFirst, bool groupDuplicatesInCategories)
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Hierarchy", "No valid scene is loaded.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        var folders = new Dictionary<string, Transform>(StringComparer.Ordinal);
        var env = FindOrCreateEnvironmentRoot(scene);
        Undo.RegisterCreatedObjectUndo(env.gameObject, "Create Environment root");

        foreach (var name in FolderNames)
        {
            var t = FindOrCreateChildFolder(env, name);
            folders[name] = t;
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Create folder " + name);
        }

        var skipGrouping = BuildSkipSetForGrouping(folders);

        if (groupDuplicatesAtRootsFirst)
            GroupDuplicatesAmongSceneRoots(scene, skipGrouping);

        int moved = 0;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (SkipRoots.Contains(go.name))
                continue;
            if (IsOrganizerFolder(go.transform, folders))
                continue;

            var cat = Categorize(go.name);
            if (cat == null)
                continue;

            Undo.SetTransformParent(go.transform, folders[cat], "Organize hierarchy");
            moved++;
        }

        moved += OrganizeDirectChildrenOfEnvironment(env, folders);

        if (groupDuplicatesInCategories)
            GroupDuplicatesUnderEnvironmentTree(env, skipGrouping);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[HierarchyLevelOrganizer] Done: moved {moved} object(s) under {EnvironmentRootName}.");
    }

    static HashSet<string> BuildSkipSetForGrouping(Dictionary<string, Transform> folders)
    {
        var skip = new HashSet<string>(SkipRoots, StringComparer.Ordinal);
        if (folders != null)
        {
            foreach (var n in FolderNames)
                skip.Add(n);
        }
        return skip;
    }

    /// <summary>Base name for grouping: strips <c> (n)</c> and <c> (Clone)</c>.</summary>
    public static string GetBaseDuplicateName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return objectName;

        if (objectName.EndsWith(GroupSuffix, StringComparison.Ordinal))
            return objectName.Substring(0, objectName.Length - GroupSuffix.Length).TrimEnd();

        var m = DuplicateIndexSuffix.Match(objectName);
        if (m.Success)
            return m.Groups[1].Value;

        if (objectName.EndsWith(" (Clone)", StringComparison.Ordinal))
            return objectName.Substring(0, objectName.Length - " (Clone)".Length);

        return objectName;
    }

    static string GetNameForCategorization(string objectName)
    {
        var n = objectName ?? "";
        if (n.EndsWith(GroupSuffix, StringComparison.Ordinal))
            n = n.Substring(0, n.Length - GroupSuffix.Length).TrimEnd();
        return GetBaseDuplicateName(n);
    }

    static int GroupDuplicatesAmongSceneRoots(Scene scene, HashSet<string> skip)
    {
        int total = 0;
        int passTotal;
        int guard = 0;
        do
        {
            passTotal = 0;
            var roots = scene.GetRootGameObjects();
            var list = new List<Transform>();
            foreach (var go in roots)
            {
                if (go.transform.parent != null)
                    continue;
                list.Add(go.transform);
            }
            passTotal += GroupDuplicateSiblingsUnderParent(list, null, scene, skip);
            total += passTotal;
            guard++;
        } while (passTotal > 0 && guard < 16);

        return total;
    }

    /// <summary>
    /// For every transform under <paramref name="environmentRoot"/>, merges sibling duplicates.
    /// Repeats until no new groups (max 16) so nested copies under a <c>… · Group</c> are also tidied.
    /// </summary>
    static int GroupDuplicatesUnderEnvironmentTree(Transform environmentRoot, HashSet<string> skip)
    {
        int total = 0;
        int passTotal;
        int guard = 0;
        do
        {
            passTotal = 0;
            var stack = new Stack<Transform>();
            stack.Push(environmentRoot);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                var siblings = new List<Transform>();
                for (int i = 0; i < t.childCount; i++)
                    siblings.Add(t.GetChild(i));
                passTotal += GroupDuplicateSiblingsUnderParent(siblings, t, t.scene, skip);

                for (int i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));
            }
            total += passTotal;
            guard++;
        } while (passTotal > 0 && guard < 16);

        return total;
    }

    /// <summary>
    /// Siblings that share the same <see cref="GetBaseDuplicateName"/> (and appear at least twice) get a parent
    /// named <c>baseName + GroupSuffix</c>.
    /// </summary>
    static int GroupDuplicateSiblingsUnderParent(List<Transform> siblings, Transform parentOrNull, Scene scene, HashSet<string> skip)
    {
        var byBase = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
        foreach (var t in siblings)
        {
            if (skip.Contains(t.name))
                continue;
            if (t.name.EndsWith(GroupSuffix, StringComparison.Ordinal))
                continue;

            var baseName = GetBaseDuplicateName(t.name);
            if (string.IsNullOrEmpty(baseName))
                continue;

            if (!byBase.TryGetValue(baseName, out var list))
            {
                list = new List<Transform>();
                byBase[baseName] = list;
            }
            list.Add(t);
        }

        int groups = 0;
        foreach (var kv in byBase)
        {
            if (kv.Value.Count < 2)
                continue;

            var baseName = kv.Key;
            var groupGo = new GameObject(baseName + GroupSuffix);
            groups++;

            if (parentOrNull != null)
                Undo.SetTransformParent(groupGo.transform, parentOrNull, "Group duplicates");
            else
                SceneManager.MoveGameObjectToScene(groupGo, scene);

            Undo.RegisterCreatedObjectUndo(groupGo, "Group duplicates");

            foreach (var child in kv.Value)
                Undo.SetTransformParent(child, groupGo.transform, "Group duplicates");
        }

        return groups;
    }

    static Transform FindEnvironmentTransform(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == EnvironmentRootName)
                return go.transform;
        }
        return null;
    }

    static int OrganizeDirectChildrenOfEnvironment(Transform env, Dictionary<string, Transform> folders)
    {
        int moved = 0;
        var list = new List<Transform>();
        for (int i = 0; i < env.childCount; i++)
            list.Add(env.GetChild(i));

        foreach (var child in list)
        {
            if (folders.ContainsValue(child))
                continue;

            var cat = Categorize(child.name);
            if (cat == null)
                continue;

            Undo.SetTransformParent(child, folders[cat], "Organize hierarchy");
            moved++;
        }

        return moved;
    }

    static bool IsOrganizerFolder(Transform t, Dictionary<string, Transform> folders)
    {
        foreach (var kv in folders)
        {
            if (kv.Value == t)
                return true;
        }
        return false;
    }

    static Transform FindOrCreateEnvironmentRoot(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == EnvironmentRootName)
                return go.transform;
        }

        var env = new GameObject(EnvironmentRootName);
        SceneManager.MoveGameObjectToScene(env, scene);
        return env.transform;
    }

    static Transform FindOrCreateChildFolder(Transform parent, string folderName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == folderName && c.GetComponents<Component>().Length == 1)
                return c;
        }

        var go = new GameObject(folderName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    /// <returns>Folder name from <see cref="FolderNames"/>, or null to leave object where it is.</returns>
    static string Categorize(string name)
    {
        var n = GetNameForCategorization(name);
        if (string.IsNullOrEmpty(n))
            return null;

        if (n.StartsWith("512dirt", StringComparison.Ordinal) ||
            n.StartsWith("Lake", StringComparison.Ordinal) ||
            n.IndexOf("dirtwide", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (n.IndexOf("dirt_", StringComparison.Ordinal) >= 0 && n.StartsWith("512", StringComparison.Ordinal)))
            return "Terrain";

        if (n.StartsWith("house", StringComparison.OrdinalIgnoreCase))
            return "Buildings";

        if (n.StartsWith("straight_", StringComparison.Ordinal) ||
            n.StartsWith("end_", StringComparison.Ordinal))
            return "Roads";

        if (n.StartsWith("fence", StringComparison.OrdinalIgnoreCase))
            return "Fences";

        if (n.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("tree", StringComparison.OrdinalIgnoreCase))
            return "Nature";

        if (n.StartsWith("streetlight", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Blumen", StringComparison.Ordinal) ||
            n.StartsWith("enten_", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("Ice_Bus", StringComparison.OrdinalIgnoreCase))
            return "Props";

        return null;
    }
}
#endif
