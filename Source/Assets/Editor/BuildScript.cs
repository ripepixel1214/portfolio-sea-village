using System.Linq;
using UnityEditor;
using UnityEngine;

public class BuildScript
{
    private const string BuildOutputPath = "Builds/SeaVillage.exe";

    // 배치모드에서의 기본 빌드 => 이후 릴리즈 시 PerformReleaseBuild()로 변경
    public static void PerformBuild()
    {
        // PerformDevelopmentBuild();
        PerformReleaseBuild();
    }

    public static void PerformDevelopmentBuild()
    {
        BuildWindowsPlayer(BuildOptions.Development);
    }

    public static void PerformReleaseBuild()
    {
        BuildWindowsPlayer(BuildOptions.None);
    }

    private static void BuildWindowsPlayer(BuildOptions options)
    {
        string[] scenes = GetEnabledScenes();
        if (scenes.Length == 0)
        {
            Debug.LogError("[BuildScript] Build Settings에 활성화된 씬이 없습니다. 빌드를 중단합니다");
            return;
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BuildOutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = options
        };

        BuildPipeline.BuildPlayer(buildPlayerOptions);
    }

    // Build Settings(EditorBuildSettings.asset)의 활성 씬 목록을 단일 진리원으로 사용
    private static string[] GetEnabledScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }
}
