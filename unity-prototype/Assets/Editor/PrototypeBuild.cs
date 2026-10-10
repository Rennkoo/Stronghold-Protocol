using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Build.Reporting;

public static class PrototypeBuild {
    public static void ValidateAndBuild() {AnimationValidation.Run();BuildWindows();}
    [MenuItem("Stronghold/Create benchmark scene")]
    public static void CreateScene() {
        var shader = Shader.Find("Spine/Skeleton");
        if (!shader) throw new System.Exception("Spine shader missing");
        var retainedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SpineShader.mat");
        if (!retainedMaterial) {
            retainedMaterial = new Material(shader);
            AssetDatabase.CreateAsset(retainedMaterial, "Assets/Resources/SpineShader.mat");
        }
        retainedMaterial.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
        EditorUtility.SetDirty(retainedMaterial);
        foreach(var shaderName in new[] {"Spine/Skeleton","Stronghold/Board","Stronghold/Effects"}) {
            var keepPath="Assets/Resources/Keep"+shaderName.Replace("/","")+".mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(keepPath))AssetDatabase.CreateAsset(new Material(Shader.Find(shaderName)),keepPath);
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Stronghold Benchmark").AddComponent<StrongholdBenchmark>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Benchmark.unity");
        EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene("Assets/Scenes/Benchmark.unity", true)};
        PlayerSettings.companyName = "Rennkoo";
        PlayerSettings.productName = "Stronghold Rendering Prototype";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        AssetDatabase.SaveAssets();
    }
    public static void BuildWindows() {
        CreateScene();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] {"Assets/Scenes/Benchmark.unity"},
            locationPathName = "Builds/Windows/StrongholdBenchmark.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Windows build failed: " + report.summary.result);
    }
}
