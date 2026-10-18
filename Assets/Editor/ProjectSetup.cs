using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MedievalTown.EditorTools
{
    /// <summary>
    /// First time the project opens: creates Assets/Scenes/Main.unity, adds it to Build Settings,
    /// names the product, and opens the scene. The game itself boots from code when you press Play.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Ensure;
            // Edit > Preferences > General > Script Changes While Playing: "Recompile After Finished Playing",
            // so updated scripts never reload in the middle of a game.
            if (EditorPrefs.GetInt("ScriptCompilationDuringPlay", 0) == 0) EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
        }

        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                PlayerSettings.productName = "Medieval Town";
                PlayerSettings.companyName = "Medieval Town";
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
