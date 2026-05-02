#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Groups loose level objects under an "Environment" parent into subfolders
/// (Terrain, Buildings, Roads, Fences, Nature, Props) by name rules.
/// Menu: Tools / Hierarchy / Organize Level Under Environment
/// </summary>
public static class HierarchyLevelOrganizer
{
    /// <summary>Root objects that are never reparented. Add your project-specific names here.</summary>
    static readonly HashSet<string> SkipRoots = new HashSet<string>(StringComparer.Ordinal)
    {
        "Main Camera", "Virtual Camera", "Canvas", "Lights", "EventSystem",
        "LevelManager", "Prefabs", "Environment", "DontDestroyOnLoad"
    };

    static readonly string[] FolderNames = { "Terrain", "Buildings", "Roads", "Fences", "Nature", "Props" };

    [MenuItem("Tools/Hierarchy/Organize Level Under Environment")]
    static void OrganizeLevelUnderEnvironment()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Hierarchy", "No valid scene is loaded.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        var env = FindOrCreateEnvironmentRoot(scene);
        Undo.RegisterCreatedObjectUndo(env.gameObject, "Create Environment root");

        var folders = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var name in FolderNames)
        {
            var t = FindOrCreateChildFolder(env, name);
            folders[name] = t;
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Create folder " + name);
        }

        var roots = scene.GetRootGameObjects();
        int moved = 0;
        foreach (var go in roots)
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

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[HierarchyLevelOrganizer] Done: moved {moved} object(s) under Environment.");
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
            if (go.name == "Environment")
                return go.transform;
        }

        var env = new GameObject("Environment");
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

    /// <returns>Folder name from <see cref="FolderNames"/>, or null to skip.</returns>
    static string Categorize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var n = name;

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
