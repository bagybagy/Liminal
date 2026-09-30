using UnityEditor;
using UnityEditor.SceneManagement;

namespace Liminal.Editor
{
    [InitializeOnLoad]
    static class TrailerCaptureSession
    {
        const string CaptureKey="Liminal.TrailerCapture.Autopilot";
        const string ScenePath="Assets/Liminal/Production/AbyssalChoir.unity";

        static TrailerCaptureSession()
        {
            EditorApplication.playModeStateChanged+=OnPlayModeChanged;
        }

        [MenuItem("Liminal/Trailer/Start Editor Autopilot")]
        static void StartEditorAutopilot()
        {
            EditorPrefs.SetBool(CaptureKey,true);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying=true;
        }

        [MenuItem("Liminal/Trailer/Stop Editor Autopilot")]
        static void StopEditorAutopilot()
        {
            EditorPrefs.SetBool(CaptureKey,false);
            EditorApplication.isPlaying=false;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode)
                EditorPrefs.SetBool(CaptureKey,false);
        }
    }
}
