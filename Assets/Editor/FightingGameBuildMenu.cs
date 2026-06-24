using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FightingGame.Editor
{
    public static class FightingGameBuildMenu
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string WindowsBuildFolder = "Builds/Windows";

        [MenuItem("Fighting Game/Build Windows to Builds Folder")]
        private static void BuildWindowsToBuildsFolder()
        {
            string scenePath = EnsureBootstrapSceneInBuild();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, WindowsBuildFolder);
            Directory.CreateDirectory(outputDirectory);

            string executablePath = Path.Combine(outputDirectory, "2D Fighting Game.exe");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(executablePath);
                EditorUtility.DisplayDialog(
                    "빌드 완료",
                    "Windows 빌드가 생성되었습니다.\n\n"
                    + executablePath
                    + "\n\n친구에게는 이 폴더(Builds/Windows) 전체를 zip으로 보내세요.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "빌드 실패",
                    "Console 창의 빨간 에러를 확인하세요.\n\n"
                    + "Unity Hub에서 Windows Build Support 모듈이 설치되어 있어야 합니다.",
                    "OK");
            }
        }

        private static string EnsureBootstrapSceneInBuild()
        {
            if (!File.Exists(BootstrapScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootstrapScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, BootstrapScenePath);
                AssetDatabase.Refresh();
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true)
            };

            return BootstrapScenePath;
        }
    }
}
