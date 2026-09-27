using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Liminal
{
    public sealed class CavernProof : MonoBehaviour
    {
        Experience experience;
        string directory;
        readonly List<string> errors=new();
        readonly List<float> renderedFractions=new();
        float halfSpeed,cruiseSpeed,coastSpeed;
        int jellyHits,fishResponses,serpentHits,whaleHits;
        bool passageTravel,pauseStable,restartClean;
        public void Initialize(Experience value)
        {
            experience=value;
            string[] args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"--output");
            directory=at>=0 && at+1<args.Length?args[at+1]:Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification/Caverns"));
            Directory.CreateDirectory(directory);
            Application.logMessageReceived+=OnLog;
        }
        void OnLog(string message,string stack,LogType type)
        {
            if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && errors.Count<20)
                errors.Add(message+"\n"+stack);
        }
        IEnumerator Start()
        {
            yield return null;
            var flight=experience.Flight;
            flight.SetPose(CaveLayout.Spawn,Quaternion.identity);
            for(int i=0;i<42;i++) flight.Step(0,1f/120,Vector3.forward,Vector2.zero,false);
            halfSpeed=flight.Speed;
            for(int i=0;i<42;i++) flight.Step(0,1f/120,Vector3.forward,Vector2.zero,false);
            cruiseSpeed=flight.Speed;
            for(int i=0;i<102;i++) flight.Step(0,1f/120,Vector3.zero,Vector2.zero,false);
            coastSpeed=flight.Speed;
            Require(Mathf.Abs(halfSpeed-12)<.05f && Mathf.Abs(cruiseSpeed-24)<.05f,"Cruise must ramp over .7 seconds");
            Require(coastSpeed>.8f && coastSpeed<1.5f,"Swimmer must coast smoothly after releasing input");
            passageTravel=TravelRoute();Require(passageTravel,"Both open passages must be physically traversable");
            experience.Restart();
            yield return new WaitForSecondsRealtime(.6f);
            Capture("01-lantern-grotto.png");
            yield return HitTargets(experience.Marine.JellyTargets,1);
            jellyHits=experience.Marine.IlluminatedJellies;
            Require(jellyHits>0,"Jelly shot must illuminate a lantern");
            Capture("02-lantern-lit.png");
            yield return HitTargets(experience.Marine.FishTargets,1);
            fishResponses=experience.Marine.FishResponses;
            Require(fishResponses>0,"Fish school must react to a scheduled shot");

            Vector3 focus=Anatomy.Focus((float)experience.Music.Time);
            flight.SetPose(focus+new Vector3(35,12,-64),Quaternion.LookRotation(new Vector3(-35,-12,64)));
            yield return new WaitForSecondsRealtime(.4f);
            Capture("03-serpent-sanctum.png");
            var organs=new List<LockTarget>();
            foreach(var target in experience.Combat.Targets) if(target.kind==TargetKind.Organ) organs.Add(target);
            for(int volley=0;volley<10;volley++) yield return HitTargets(organs,8);
            serpentHits=experience.Combat.BossDamage;
            Require(experience.Combat.SerpentComplete && serpentHits==80 && !experience.Combat.Ended,
                "Serpent's 80 hits must complete its stage without ending exploration");

            Vector3 whale=experience.Marine.WhalePosition;
            Vector3 whaleView=whale+experience.Marine.WhaleRotation*new Vector3(175,30,45);
            flight.SetPose(whaleView,Quaternion.LookRotation(whale-whaleView));
            yield return new WaitForSecondsRealtime(.4f);
            Capture("04-horizon-whale.png");
            yield return HitTargets(experience.Marine.WhaleResonatorTargets,8);
            whaleHits=experience.Marine.WhaleResonance;
            Require(whaleHits==8 && !experience.Combat.Ended,"All eight whale resonators must awaken without blocking flight");
            Require(experience.Music.MaxGridError<.00001,"Hits must remain aligned with authored soundtrack");
            experience.TogglePause();
            double song=experience.Music.Time;
            yield return new WaitForSecondsRealtime(.15f);
            pauseStable=Math.Abs(experience.Music.Time-song)<.005;
            Require(pauseStable,"Pause must freeze song time");
            experience.TogglePause();
            int count=experience.Combat.Targets.Count;
            experience.Restart();
            yield return null;
            restartClean=experience.Combat.BossDamage==0 && experience.Marine.WhaleResonance==0 &&
                experience.Marine.IlluminatedJellies==0 && !experience.Combat.HasPending &&
                Vector3.Distance(flight.Position,CaveLayout.Spawn)<.01f;
            int afterRestart=experience.Combat.Targets.Count;
            experience.Restart();
            Require(restartClean && experience.Combat.Targets.Count==afterRestart,"Restart must reset and not duplicate fauna targets");
            Require(renderedFractions.Count==4 && renderedFractions.TrueForAll(x=>x>.001f && x<.95f),"Every chamber must render visible, nonblank content");
            var report=new Report {passed=errors.Count==0,halfSpeed=halfSpeed,cruiseSpeed=cruiseSpeed,coastSpeed=coastSpeed,
                passageTravel=passageTravel,jellyHits=jellyHits,fishResponses=fishResponses,serpentHits=serpentHits,whaleHits=whaleHits,
                pauseStable=pauseStable,restartClean=restartClean,gridError=experience.Music.MaxGridError,
                renderedFractions=renderedFractions.ToArray(),errors=errors.ToArray(),gpu=SystemInfo.graphicsDeviceName};
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LIMINAL_CAVERNS_PROOF "+JsonUtility.ToJson(report));
            Application.Quit(report.passed?0:2);
        }

        bool TravelRoute()
        {
            var waypoints=new List<Vector3> {CaveLayout.Spawn,CaveLayout.Rooms[0].Center};
            waypoints.AddRange(CaveLayout.Passages[0]);waypoints.Add(CaveLayout.Rooms[1].Center);
            waypoints.AddRange(CaveLayout.Passages[1]);waypoints.Add(CaveLayout.Rooms[2].Center);
            var flight=experience.Flight;
            for(int p=1;p<waypoints.Count;p++) {
                Vector3 start=waypoints[p-1],end=waypoints[p];
                flight.SetPose(start,Quaternion.LookRotation(end-start));
                int steps=Mathf.CeilToInt(Vector3.Distance(start,end)/24f*60)+120;
                for(int i=0;i<steps && Vector3.Distance(flight.Position,end)>1;i++)
                    flight.Step(0,1f/60,Vector3.forward,Vector2.zero,false);
                if(Vector3.Distance(flight.Position,end)>2) return false;
            }
            return true;
        }

        IEnumerator HitTargets(IReadOnlyList<LockTarget> targets,int maximum)
        {
            var combat=experience.Combat;
            int accepted=0;
            foreach(var target in targets) {
                if(accepted>=maximum) break;
                if(!target.Available) continue;
                Vector3 center=CaveLayout.Rooms[CaveLayout.NearestRoom(target.position)].Center;
                Vector3 toward=(center-target.position).normalized;
                if(toward.sqrMagnitude<.01f) toward=Vector3.back;
                Vector3 position=target.position+toward*22;
                experience.Flight.SetPose(position,Quaternion.LookRotation(target.position-position));
                Vector3 projected=experience.Flight.View.WorldToScreenPoint(target.position);
                int before=combat.Locks.Count;
                combat.AcquireAt(projected);
                if(combat.Locks.Count>before) accepted++;
            }
            Require(accepted>0,"Expected at least one visible target acquired");
            combat.Release();
            float deadline=Time.realtimeSinceStartup+5;
            while(combat.HasPending && Time.realtimeSinceStartup<deadline) yield return null;
            Require(!combat.HasPending,"Scheduled shots must resolve within five seconds");
            yield return null;
        }

        void Capture(string name)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);rt.Create();
            RenderPipeline.SubmitRenderRequest(experience.Flight.View,new UniversalRenderPipeline.SingleCameraRequest {destination=rt});
            var previous=RenderTexture.active;RenderTexture.active=rt;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
            int lit=0;var colors=pixels.GetPixels32();
            foreach(var color in colors) if(Mathf.Max(color.r,Mathf.Max(color.g,color.b))>60) lit++;
            renderedFractions.Add(lit/(float)colors.Length);
            File.WriteAllBytes(Path.Combine(directory,name),pixels.EncodeToPNG());
            RenderTexture.active=previous;Destroy(pixels);rt.Release();Destroy(rt);
        }
        void Require(bool result,string message) {if(!result) errors.Add(message);}
        void OnDestroy() {Application.logMessageReceived-=OnLog;}
        [Serializable] sealed class Report
        {
            public bool passed,passageTravel,pauseStable,restartClean;
            public float halfSpeed,cruiseSpeed,coastSpeed;
            public int jellyHits,fishResponses,serpentHits,whaleHits;
            public double gridError;
            public float[] renderedFractions;
            public string[] errors;
            public string gpu;
        }
    }
}
