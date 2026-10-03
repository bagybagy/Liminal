using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class PressurePatternProof : MonoBehaviour
    {
        Experience game;
        string output;
        float started;
        bool finished;
        readonly List<string> errors=new();
        readonly Report report=new();
        static readonly Color Blue=new(.08f,.55f,1f), Cyan=new(.1f,1f,.8f);

        public void Initialize(Experience owner)
        {
            game=owner;started=Time.realtimeSinceStartup;
            string[] args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"--output");
            output=at>=0 && at+1<args.Length?args[at+1]:Path.GetFullPath("Verification/PressurePatterns");
            Directory.CreateDirectory(output);Application.logMessageReceived+=OnLog;
        }
        void OnLog(string message,string stack,LogType type)
        {
            if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && errors.Count<16)
                errors.Add(message+"\n"+stack);
        }
        void Update()
        {
            if(!finished && Time.realtimeSinceStartup-started>60) {
                errors.Add("Pressure proof exceeded its bounded deadline.");Finish();
            }
        }
        void Check(bool value,string message)
        {
            if(value) return;
            errors.Add(message);Finish();throw new InvalidOperationException(message);
        }
        void ResetRoom()
        {
            game.Restart();game.Tutorial.SetEnabled(false);
            game.Flight.EnableVr(false);
            game.Flight.SetPose(CaveLayout.Rooms[1].Center,Quaternion.identity);
            game.Combat.ActiveRoom=1;
        }
        IEnumerator Resolve()
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(game.Combat.HasPending && Time.realtimeSinceStartup<deadline) yield return null;
            yield return null;
            Check(!game.Combat.HasPending && game.Combat.MissedScheduledHits==0,
                "Accepted multi-lock notes must finish without missed hits.");
            Check(game.Music.DroppedNotes==0,"Every accepted musical hit must retain its scheduled sound.");
        }
        IEnumerator Start()
        {
            while(!game.Ready) yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            ResetRoom();
            var combat=game.Combat;
            Vector3 center=CaveLayout.Rooms[1].Center;
            float song=(float)game.Music.Time;
            int first=combat.Targets.Count;
            Check(combat.RegisterSpiralPair(center+Vector3.forward*54,Vector3.back,song,Blue,Cyan,this)==6,
                "Two simultaneous spiral groups must register exactly six targets.");
            var spirals=new List<LockTarget>();
            for(int i=first;i<combat.Targets.Count;i++) spirals.Add(combat.Targets[i]);
            for(int g=0;g<2;g++) {
                Vector3 average=Vector3.zero;
                for(int i=0;i<3;i++) average+=Encounter.PressurePosition(spirals[g*3+i],.5f);
                average/=3;
                Check(Vector3.Distance(average,spirals[g*3].origin+Vector3.back*16)<.001f,
                    "Three helix phases must remain balanced around their moving axis.");
                for(int i=0;i<3;i++)
                    Check(Mathf.Abs(Vector3.Distance(Encounter.PressurePosition(spirals[g*3+i],.5f),average)-2.7f)<.001f,
                        "Helix radius must not collapse or diverge.");
            }
            var pod=combat.RegisterPressurePod(center+Vector3.forward*40,Vector3.back,song,Blue,this);
            Check(pod!=null && pod.hp==4 && pod.lockCapacity==4 && pod.pressureSpeed<spirals[0].pressureSpeed,
                "A large slow pod must accept four locks, without recursive splitting.");
            Capture("six-spirals-and-pod.png");
            foreach(var target in spirals) Check(combat.Acquire(target),"Each spiral bullet must be lockable.");
            Check(combat.Acquire(pod) && combat.Acquire(pod) && combat.Locks.Count==8,"Six spirals plus two pod locks must fill one musical volley.");
            int hits=combat.Hits,notes=game.Music.ScheduledNotes;
            combat.Release();yield return Resolve();
            Check(combat.Hits-hits==8 && game.Music.ScheduledNotes-notes==8 && pod.hp==2,
                "Eight scheduled musical hits must preserve the pod's remaining two HP.");
            int spawned=combat.SpawnedPressureShots;
            Check(combat.Acquire(pod) && combat.Acquire(pod) && !combat.Acquire(pod),"Remaining HP must bound multi-lock count.");
            combat.Release();yield return Resolve();
            Check(combat.SpawnedPressureShots-spawned==4,"Lethal pod interception must release exactly four children once.");
            report.gridError=Math.Max(report.gridError,game.Music.MaxGridError);
            foreach(var target in combat.Targets)
                if(target.pressurePattern==3) Check(target.hp==1 && target.lockCapacity==1,"Children must never inherit splitting or multi-lock.");
            report.spiralCount=6;report.podChildren=4;

            ResetRoom();song=(float)game.Music.Time;
            pod=combat.RegisterPressurePod(center+Vector3.forward*42,Vector3.back,song,Blue,this);
            for(int i=0;i<20;i++)
                Check(combat.RegisterPressureShot(center+Vector3.forward*(70+i),Vector3.forward,song,Blue,this,1,
                    lifetimeOverride:12)!=null,"The reserved-child pressure budget must fit twenty ordinary bullets.");
            Check(!combat.CanRegisterPressureShots(1) &&
                combat.RegisterSpiralPair(center+Vector3.forward*55,Vector3.back,song,Blue,Cyan,this)==0,
                "Shared pressure cap must defer the entire six-shot burst, not partially fire.");
            game.Flight.EnableVr(true);game.Flight.SetVrHeadPose(Vector3.zero,Quaternion.identity);
            for(int i=0;i<4;i++) Check(combat.Acquire(pod),"VR must acquire all four locks on one pod.");
            Check(!combat.Acquire(pod),"VR must not overbook pod HP.");
            combat.Release();yield return Resolve();
            Check(combat.LivePressureShots(this)==24,"Four reserved children must still appear at the shared twenty-four-shot cap.");
            foreach(var target in combat.Targets) Check(target.reserved==0,"All completed reservations must be cleared.");
            combat.ClearPressureShots(this);
            Check(combat.LivePressureShots(this)==0 && combat.CanRegisterPressureShots(6),"Owner cleanup must release pod and child budgets.");
            report.pressureCap=24;report.vrMultiLock=true;
            report.gridError=Math.Max(report.gridError,game.Music.MaxGridError);

            ResetRoom();song=(float)game.Music.Time;
            Check(combat.Puffers.TrySpawn(center+Vector3.forward*52,song),"Puffer must spawn in the serpent room.");
            LockTarget puffer=combat.Puffers.LiveTargets[0];
            float smallScale=puffer.visual.transform.localScale.x;
            Check(puffer.hp==16 && puffer.lockCapacity==8,"Puffer must have sixteen HP and eight stackable locks.");
            Capture("puffer-small.png");
            int before=combat.Hits;
            for(int i=0;i<8;i++) Check(combat.Acquire(puffer),"First puffer volley must accept eight locks.");
            Check(!combat.Acquire(puffer),"Puffer volley must remain capped at eight locks.");
            combat.Release();yield return Resolve();
            yield return new WaitForSecondsRealtime(.4f);
            Check(puffer.hp==8 && puffer.visual.transform.localScale.x>smallScale,"Each hit must inflate the same puffer body.");
            Check(puffer.visual.transform.localScale.x<PufferfishEncounter.ScaleForHits(8)+.001f,
                "Rapid consecutive hits must inflate smoothly without overshooting accumulated damage.");
            Capture("puffer-inflated.png");
            for(int i=0;i<8;i++) Check(combat.Acquire(puffer),"Second puffer volley must accept the remaining eight locks.");
            combat.Release();yield return Resolve();
            float burstDeadline=Time.realtimeSinceStartup+3;
            while(combat.Puffers.Bursts==0 && Time.realtimeSinceStartup<burstDeadline) yield return null;
            Check(combat.Hits-before==16 && combat.Puffers.Bursts==1,"Exactly sixteen real musical hits must burst one puffer.");
            report.radialShots=combat.LivePressureShots(combat.Puffers);
            Check(report.radialShots==36,"Puffer burst must produce thirty-six omnidirectional targets at twice the previous density.");
            foreach(var shot in combat.Targets) if(shot.pressurePattern==4) {
                Check(Mathf.Abs(Encounter.PressureVelocity(shot,0).magnitude-20)<.001f &&
                    Mathf.Abs(Encounter.PressureVelocity(shot,1).magnitude-10)<.001f,
                    "Puffer pressure must halve its speed over one second.");
                Check(Vector3.Distance(Encounter.PressurePosition(shot,1),shot.origin+shot.direction*15)<.001f,
                    "Decelerating pressure position must integrate speed continuously.");
            }
            Check(Mathf.Abs(PufferfishEncounter.ScaleForHits(16)-6.016f)<.001f,
                "Fully inflated puffer must be twice its previous maximum size.");
            Capture("puffer-radial-release.png");
            report.pufferHits=16;
            report.gridError=Math.Max(report.gridError,game.Music.MaxGridError);
            game.Flight.SetPose(CaveLayout.Rooms[0].Center,Quaternion.identity);
            yield return null;
            Check(combat.LivePressureShots(combat.Puffers)==0,"Leaving the serpent room must end puffer pressure.");
            game.Restart();
            Check(combat.Puffers.LiveTargets.Count==0 && !combat.HasPending && combat.Locks.Count==0,
                "Restart must remove puffer targets, locks, and scheduled damage.");
            Check(game.Music.DroppedNotes==0 && report.gridError<=1.0/AuthoredScore.Data.sampleRate,
                "New attacks must retain authored musical synchronization.");
            report.tidalMemory=game.Music.ActiveSoundtrack==game.soundtrack;
            Check(report.tidalMemory,"New attack work must retain TidalMemory.");
            ResetRoom();
            var wave=typeof(Encounter).GetMethod("SpawnWave",BindingFlags.Instance|BindingFlags.NonPublic);
            Check(wave!=null,"Serpent summon hook must exist.");
            for(int i=0;i<4;i++) {
                wave.Invoke(combat,new object[] {(float)game.Music.Time,16+i*32});
                Check(combat.Puffers.Spawned==(i<2?1:2),"Puffer must now appear once in two serpent summons, starting with the first.");
            }
            report.rareWave=true;
            ResetRoom();
            Vector3 source=center+Vector3.up*70;
            var matter=PointCloud.Place("Transfer proof",game.World.EnemyMesh,game.World.NodeMaterial,game.World.transform);
            matter.transform.position=source;matter.transform.localScale=Vector3.one*.45f;
            song=(float)game.Music.Time;
            Check(combat.Colonies.TryAdopt(matter,song,1,Vector3.forward*.44f),"Defeated ray must retain its own particle mesh during transfer.");
            float distance=Vector3.Distance(source,combat.Colonies.RootAt(0));
            report.transferSeconds=combat.Colonies.TransferDurationAt(0);
            report.transferDistance=distance;
            Check(report.transferSeconds>=2+distance/8f && !combat.Colonies.IsSettled(0,song+7.4f),
                "A distant floor must no longer force the entire morph into seven seconds.");
            Check(DefeatedMarineForms.TransferSeconds(400)>DefeatedMarineForms.TransferSeconds(40),
                "Transfer time must grow with distance.");
            game.Flight.SetPose(source+new Vector3(0,4,-22),Quaternion.LookRotation(source-(source+new Vector3(0,4,-22))));
            combat.Colonies.Tick(song+1.1f);game.World.Tick(song+1.1f,.25f,0,true);
            Capture("ray-turbulent-peel.png");
            combat.Colonies.Tick(song+report.transferSeconds*.4f);
            Check(matter.activeSelf && matter.GetComponent<MeshFilter>().sharedMesh==game.World.EnemyMesh,
                "Distance-scaled transfer must keep the same particle identity.");
            Finish();
        }
        void Capture(string name)
        {
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);target.Create();
            RenderPipeline.SubmitRenderRequest(game.Flight.View,new RenderPipeline.StandardRequest {destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            RenderTexture.active=previous;Destroy(image);target.Release();Destroy(target);
        }
        void Finish()
        {
            if(finished) return;finished=true;
            report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:2);
        }
        void OnDestroy() => Application.logMessageReceived-=OnLog;
        [Serializable] sealed class Report
        {
            public bool passed,vrMultiLock,tidalMemory,rareWave;
            public int spiralCount,podChildren,pressureCap,pufferHits,radialShots;
            public float transferSeconds,transferDistance;
            public double gridError;
            public string[] errors;
        }
    }
}
