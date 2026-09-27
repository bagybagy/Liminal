using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal.Editor
{
    public static class Production
    {
        const string Generated="Assets/Liminal/Production";
        [MenuItem("Liminal/Create Production Scene")]
        public static void Create()
        {
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            var renderer=LoadOrCreate<UniversalRendererData>(Generated+"/Renderer.asset");
            renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(renderer);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Generated+"/Pipeline.asset");
            if(!pipeline) {pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,Generated+"/Pipeline.asset");}
            pipeline.supportsHDR=true;pipeline.msaaSampleCount=1;pipeline.renderScale=1;pipeline.shadowDistance=0;
            GraphicsSettings.defaultRenderPipeline=pipeline;
            for(int i=0;i<QualitySettings.names.Length;i++) {QualitySettings.SetQualityLevel(i);QualitySettings.renderPipeline=pipeline;}
            QualitySettings.vSyncCount=1;
            EditorUtility.SetDirty(pipeline);

            var profile=LoadOrCreate<VolumeProfile>(Generated+"/Atmosphere.asset");
            foreach(var component in profile.components) UnityEngine.Object.DestroyImmediate(component,true);
            profile.components.Clear();
            var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(1.3f);bloom.threshold.Override(0.5f);bloom.scatter.Override(0.76f);
            bloom.highQualityFiltering.Override(true);
            var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(0.28f);vignette.smoothness.Override(0.64f);
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(0.5f);color.contrast.Override(8);color.saturation.Override(-4);
            var grain=profile.Add<FilmGrain>(true);grain.intensity.Override(0.065f);grain.response.Override(0.8f);
            foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile);
            var particles=CreateMaterial("Liminal/Luminous",Generated+"/Particles.mat");
            var ribbons=CreateMaterial("Liminal/Ribbon",Generated+"/Ribbons.mat");
            var flowing=CreateMaterial("Liminal/Advected Light",Generated+"/FlowingParticles.mat");
            var membrane=CreateMaterial("Liminal/Bioluminescent Membrane",Generated+"/Membrane.mat");
            var cavern=CreateMaterial("Liminal/Cavern Surface",Generated+"/CavernSurface.mat");
            var marine=CreateMaterial("Liminal/Marine Light",Generated+"/MarineLight.mat");
            flowing.SetFloat("_Gain",0.55f);membrane.SetFloat("_Gain",1.05f);
            EditorUtility.SetDirty(flowing);EditorUtility.SetDirty(membrane);

            string audioPath="Assets/Liminal/Audio/TidalMemory.wav";
            var audioImporter=(AudioImporter)AssetImporter.GetAtPath(audioPath);
            var settings=audioImporter.defaultSampleSettings;
            settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=0.86f;
            audioImporter.defaultSampleSettings=settings;audioImporter.SaveAndReimport();

            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cameraObj=new GameObject("Camera / abyss");
            var camera=cameraObj.AddComponent<Camera>();camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(0.001f,0.003f,0.006f,1);
            camera.nearClipPlane=0.1f;camera.farClipPlane=1600;camera.fieldOfView=54;camera.allowHDR=true;
            camera.transform.position=Anatomy.Focus(0)+new Vector3(30,13,-84);camera.transform.LookAt(Anatomy.Focus(0));
            cameraObj.AddComponent<AudioListener>();
            var cameraData=camera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=true;
            cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            cameraData.volumeLayerMask=1;
            var volume=new GameObject("Atmosphere / HDR").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.fog=false;
            var game=new GameObject("LIMINAL").AddComponent<Experience>();
            game.soundtrack=AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            game.particles=particles;game.ribbons=ribbons;game.sceneCamera=camera;
            game.advectedParticles=flowing;game.membrane=membrane;
            game.cavernSurface=cavern;game.marineLight=marine;
            game.particleSimulation=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Liminal/Shaders/LeviathanFlow.compute");
            string scenePath=Generated+"/AbyssalChoir.unity";
            EditorSceneManager.SaveScene(scene,scenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            PlayerSettings.companyName="Bagybagy";PlayerSettings.productName="LIMINAL";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            AssetDatabase.SaveAssets();
            if(!game.soundtrack||!particles.shader.isSupported||!renderer.postProcessData||!game.particleSimulation||!flowing.shader.isSupported||!membrane.shader.isSupported) throw new Exception("Production asset validation failed");
            if(Math.Abs(game.soundtrack.length-Score.Duration)>0.02) throw new Exception("Soundtrack and authored score duration differ");
            if(Math.Abs(Score.NextEighth(3.14159)/(Score.BeatSeconds*0.5)-Math.Round(Score.NextEighth(3.14159)/(Score.BeatSeconds*0.5)))>0.000001)
                throw new Exception("Score quantization failed");
            Debug.Log("LIMINAL_SCENE_READY "+scenePath);
        }
        [MenuItem("Liminal/Build Windows Player")]
        public static void Build()
        {
            Create();
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{Generated+"/AbyssalChoir.unity"},
                locationPathName="Builds/Windows/Liminal.exe",target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.CompressWithLz4HC
            });
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("LIMINAL_BUILD_SUCCESS bytes="+report.summary.totalSize);
        }
        static T LoadOrCreate<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!asset) {asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}
            return asset;
        }
        static Material CreateMaterial(string shader,string path)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) {m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
            return m;
        }
    }
}
