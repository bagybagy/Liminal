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
        bool spatialPassed;
        float flightDistance;
        Vector3 lastPlayerPosition;
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
            spatialPassed=VerifySpatialContracts();
            lastPlayerPosition=experience.Flight.Position;
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
            if(!experience.Music.Paused) Pilot(t,Mathf.Min(Time.unscaledDeltaTime,0.05f));
            flightDistance+=Vector3.Distance(lastPlayerPosition,experience.Flight.Position);
            lastPlayerPosition=experience.Flight.Position;
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
                errors=errors.ToArray(),spatialPassed=spatialPassed,flightDistance=flightDistance,hits=experience.Combat.Hits,damage=experience.Combat.BossDamage,
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
            int damageTaken=e.DamageTaken,simulationSteps=experience.World.Serpent.SimulationSteps;
            int missed=e.MissedScheduledHits,cancelled=e.CancelledAfterFinish;
            experience.Restart();
            bool restart=e.Points==0&&e.Hits==0&&!e.Ended&&!e.HasPending&&e.Locks.Count==0&&experience.Music.Time<0.05;
            for(int i=0;i<8;i++) e.ReceiveDamage();
            e.Tick(0,false);
            bool failure=e.Lost&&!e.Won;
            experience.Restart();
            yield return new WaitForSecondsRealtime(0.1f);
            bool rendered=litFractions.Count>=5&&litFractions.Take(4).All(x=>x>0.002f&&x<0.7f);
            bool dissolved=litFractions.Count>=5&&litFractions[4]>0.0001f&&litFractions[4]<litFractions.Take(4).Min()*0.5f;
            bool passed=victory&&restart&&failure&&pausePassed&&rendered&&dissolved&&spatialPassed&&flightDistance>300&&simulationSteps>500&&damage==240&&locks==8&&missed==0&&fired==hits+cancelled&&errors.Count==0&&gridError<0.00001&&impactDelay<0.1;
            var report=new Report {
                passed=passed,won=victory,restart=restart,failure=failure,pause=pausePassed,rendered=rendered,dissolved=dissolved,spatialPassed=spatialPassed,flightDistance=flightDistance,simulationSteps=simulationSteps,missedScheduledHits=missed,cancelledAfterFinish=cancelled,litFractions=litFractions.ToArray(),
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
        void Pilot(float song,float dt)
        {
            var flight=experience.Flight;
            Vector3 focus=Anatomy.Focus(song);
            Vector3 desired=focus+new Vector3(Mathf.Cos(song*.025f)*52,15,Mathf.Sin(song*.025f)*52);
            Vector3 toward=focus-flight.Position;
            Vector3 forward=flight.View.transform.forward;
            float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            float targetYaw=Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg;
            float targetPitch=Mathf.Atan2(toward.y,new Vector2(toward.x,toward.z).magnitude)*Mathf.Rad2Deg;
            float blend=1-Mathf.Exp(-dt*3);
            Vector2 look=new(Mathf.DeltaAngle(yaw,targetYaw)*blend/2.1f,(targetPitch-pitch)*blend/1.7f);
            Vector3 motion=Vector3.ClampMagnitude((desired-flight.Position)/24,1);
            Vector3 local=new(Vector3.Dot(motion,flight.View.transform.right),motion.y,Vector3.Dot(motion,flight.View.transform.forward));
            flight.Step(song,dt,local,look,Vector3.Distance(desired,flight.Position)>90);
        }
        bool VerifySpatialContracts()
        {
            var flight=experience.Flight;
            Vector3 worldOrigin=experience.World.transform.position;
            Vector3 start=Anatomy.Focus(0);
            flight.SetPose(start,Quaternion.identity);
            for(int i=0;i<360;i++) flight.Step(0,1f/60,Vector3.forward,Vector2.zero,true);
            bool freedom=Vector3.Distance(start,flight.Position)>220;
            bool cameraBehind=Vector3.Dot(flight.Position-flight.View.transform.position,flight.View.transform.forward)>5;
            bool stableWorld=Vector3.Distance(worldOrigin,experience.World.transform.position)<0.001f;
            flight.SetPose(start,Quaternion.identity);
            flight.Step(0,1f/60,Vector3.zero,new Vector2(180/2.1f,0),false);
            bool turnCamera=Vector3.Dot(flight.Position-flight.View.transform.position,flight.View.transform.forward)>=6.99f;
            flight.SetPose(start,Quaternion.identity);
            flight.Step(0,1f/60,Vector3.zero,new Vector2(130/2.1f,30/1.7f),false);
            Quaternion orientation=flight.View.transform.rotation;
            for(int i=0;i<300;i++) flight.Step(0,1f/60,Vector3.zero,Vector2.zero,false);
            bool heading=Quaternion.Angle(orientation,flight.View.transform.rotation)<.1f;
            Vector3 min=Vector3.one*10000,max=-min;
            for(int i=0;i<=180;i++) {Vector3 p=Anatomy.Head(i);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            Vector3 travel=max-min;
            bool roaming=travel.x>160&&travel.y>40&&travel.z>190;
            flight.SetPose(start,Quaternion.identity);
            var e=experience.Combat;
            var fixture=new LockTarget {id=-1,hp=2,kind=TargetKind.Ray,born=0,origin=start+Vector3.forward*45,position=start+Vector3.forward*45};
            fixture.visual=PointCloud.Place("Range proof",experience.World.NodeMesh,experience.World.NodeMaterial,transform);
            fixture.visual.transform.position=fixture.position;e.Targets.Add(fixture);
            Vector3 screen=flight.View.WorldToScreenPoint(fixture.position);
            e.AcquireAt((Vector2)screen+Vector2.right*e.LockRadiusPixels*.82f);
            bool generous=e.Locks.Contains(fixture);
            e.AbandonLocks();flight.SetPose(start-Vector3.forward*100,Quaternion.identity);
            bool range=!e.CanAcquire(fixture);
            flight.SetPose(start,Quaternion.identity);e.Acquire(fixture);
            flight.SetPose(start-Vector3.forward*100,Quaternion.identity);e.Release();
            bool retained=e.HasPending;
            experience.Restart();
            bool result=freedom&&cameraBehind&&turnCamera&&stableWorld&&heading&&roaming&&generous&&range&&retained;
            if(!result) errors.Add($"Spatial proof: freedom={freedom}, cameraBehind={cameraBehind}, turnCamera={turnCamera}, stableWorld={stableWorld}, heading={heading}, roaming={roaming}, radius={generous}, range={range}, retained={retained}");
            return result;
        }
        [Serializable] sealed class Preview {public int hits,damage,particles;public bool spatialPassed;public float flightDistance;public float[] renderAndReadbackMs;public string[] errors;}
        [Serializable] sealed class Report
        {
            public bool passed,won,restart,failure,pause,rendered,dissolved,spatialPassed;
            public int hits,fired,bossDamage,maxLocks,score,damageTaken,particles,simulationSteps,missedScheduledHits,cancelledAfterFinish;
            public double scheduledGridErrorMs,maxVisualImpactDelayMs;
            public float logicUpdatesPerSecond,flightDistance;
            public float[] litFractions,renderAndReadbackMs;
            public int[] sections;
            public string[] errors;
            public string unity,gpu,resolution;
        }
        void OnDestroy() {Application.logMessageReceived-=OnLog;}
    }
}
