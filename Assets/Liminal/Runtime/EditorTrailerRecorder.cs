#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Liminal
{
    public sealed class EditorTrailerRecorder : MonoBehaviour
    {
        RecorderController controller;
        bool started,stopping;

        void Start()
        {
            try
            {
                var settings=ScriptableObject.CreateInstance<RecorderControllerSettings>();
                settings.FrameRate=30;
                settings.CapFrameRate=true;
                settings.SetRecordModeToTimeInterval(0,58);

                var movie=ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name="LIMINAL Game View";
                movie.Enabled=true;
                movie.EncoderSettings=new CoreEncoderSettings {
                    Codec=CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High
                };
                movie.ImageInputSettings=new GameViewInputSettings {OutputWidth=1920,OutputHeight=1080};
                movie.CaptureAudio=true;
                movie.OutputFile=Path.GetFullPath(Path.Combine(Application.dataPath,"../Video/UnityRecorderRetake/LIMINAL_X_AbyssalChoir"));
                Directory.CreateDirectory(Path.GetDirectoryName(movie.OutputFile));
                settings.AddRecorderSettings(movie);

                controller=new RecorderController(settings);
                controller.PrepareRecording();
                started=controller.StartRecording();
                if(!started) throw new InvalidOperationException("Unity Recorder did not start a recording session.");
                Debug.Log("LIMINAL_RECORDER_ACTIVE output="+movie.OutputFile+" duration=58s fps=30 size=1920x1080 audio=true");
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.isPlaying=false;
            }
        }

        void Update()
        {
            if(!started||stopping||controller.IsRecording()) return;
            stopping=true;
            controller.StopRecording();
            Debug.Log("LIMINAL_RECORDER_COMPLETE");
            EditorApplication.isPlaying=false;
        }

        void OnDisable()
        {
            if(controller!=null && controller.IsRecording()) controller.StopRecording();
        }
    }
}
#endif
