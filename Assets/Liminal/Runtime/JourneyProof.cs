using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class JourneyProof : MonoBehaviour
    {
        Experience game;
        string output;
        float started;
        bool finished;
        readonly List<string> errors=new();
        readonly Report report=new();
        public void Initialize(Experience experience)
        {
            game=experience;started=Time.realtimeSinceStartup;
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--output");
            output=at>=0&&at+1<args.Length?args[at+1]:Path.GetFullPath("Verification/Journey");
            Directory.CreateDirectory(output);Application.logMessageReceived+=OnLog;
        }
        void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)
                if(errors.Count<24) errors.Add(message+"\n"+stack);
        }
        void Update() {if(!finished && Time.realtimeSinceStartup-started>480){Check(false,"Journey timeout");Finish();}}
        void Check(bool condition,string message)
        {
            if(condition) return;
            errors.Add(message);Finish();
            throw new InvalidOperationException("Journey assertion: "+message);
        }
        IEnumerator Start()
        {
            yield return null;
            game.Tutorial.SetEnabled(false);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--vr-motion-only")>=0) {
                yield return InspectVrPath();
                game.Vr.RequestEnable();
                float vrDeadline=Time.realtimeSinceStartup+12;
                while(game.Vr.Starting && Time.realtimeSinceStartup<vrDeadline) yield return null;
                report.vrDesktopFallback=!game.Vr.Enabled&&!game.Flight.VrEnabled&&!game.Vr.Starting;
                Check(report.vrDesktopFallback,"Unavailable OpenXR runtime must return cleanly to desktop");
                Finish();yield break;
            }
            report.graphEdges=CaveLayout.Passages.Length;
            Check(report.graphEdges==7,"Room graph must contain seven bidirectional connections");
            for(int p=0;p<CaveLayout.Passages.Length;p++) {
                CaveLayout.GetPortal(p,true,out var a,out var axis);
                CaveLayout.GetPortal(p,false,out var b,out _);
                Check(Mathf.Abs(CaveLayout.RoomDistance(CaveLayout.FromRoom(p),a)-1)<.001f,"Source portal must be a real wall opening");
                Check(Mathf.Abs(CaveLayout.RoomDistance(CaveLayout.ToRoom(p),b)-1)<.001f,"Destination portal must be a real wall opening");
                var route=CaveLayout.Passages[p];
                for(int s=1;s<route.Length;s++) {
                    game.Flight.SetPose(route[s-1],Quaternion.LookRotation(route[s]-route[s-1]));
                    int bound=Mathf.CeilToInt(Vector3.Distance(route[s-1],route[s])/24*60)+180;
                    for(int k=0;k<bound && Vector3.Distance(game.Flight.Position,route[s])>1;k++)
                        game.Flight.Step(0,1f/60,Vector3.forward,Vector2.zero,false);
                    Check(Vector3.Distance(game.Flight.Position,route[s])<2,"Swimmer must traverse graph edge "+p);
                }
                Frame(a-axis*60,a);
                yield return null;
                Capture("passage-"+p+".png");
            }
            report.corridorFish=game.PassageGuide.FishCount;
            Check(report.corridorFish==7*3*7,"Every route must retain three schools of seven fish");
            game.Restart();game.Tutorial.SetEnabled(false);
            yield return new WaitForSecondsRealtime(.4f);
            Check(game.Music.ThemeCount==6 && (game.Music.StageMusicEnabled ? game.Music.CurrentTheme==0 :
                game.Music.CurrentTheme==-1 && game.Music.ActiveSoundtrack==game.soundtrack),
                "Music must respect the opt-in stage-theme policy");
            game.Music.RequestTheme(1);
            Frame(Anatomy.Focus((float)game.Music.Time)+new Vector3(25,20,-45),Anatomy.Focus((float)game.Music.Time));
            yield return null;
            yield return BeatBoss(game.Combat.Targets,()=>game.Combat.SerpentComplete,
                target=>target.kind==TargetKind.Organ,45);
            yield return null;
            Check(game.Progress.Has(BossId.Serpent),"Serpent defeat must be recorded once");
            Check(!game.Progress.Record(BossId.Serpent),"Revisiting a defeated boss must not count it twice");

            game.Music.RequestTheme(3);
            int smallGoal=8*4;
            while(game.Hermits.SmallDefeated<8 && smallGoal>0) {
                yield return Shoot(game.Hermits.SmallTargets,8,null);smallGoal-=8;
            }
            Check(game.Hermits.SmallDefeated==8,"Eight crabs need four distinct musical impacts each");
            yield return new WaitForSecondsRealtime(4.5f);
            Frame(game.Hermits.BossPosition+new Vector3(90,65,-155),game.Hermits.BossPosition+Vector3.up*35);
            float ringDeadline=Time.realtimeSinceStartup+5;
            while(game.Hermits.GiantBubbleRings==0 && Time.realtimeSinceStartup<ringDeadline) yield return null;
            Capture("giant-hermit-arc.png");
            foreach(var target in game.Combat.Targets)
                if(target.isPressureShot && target.pressureOwner==game.Hermits && target.pressureRing)
                    report.ballisticRings|=target.pressureAcceleration.y<0 && target.direction.y>0;
            Check(report.ballisticRings,"Giant rings must be launched upward with downward acceleration");
            yield return BeatBoss(game.Hermits.BossTargets,()=>game.Hermits.Complete,null,40);
            report.hermitHits=game.Hermits.BossHits;
            Check(report.hermitHits==48,"Giant hermit must accept forty-eight normally scheduled impacts");
            yield return new WaitForSecondsRealtime(8f);
            Frame(game.Hermits.BossPosition+new Vector3(140,90,-220),game.Hermits.BossPosition+Vector3.up*30);
            yield return null;Capture("shell-reef.png");
            Check(game.Progress.Has(BossId.Hermit)&&game.Hermits.ReefParticleGroups==24,
                "All crab groups must join the same reef and record the boss");
            Check(game.Hermits.ReefFishActive && game.Hermits.ReefFishCount==16 && game.Hermits.ReefParticleCount==76800,
                "Completed shell reef must keep crab matter and a swimming shoal");

            game.Music.RequestTheme(4);
            Frame(CaveLayout.Rooms[4].Center+new Vector3(0,20,-150),CaveLayout.Rooms[4].Center);
            yield return null;
            for(int phase=0;phase<3;phase++) {
                if(phase==2) for(int point=0;point<32;point++)
                    Check(SubmarineGeometry.GiantTarget(point,out _).y>=-220,
                        "Poseidon targets must remain above the waist");
                yield return BeatBoss(game.Submarines.Targets,()=>game.Submarines.Phase!=phase||game.Submarines.Complete,null,45);
                yield return new WaitForSecondsRealtime(4.2f);
                if(phase<2) {
                    Frame(game.Submarines.Focus+new Vector3(100,60,-160),game.Submarines.Focus);
                    yield return null;Capture("submarine-form-"+(phase+1)+".png");
                }
            }
            report.submarineHits=game.Submarines.TotalHits;
            Check(game.Submarines.Complete&&report.submarineHits==160&&game.Progress.Has(BossId.Submarine),
                "All submarine forms must remain beatable and record the boss");
            Check(game.Progress.OptionalCount==3,"Defeat count must derive from boss IDs, not visited rooms");

            Check(!game.Marine.WhaleVisible,"Whale must remain invisible until its gyre is triggered");
            game.Music.RequestTheme(2);
            game.Flight.SetPose(game.Marine.Arrival.Origin+new Vector3(0,10,-140),Quaternion.identity);
            yield return new WaitForSecondsRealtime(2.6f);
            Check(!game.Marine.WhaleVisible,"Whale must stay hidden before the gyre's radiance peak");
            Vector3 beforePeak=game.Marine.WhalePosition;
            yield return new WaitForSecondsRealtime(.7f);
            Check(game.Marine.WhaleVisible&&Vector3.Distance(beforePeak,game.Marine.WhalePosition)>10,
                "Whale must emerge moving, not pause below the vortex");
            Frame(game.Marine.Arrival.Origin+new Vector3(0,35,-185),game.Marine.WhalePosition);
            Capture("whale-peak-breach.png");
            yield return new WaitForSecondsRealtime(8f);
            yield return InspectInheritance();
            yield return BeatBoss(game.Marine.WhaleResonatorTargets,()=>game.Marine.WhaleReleased,null,60);
            yield return null;
            game.Music.RequestTheme(2,true);
            Check(game.Progress.EndingStarted&&game.Progress.EndingMask==RunProgress.OptionalBosses,
                "Whale defeat must freeze the selected Atlantis layers");
            Check(game.Combat.Peaceful&&!game.Combat.Ended,"Ending must permit free flight without hostile fire");
            yield return new WaitForSecondsRealtime(20f);
            Frame(AtlantisGeometry.CityOrigin+new Vector3(230,150,-420),AtlantisGeometry.CityOrigin+Vector3.up*22);
            yield return null;Capture("atlantis-all-layers.png");
            yield return InspectFinaleVariants();
            report.musicTransitions=game.Music.TransitionCount;
            report.playbackPhaseError=game.Music.MaxPlaybackPhaseError;
            if(game.Music.StageMusicEnabled) {
                Check(game.Music.CurrentTheme==5&&report.musicTransitions>=5,"Each room and ending must switch themes on the shared clock");
                Check(Math.Abs(game.Music.LastTransitionTime/(16*Score.BeatSeconds)-Math.Round(game.Music.LastTransitionTime/(16*Score.BeatSeconds)))<.00001,
                    "Theme changes must start on a four-bar boundary");
            } else Check(game.Music.CurrentTheme==-1&&report.musicTransitions==0&&game.Music.ActiveSoundtrack==game.soundtrack,
                "Every room and the ending must keep the original soundtrack playing");
            double frozen=game.Music.Time;game.Music.SetPaused(true);
            yield return new WaitForSecondsRealtime(.25f);
            Check(Math.Abs(game.Music.Time-frozen)<.003,"Pause must freeze the DSP music clock");game.Music.SetPaused(false);
            report.hits=game.Combat.Hits;report.notes=game.Music.ScheduledNotes;report.gridError=game.Music.MaxGridError;
            Check(game.Combat.MissedScheduledHits==0&&game.Music.DroppedNotes==0&&report.gridError<.00003,
                "Branching battles and music changes must retain every accepted musical shot");
            yield return InspectVrPath();
            game.Restart();yield return null;
            Check(game.Progress.Defeated==BossId.None&&!game.Combat.Peaceful&&!game.Marine.WhaleReleased,
                "Restart must reset progress and leave peaceful ending mode");
            Check(new HashSet<LockTarget>(game.Combat.Targets).Count==game.Combat.Targets.Count,
                "Restart must not duplicate lock targets");
            Finish();
        }
        IEnumerator BeatBoss(IReadOnlyList<LockTarget> targets,Func<bool> done,Predicate<LockTarget> filter,float seconds)
        {
            float end=Time.realtimeSinceStartup+seconds;
            while(!done() && Time.realtimeSinceStartup<end && !game.Combat.Ended) {
                bool any=false;
                foreach(var target in targets) if(target.Available&&(filter==null||filter(target))) {any=true;break;}
                if(any) yield return Shoot(targets,8,filter);else yield return null;
            }
            Check(done(),"Boss did not complete through the normal lock-and-note path");
        }
        IEnumerator Shoot(IReadOnlyList<LockTarget> targets,int max,Predicate<LockTarget> filter)
        {
            int count=0;
            foreach(var target in targets) {
                if(!target.Available||(filter!=null&&!filter(target))) continue;
                var center=CaveLayout.Rooms[CaveLayout.NearestRoom(target.position)].Center;
                var offset=(center-target.position).normalized*30;
                if(offset.sqrMagnitude<1) offset=Vector3.back*30;
                Frame(target.position+offset,target.position);
                if(game.Combat.Acquire(target)) count++;
                if(count>=max) break;
            }
            Check(count>0,"Requested real lock targets must be reachable");
            game.Combat.Release();
            float end=Time.realtimeSinceStartup+4;
            while(game.Combat.HasPending && Time.realtimeSinceStartup<end) {
                game.Flight.Step((float)game.Music.Time,Mathf.Min(Time.unscaledDeltaTime,.05f),Vector3.right,Vector2.zero,true);
                yield return null;
            }
            Check(!game.Combat.HasPending,"Queued impacts must resolve");
        }
        void Frame(Vector3 position,Vector3 focus)
        {
            Vector3 velocity=Vector3.zero;
            position=CaveLayout.Constrain(position,ref velocity);
            game.Flight.SetPose(position,Quaternion.LookRotation(focus-position));
            game.Flight.View.transform.SetPositionAndRotation(position,Quaternion.LookRotation(focus-position));
        }
        IEnumerator InspectInheritance()
        {
            var inherited=game.Marine.Inheritance;
            Check(inherited.LoadoutFrozen && inherited.GrowthMask==RunProgress.OptionalBosses,
                "Whale loadout must freeze the three defeated boss flags at arrival");
            Check(Mathf.Abs(game.Marine.Dolphins.AttackRateMultiplier-2)<.001f,"Three optional bosses must cap attack frequency at twice base");
            yield return Shoot(inherited.SummonTargets,8,null);
            Check(inherited.SummonHits>=8,"Serpent inheritance must create shootable summons even after the original serpent is cleared");
            int spearGoal=24;
            float spearDeadline=Time.realtimeSinceStartup+15;
            while(inherited.SpearHits<spearGoal && !game.Combat.Ended && Time.realtimeSinceStartup<spearDeadline)
                yield return Shoot(inherited.SpearLockTargets,8,null);
            report.spearHits=inherited.SpearHits;
            Check(report.spearHits==24,"An eight-point spear device must break after three full locks");
            Check(inherited.ActiveGyres==1,"Destroying a spear must retire its own gyre");
            // These resonators also generate dolphins through the real whale impact callback.
            yield return Shoot(game.Marine.WhaleResonatorTargets,8,null);
            yield return Shoot(game.Marine.WhaleResonatorTargets,8,null);
            float deadline=Time.realtimeSinceStartup+10;
            while(inherited.RingShots==0 && Time.realtimeSinceStartup<deadline && !game.Combat.Ended) {
                game.Flight.Step((float)game.Music.Time,Mathf.Min(Time.unscaledDeltaTime,.05f),Vector3.right,Vector2.zero,true);
                yield return null;
            }
            report.inheritedRings=inherited.RingShots;report.inheritedCurtains=inherited.CurtainsFired;
            Check(report.inheritedRings>0&&report.inheritedCurtains>0,"Hermit rings and submarine curtains must both run in the whale chamber");
            Frame(game.Marine.WhalePosition+new Vector3(110,25,-150),game.Marine.WhalePosition);
            yield return null;Capture("whale-inheritance.png");
        }
        IEnumerator InspectFinaleVariants()
        {
            int initializations=game.Marine.Matter.InitializationCount;
            int particleCount=game.Marine.Matter.ParticleCount;
            float song=(float)game.Music.Time;
            for(int mask=0;mask<8;mask++) {
                game.Finale.Begin((BossId)mask,song);
                for(int step=0;step<182;step++) game.Finale.Tick(song+step*.1f,.1f);
                Check(game.Finale.Formation>.99f&&game.Finale.LayerCount==1+RunProgress.Count((BossId)mask),
                    "Atlantis must enable only the requested layers for mask "+mask);
                report.endingVariants++;
                if(mask==0) {yield return null;Capture("atlantis-base-only.png");}
            }
            report.creditParticles=game.Finale.Credits.StableParticleCount;
            for(int step=0;step<28;step++) game.Finale.Credits.Tick(song+step*.1f,.1f);
            Vector3 credits=AtlantisGeometry.CityOrigin+new Vector3(207,36,8);
            Frame(credits+Vector3.back*90,credits-Vector3.up*16);
            yield return null;Capture("particle-credits.png");
            // Exercise the 96-second circulation with bounded logical ticks; gameplay recording is not needed.
            for(int step=0;step<960;step++) game.Finale.Credits.Tick(song+step*.1f,.1f);
            report.creditCycles=game.Finale.Credits.CycleCount;
            Check(game.Finale.Credits.Completed&&report.creditCycles==8&&game.Finale.Credits.SeenLines==8,
                "All credits must rise, return to ambient matter, and finish once");
            Check(game.Finale.Credits.StableParticleCount==report.creditParticles&&report.creditParticles>0,
                "Credit cycles must keep one fixed particle pool");
            Check(game.Marine.Matter.InitializationCount==initializations&&game.Marine.Matter.ParticleCount==particleCount,
                "Finale destination morphs must not recreate whale particles");
        }
        IEnumerator InspectVrPath()
        {
            game.Flight.SetPose(CaveLayout.Rooms[2].Center,Quaternion.identity);
            game.Flight.EnableVr(true);
            var head=Quaternion.Euler(-18,35,0);
            Vector3 offset=new(.15f,1.65f,.12f);
            game.Flight.SetVrHeadPose(offset,head);
            Vector3 origin=game.Flight.Position,forward=head*Vector3.forward;
            float fieldOfView=game.Flight.View.fieldOfView;
            for(int frame=0;frame<42;frame++) game.Flight.StepVr(1f/60,forward,1,false);
            Check(Mathf.Abs(game.Flight.Speed-20)<.1f,"VR cruise must ramp to twenty units over 0.7 seconds");
            Check(Vector3.Dot((game.Flight.Position-origin).normalized,forward)>.999f,
                "VR translation must follow requested head-forward input");
            Check(Vector3.Distance(game.Flight.View.transform.localPosition,offset)<.001f &&
                Quaternion.Angle(game.Flight.View.transform.localRotation,head)<.001f&&
                Mathf.Abs(game.Flight.View.fieldOfView-fieldOfView)<.001f,
                "VR movement must preserve physical head pose and projection without camera banking");
            for(int frame=0;frame<42;frame++) game.Flight.StepVr(1f/60,forward,1,true);
            Check(Mathf.Abs(game.Flight.Speed-40)<.1f,"VR boost must follow the same gaze direction");
            for(int frame=0;frame<24;frame++) game.Flight.StepVr(1f/60,forward,0,false);
            report.vrMovement=game.Flight.Speed<.01f;
            Check(report.vrMovement,"Releasing VR movement must brake to rest");
            game.Flight.EnableVr(false);
            yield return null;
        }
        void Capture(string name)
        {
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);
            target.Create();
            RenderPipeline.SubmitRenderRequest(game.Flight.View,new RenderPipeline.StandardRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name),image.EncodeToPNG());
            RenderTexture.active=previous;Destroy(image);target.Release();Destroy(target);
        }
        void Finish()
        {
            if(finished) return;finished=true;
            if(errors.Count>0) {
                report.hits=game.Combat.Hits;report.notes=game.Music.ScheduledNotes;
                report.hermitHits=game.Hermits.BossHits;report.submarineHits=game.Submarines.TotalHits;
            }
            report.errors=errors.ToArray();report.passed=errors.Count==0;
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.passed?0:2);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        [Serializable] sealed class Report
        {
            public bool passed,ballisticRings,vrMovement,vrDesktopFallback;
            public int graphEdges,corridorFish,hermitHits,submarineHits,hits,notes,endingVariants;
            public int spearHits,inheritedRings,inheritedCurtains,musicTransitions,creditParticles,creditCycles;
            public double gridError,playbackPhaseError;
            public string[] errors;
        }
    }
}
