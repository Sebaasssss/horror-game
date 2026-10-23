using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The game scene holds a single GameBoot object; everything else is created from code and Resources at runtime.
public static class GameSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Game.unity";

    [MenuItem("Tools/Retro/Rebuild Game Scene")]
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Game").AddComponent<GameBoot>();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Game scene rebuilt at " + ScenePath);
    }

    // Batch entry: Unity -batchmode -quit -executeMethod GameSceneSetup.BuildBatch
    public static void BuildBatch()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Build();
    }
}
