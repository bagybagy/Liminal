using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class RuntimeProof : MonoBehaviour
    {
        Experience experience;
        readonly List<string> errors=new();
        readonly List<float> frames=new();
        readonly List<int> sections=new();
        readonly float[] captureTimes={9,54,119,163};
        string directory;
        float nextVolley=8,wallStart;
        int captureIndex;
        bool finishing,pausedTest,preview;
        double pauseSong;
        float pauseWall;
        bool pausePassed;
        readonly List<float> litFractions=new();
        readonly List<float> renderAndReadbackMs=new();
        public void Initialize(Experience value)
        {
            experience=value;
            string[] args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"--output");
            directory=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.dataPath,"../Verification");
            Directory.CreateDirectory(directory);
            preview=Array.IndexOf(args,"--preview")>=0;
            wallStart=Time.realtimeSinceStartup;
            Application.logMessageReceived+=OnLog;
        }
        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message+"\n"+stack);
        }
        void Update()
        {
            if(!experience||!experience.Ready||finishing) return;
            float t=(float)experience.Music.Time;
            var e=experience.Combat;
            if(!sections.Contains(e.Section)) sections.Add(e.Section);
            if(t>5) frames.Add(Time.unscaledDeltaTime);
            if(!pausedTest && t>2) {
                pausedTest=true;pauseSong=experience.Music.Time;pauseWall=Time.realtimeSinceStartup;
                experience.Music.SetPaused(true);
            }
            if(experience.Music.Paused && Time.realtimeSinceStartup-pauseWall>0.3f) {
                pausePassed=Math.Abs(experience.Music.Time-pauseSong)<0.005;
                experience.Music.SetPaused(false);
            }
            if(captureIndex<captureTimes.Length && t>captureTimes[captureIndex]) {
                CaptureFrame("stage-"+captureIndex+".png");captureIndex++;
            }
            if(t>=nextVolley && !e.Ended) {
                // Hit-test projected positions through the same acquisition path as the mouse.
                foreach(var target in e.Targets.OrderByDescending(x=>x.kind==TargetKind.Threat).ThenBy(x=>x.kind==TargetKind.Ray?0:1)) {
                    if(!target.Available||e.Locks.Count>=8) continue;
                    Vector3 p=experience.Flight.View.WorldToScreenPoint(target.position);
                    if(p.z>0 && p.x>25 && p.x<Screen.width-25 && p.y>90 && p.y<Screen.height-90) e.AcquireAt(p);
                }
                e.Release();nextVolley=t+2.05f;
            }
            if(preview&&t>12) {finishing=true;StartCoroutine(FinishPreview());}
            else if(!preview&&e.Ended && t-e.EndTime>10) {finishing=true;StartCoroutine(Finish());}
            else if(Time.realtimeSinceStartup-wallStart>235) {
                errors.Add("Verification timed out");finishing=true;StartCoroutine(Finish());
            }
        }
        IEnumerator FinishPreview()
        {
            yield return new WaitForSecondsRealtime(0.1f);
            File.WriteAllText(Path.Combine(directory,"preview.json"),JsonUtility.ToJson(new Preview {
                errors=errors.ToArray(),hits=experience.Combat.Hits,damage=experience.Combat.BossDamage,
                particles=experience.World.ParticleCount,renderAndReadbackMs=renderAndReadbackMs.ToArray()},true));
            Application.Quit(errors.Count==0?0:2);
        }
        IEnumerator Finish()
        {
            var e=experience.Combat;
            CaptureFrame("complete.png");
            yield return new WaitForSecondsRealtime(0.1f);
            bool victory=e.Won;
            int hits=e.Hits,damage=e.BossDamage,fired=e.Fired,locks=e.MaxLocks,points=e.Points;
            double impactDelay=e.MaxImpactDelay,gridError=experience.Music.MaxGridError;
            int damageTaken=e.DamageTaken;
            experience.Restart();
            bool restart=e.Points==0&&e.Hits==0&&!e.Ended&&!e.HasPending&&e.Locks.Count==0&&experience.Music.Time<0.05;
            for(int i=0;i<8;i++) e.ReceiveDamage();
            e.Tick(0,false);
            bool failure=e.Lost&&!e.Won;
            experience.Restart();
            yield return new WaitForSecondsRealtime(0.1f);
            bool rendered=litFractions.Count>=4&&litFractions.All(x=>x>0.002f&&x<0.7f);
            bool passed=victory&&restart&&failure&&pausePassed&&rendered&&damage==240&&locks==8&&errors.Count==0&&gridError<0.00001&&impactDelay<0.1;
            var report=new Report {
                passed=passed,won=victory,restart=restart,failure=failure,pause=pausePassed,rendered=rendered,litFractions=litFractions.ToArray(),
                hits=hits,fired=fired,bossDamage=damage,maxLocks=locks,score=points,damageTaken=damageTaken,
                scheduledGridErrorMs=gridError*1000,maxVisualImpactDelayMs=impactDelay*1000,
                logicUpdatesPerSecond=frames.Count/frames.Sum(),renderAndReadbackMs=renderAndReadbackMs.ToArray(),
                particles=experience.World.ParticleCount,sections=sections.ToArray(),errors=errors.ToArray(),
                unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,resolution=Screen.width+"x"+Screen.height
            };
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LIMINAL_PROOF "+JsonUtility.ToJson(report));
            Application.Quit(passed?0:2);
        }
        void CaptureFrame(string name)
        {
            // A hidden Windows player may skip its swapchain. Request an actual URP render.
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
            rt.Create();
            RenderPipeline.SubmitRenderRequest(experience.Flight.View,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            renderAndReadbackMs.Add((float)timer.Elapsed.TotalMilliseconds);
            RenderTexture.active=previous;
            var pixels=image.GetPixels32();int lit=0;
            foreach(var p in pixels) if(Mathf.Max(p.r,Mathf.Max(p.g,p.b))>80) lit++;
            litFractions.Add(lit/(float)pixels.Length);
            File.WriteAllBytes(Path.Combine(directory,name),image.EncodeToPNG());
            Destroy(image);rt.Release();Destroy(rt);
        }
        [Serializable] sealed class Preview {public int hits,damage,particles;public float[] renderAndReadbackMs;public string[] errors;}
        [Serializable] sealed class Report
        {
            public bool passed,won,restart,failure,pause,rendered;
            public int hits,fired,bossDamage,maxLocks,score,damageTaken,particles;
            public double scheduledGridErrorMs,maxVisualImpactDelayMs;
            public float logicUpdatesPerSecond;
            public float[] litFractions,renderAndReadbackMs;
            public int[] sections;
            public string[] errors;
            public string unity,gpu,resolution;
        }
        void OnDestroy() {Application.logMessageReceived-=OnLog;}
    }
}
