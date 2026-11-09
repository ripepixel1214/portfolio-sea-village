using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    private const string BuildOutputPath = "Builds/Windows/Development/SeaVillage.exe";
    private const string AndroidOutputPath = "Builds/Android/SeaVillage.aab";
    private const int MinimumPlayTargetApi = 36;

    #region Public API
    public static void PerformBuild()
    {
        switch (EditorUserBuildSettings.activeBuildTarget)
        {
            case BuildTarget.StandaloneWindows64:
                PerformDevelopmentBuild();
                break;
            case BuildTarget.Android:
                PerformAndroidReleaseBuild();
                break;
            default:
                throw new BuildFailedException("[BuildScript] 자동 빌드는 Windows64 또는 Android만 지원합니다");
        }
    }

    public static void PerformDevelopmentBuild()
    {
        BuildPlayer(BuildTarget.StandaloneWindows64, BuildOutputPath, BuildOptions.Development);
    }

    public static void PerformReleaseBuild()
    {
        BuildPlayer(BuildTarget.StandaloneWindows64, "Builds/Windows/Release/SeaVillage.exe", BuildOptions.None);
    }

    [MenuItem("SeaVillage/Build/Configure Android App Bundle")]
    public static void ConfigureAndroidAppBundle()
    {
        var settings = GetPlayerSettings();
        SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
        if (inputHandler == null)
            throw new BuildFailedException("[BuildScript] Active Input Handling 설정을 찾을 수 없습니다");
        bool inputChanged = inputHandler.intValue != 1;
        inputHandler.intValue = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();

        // 앱 식별자·서명·버전·최소 지원 OS는 기존 정책 유지
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        if ((int)PlayerSettings.Android.targetSdkVersion < MinimumPlayTargetApi)
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)MinimumPlayTargetApi;
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildScript] Android AAB 설정 적용 완료: IL2CPP·ARM64·Target API 36 이상");
        if (inputChanged)
            Debug.LogWarning("[BuildScript] Input System 설정 적용을 위해 Editor를 재시작해야 합니다");
    }

    [MenuItem("SeaVillage/Build/Validate Android Release Settings")]
    public static void ValidateAndroidReleaseSettings()
    {
        var errors = new List<string>();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            errors.Add("현재 Editor에 Android Build Support가 필요합니다");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            errors.Add("Build Profiles에서 Android로 전환해야 합니다. 배치 실행은 -buildTarget Android를 지정합니다");
        if (!EditorUserBuildSettings.buildAppBundle || EditorUserBuildSettings.exportAsGoogleAndroidProject)
            errors.Add("Configure Android App Bundle을 먼저 실행해야 합니다");
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            errors.Add("Android Scripting Backend가 IL2CPP여야 합니다");
        if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            errors.Add("Android Target Architectures에 ARM64가 필요합니다");
        if ((int)PlayerSettings.Android.targetSdkVersion < MinimumPlayTargetApi)
            errors.Add("Android Target API를 Auto가 아닌 36 이상으로 지정해야 합니다");

        var settings = GetPlayerSettings();
        SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
        if (inputHandler == null || inputHandler.intValue != 1)
            errors.Add("Active Input Handling을 Input System Package (New)로 설정하고 Editor를 재시작해야 합니다");

        if (string.IsNullOrWhiteSpace(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)))
            errors.Add("Android Application Identifier가 필요합니다");
        if (PlayerSettings.Android.bundleVersionCode < 1)
            errors.Add("Android Bundle Version Code는 양수여야 합니다");
        if (!PlayerSettings.Android.useCustomKeystore
            || string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName)
            || !File.Exists(PlayerSettings.Android.keystoreName)
            || string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
            errors.Add("Publishing Settings에서 실제 업로드 keystore와 alias를 연결해야 합니다");

        GetEnabledScenes();
        if (errors.Count > 0)
            throw new BuildFailedException("[BuildScript] Android 제출 설정 미완료:\n- " + string.Join("\n- ", errors));

        Debug.Log("[BuildScript] Android 제출 설정 검증 통과. 서명 비밀번호·최종 Manifest·16KB 호환성은 실제 빌드에서 별도 검증 필요");
    }

    [MenuItem("SeaVillage/Build/Build Android Release AAB")]
    public static void PerformAndroidReleaseBuild()
    {
        bool bundle = EditorUserBuildSettings.buildAppBundle;
        bool export = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        string keystore = PlayerSettings.Android.keystoreName;
        string keystorePass = PlayerSettings.Android.keystorePass;
        string alias = PlayerSettings.Android.keyaliasName;
        string aliasPass = PlayerSettings.Android.keyaliasPass;
        try
        {
            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            ApplyCiSigning();
            ValidateAndroidReleaseSettings();
            BuildPlayer(BuildTarget.Android, AndroidOutputPath, BuildOptions.None);
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = export;
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
        }
    }
    #endregion

    #region Private Helpers
    private static void ApplyCiSigning()
    {
        // GameCI가 컨테이너에 전달한 서명 설정 사용
        string[] names = { "ANDROID_KEYSTORE_NAME", "ANDROID_KEYSTORE_PASS", "ANDROID_KEYALIAS_NAME", "ANDROID_KEYALIAS_PASS" };
        string[] values = names.Select(System.Environment.GetEnvironmentVariable).ToArray();
        if (values.All(string.IsNullOrEmpty))
            return;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(values[i]))
                throw new BuildFailedException($"[BuildScript] CI 서명 설정 누락: {names[i]}");
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = Path.GetFullPath(Path.Combine(Application.dataPath, "..", values[0]));
        PlayerSettings.Android.keystorePass = values[1];
        PlayerSettings.Android.keyaliasName = values[2];
        PlayerSettings.Android.keyaliasPass = values[3];
    }

    private static SerializedObject GetPlayerSettings()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets.Length == 0 || assets[0] == null)
            throw new BuildFailedException("[BuildScript] PlayerSettings를 읽을 수 없습니다");
        return new SerializedObject(assets[0]);
    }

    private static void BuildPlayer(BuildTarget target, string outputPath, BuildOptions options)
    {
        if (EditorUserBuildSettings.activeBuildTarget != target)
            throw new BuildFailedException($"[BuildScript] 먼저 빌드 대상을 {target}(으)로 전환해야 합니다");
        string[] scenes = GetEnabledScenes();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = options
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report == null || report.summary.result != BuildResult.Succeeded)
        {
            string result = report == null ? "결과 없음" : report.summary.result.ToString();
            throw new BuildFailedException($"[BuildScript] {target} 빌드 실패: {result}");
        }
        Debug.Log($"[BuildScript] {target} 빌드 완료: {outputPath}");
    }

    // 활성 씬 목록은 EditorBuildSettings에서만 조회
    private static string[] GetEnabledScenes()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new BuildFailedException("[BuildScript] 활성화된 빌드 씬이 없습니다");
        foreach (string scene in scenes)
        {
            if (string.IsNullOrWhiteSpace(scene) || !File.Exists(scene))
                throw new BuildFailedException($"[BuildScript] 빌드 씬을 찾을 수 없습니다: {scene}");
        }
        return scenes;
    }
    #endregion
}
