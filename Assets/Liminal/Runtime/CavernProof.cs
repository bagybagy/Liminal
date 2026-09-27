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
        bool particleIdentityStable=true,fishReassembled,permanentGarden,whaleReleased;
        int persistentCount,settledParticles;
        float jellyDisplacement;
        float settlementError;
        float whaleMinSpeed=float.MaxValue,whaleMaxSpeed,whaleVerticalSpan;
        int surfaceCrossings,waveEvents;
        bool whaleFits,serpentRearmed,whaleRearmed;
        float whaleColorChange,whaleTrackingError;
        float whaleSettlementError;
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
            MatterParticle[] initialMatter=experience.Marine.Matter.Readback();
            int initializations=experience.Marine.Matter.InitializationCount;
            int serpentInitializations=experience.World.Serpent.InitializationCount;
            persistentCount=initialMatter.Length;
            Require(persistentCount>10000,"Marine forms must use the persistent GPU pool");
            Capture("01-lantern-grotto.png");
            CheckWhaleRoute();
            yield return InspectHorizon();
            Frame(CaveLayout.Spawn+Vector3.forward*30,new Vector3(0,0,-30));
            var firstJelly=new[] {experience.Marine.JellyTargets[0]};
            yield return HitTargets(firstJelly,1);
            jellyHits=experience.Marine.IlluminatedJellies;
            Require(jellyHits>0,"Jelly shot must illuminate a lantern");
            Capture("02-lantern-lit.png");
            yield return HitTargets(firstJelly,1);
            Require(experience.Marine.CompletedJellies==1 && !firstJelly[0].Available,"Second jelly hit must permanently finish its target");
            yield return new WaitForSecondsRealtime(1.2f);
            Capture("02b-jelly-transfer.png");
            yield return new WaitForSecondsRealtime(10f);
            MatterParticle[] garden=experience.Marine.Matter.Readback();
            CheckMatter(initialMatter,garden);
            Vector3 gardenCenter=Vector3.zero;int gardenCount=0;
            for(int i=0;i<garden.Length;i++) if(garden[i].identityState.z==(float)MatterPhase.Settled) {
                gardenCenter+=(Vector3)garden[i].positionAge;
                jellyDisplacement+=Vector3.Distance(initialMatter[i].positionAge,garden[i].positionAge);
                settlementError+=Vector3.Distance(experience.Marine.SeedAt(i).destination,garden[i].positionAge);
                gardenCount++;
            }
            jellyDisplacement/=Mathf.Max(1,gardenCount);
            settlementError/=Mathf.Max(1,gardenCount);
            permanentGarden=experience.Marine.SettledJellies==1 && gardenCount>100 && !firstJelly[0].Available;
            Require(permanentGarden && jellyDisplacement>8,"The same jelly particles must settle into a new world-space garden");
            Require(settlementError<1f,"Settled jelly particles must actually reach the plant, not only change a state flag");
            if(gardenCount>0) Frame(gardenCenter/gardenCount,new Vector3(26,12,-40));
            Capture("02c-jelly-garden.png");

            Require(experience.Marine.FishTargets.Count>=84,"Fish must be individually targetable, not one target per school");
            var fish=new List<LockTarget>();
            for(int i=0;i<8;i++) fish.Add(experience.Marine.FishTargets[i]);
            yield return HitTargets(fish,8);
            fishResponses=experience.Marine.FishResponses;
            Require(fishResponses>0 && experience.Marine.FishScatteringCount>0,"Fish must flee a scheduled impact");
            yield return new WaitForSecondsRealtime(1f);
            Capture("02d-fish-scatter.png");
            yield return new WaitForSecondsRealtime(8f);
            float regroupDeadline=Time.realtimeSinceStartup+5f;
            while(!fish.TrueForAll(target=>target.Available) && Time.realtimeSinceStartup<regroupDeadline) yield return null;
            fishReassembled=fish.TrueForAll(target=>target.Available) && experience.Marine.FishScatteringCount==0;
            Require(fishReassembled,"Fish must reassemble and rearm after a visible escape");

            Vector3 focus=Anatomy.Focus((float)experience.Music.Time);
            flight.SetPose(focus+new Vector3(35,12,-64),Quaternion.LookRotation(new Vector3(-35,-12,64)));
            yield return new WaitForSecondsRealtime(.4f);
            Capture("03-serpent-sanctum.png");
            var organs=new List<LockTarget>();
            foreach(var target in experience.Combat.Targets) if(target.kind==TargetKind.Organ) organs.Add(target);
            for(int round=0;round<5;round++) {
                if(round>0) {
                    yield return WaitForRearm(organs);
                    serpentRearmed=organs.TrueForAll(t=>t.Available);
                    Require(serpentRearmed,"Every serpent scale must rearm between complete rounds");
                    Require(experience.World.Serpent.OrganHeat(0)==0,"Serpent scale color must reset with its target");
                }
                yield return HitTargets(organs,8);
                if(round==0) {
                    Require(!organs[0].Available && organs[8].Available,"Only struck serpent scales must be spent");
                    Require(experience.World.Serpent.OrganHeat(0)>.9f && experience.World.Serpent.OrganHeat(8)==0,
                        "Serpent hit warmth must be strong and localized to the struck scales");
                    Frame(Anatomy.Center(.3f,(float)experience.Music.Time),new Vector3(40,20,-70));
                    Capture("03a-serpent-scales.png");
                }
                yield return HitTargets(organs,8);
            }
            serpentHits=experience.Combat.BossDamage;
            Require(experience.Combat.SerpentComplete && serpentHits==80 && !experience.Combat.Ended,
                "Serpent's 80 hits must complete its stage without ending exploration");
            yield return new WaitForSecondsRealtime(1.5f);
            Capture("03b-serpent-transfer.png");
            yield return new WaitForSecondsRealtime(13f);
            Require(experience.World.Serpent.ReleaseSettled && experience.World.Serpent.InitializationCount==serpentInitializations &&
                experience.World.Serpent.ParticleCount==LeviathanVfx.SimulatedParticles,
                "Serpent must complete its release without replacing its GPU pool");
            Frame(CaveLayout.Rooms[1].Center,new Vector3(95,15,-155));
            Capture("03c-serpent-current.png");

            Vector3 whale=experience.Marine.WhalePosition;
            Vector3 whaleView=whale+experience.Marine.WhaleRotation*new Vector3(250,45,50);
            flight.SetPose(whaleView,Quaternion.LookRotation(whale-whaleView));
            yield return new WaitForSecondsRealtime(.4f);
            Capture("04-horizon-whale.png");
            var whaleOrgans=experience.Marine.WhaleResonatorTargets;
            Require(whaleOrgans.Count==16,"Whale must have sixteen independently aimed organs");
            for(int round=0;round<5;round++) {
                if(round>0) {
                    yield return WaitForRearm(whaleOrgans);
                    whaleRearmed=true;
                    foreach(var target in whaleOrgans) whaleRearmed&=target.Available;
                    Require(whaleRearmed,"Whale must rearm all sixteen organs between rounds");
                    Require(experience.Marine.WhaleOrganHeat(0)==0,"Whale hit patches must reset with their targets");
                }
                yield return HitTargets(whaleOrgans,8);
                if(round==0) {
                    Require(!whaleOrgans[0].Available && whaleOrgans[8].Available,"Whale hit patches must match spent organs");
                    Require(experience.Marine.WhaleOrganHeat(0)>=.45f && experience.Marine.WhaleOrganHeat(8)==0,
                        "Whale hit color must persist locally until the next round");
                    Frame(experience.Marine.WhalePosition,experience.Marine.WhaleRotation*new Vector3(240,75,25));
                    Capture("04a-whale-hit-patches.png");
                }
                yield return HitTargets(whaleOrgans,8);
            }
            whaleHits=experience.Marine.WhaleResonance;
            Require(whaleHits==MarineLife.WhaleDamageGoal && !experience.Combat.Ended,"Whale must require eighty scheduled hits without blocking flight");
            yield return new WaitForSecondsRealtime(1f);
            Capture("04b-whale-release.png");
            yield return new WaitForSecondsRealtime(11f);
            whaleReleased=experience.Marine.WhaleReleased;
            foreach(var target in experience.Marine.WhaleResonatorTargets) whaleReleased&=!target.Available;
            Require(whaleReleased,"Whale must release its particles and permanently retire all sixteen organs");
            MatterParticle[] finalMatter=experience.Marine.Matter.Readback();
            Require(experience.Marine.Matter.InitializationCount==initializations,"Persistent pool must not be reinitialized during phase transitions");
            CheckMatter(initialMatter,finalMatter);
            foreach(var particle in finalMatter) if(particle.identityState.z==(float)MatterPhase.Settled) settledParticles++;
            Require(settledParticles>gardenCount+1000,"Whale particles must remain in their new form, not disappear");
            int whaleSettledCount=0;
            for(int i=0;i<finalMatter.Length;i+=47) if(finalMatter[i].identityState.y==experience.Marine.WhaleGroup) {
                whaleSettlementError+=Vector3.Distance(experience.Marine.SeedAt(i).destination,finalMatter[i].positionAge);
                whaleSettledCount++;
            }
            whaleSettlementError/=Mathf.Max(1,whaleSettledCount);
            Require(whaleSettlementError<2,"Whale release must physically reach its reef destinations in the expanded room");
            Frame(CaveLayout.Rooms[2].Center+Vector3.down*60,new Vector3(100,50,-150));
            Capture("04c-whale-memory.png");
            Require(experience.Music.MaxGridError<.00001,"Hits must remain aligned with authored soundtrack");
            double gridError=experience.Music.MaxGridError;
            Require(experience.Combat.MissedScheduledHits==0,"Neighbour reactions must not invalidate already scheduled hits");
            experience.TogglePause();
            double song=experience.Music.Time;
            yield return new WaitForSecondsRealtime(.15f);
            pauseStable=Math.Abs(experience.Music.Time-song)<.005;
            Require(pauseStable,"Pause must freeze song time");
            experience.TogglePause();
            experience.Restart();
            yield return null;
            restartClean=experience.Combat.BossDamage==0 && experience.Marine.WhaleResonance==0 &&
                experience.Marine.IlluminatedJellies==0 && !experience.Combat.HasPending &&
                Vector3.Distance(flight.Position,CaveLayout.Spawn)<.01f && !experience.Marine.WhaleReleased &&
                experience.Marine.CompletedJellies==0 && !experience.World.Serpent.Released;
            int afterRestart=experience.Combat.Targets.Count;
            experience.Restart();
            Require(restartClean && experience.Combat.Targets.Count==afterRestart,"Restart must reset and not duplicate fauna targets");
            Require(renderedFractions.Count>=16 && renderedFractions.TrueForAll(x=>x>.0005f && x<.95f),"Every chamber and transition must render visible, nonblank content");
            var report=new Report {passed=errors.Count==0,halfSpeed=halfSpeed,cruiseSpeed=cruiseSpeed,coastSpeed=coastSpeed,
                passageTravel=passageTravel,jellyHits=jellyHits,fishResponses=fishResponses,serpentHits=serpentHits,whaleHits=whaleHits,
                pauseStable=pauseStable,restartClean=restartClean,gridError=gridError,
                particleIdentityStable=particleIdentityStable,persistentCount=persistentCount,settledParticles=settledParticles,
                jellyDisplacement=jellyDisplacement,permanentGarden=permanentGarden,fishReassembled=fishReassembled,whaleReleased=whaleReleased,
                settlementError=settlementError,
                whaleMinSpeed=whaleMinSpeed,whaleMaxSpeed=whaleMaxSpeed,whaleVerticalSpan=whaleVerticalSpan,
                whaleFits=whaleFits,serpentRearmed=serpentRearmed,whaleRearmed=whaleRearmed,surfaceCrossings=surfaceCrossings,waveEvents=waveEvents,
                whaleColorChange=whaleColorChange,whaleTrackingError=whaleTrackingError,
                whaleSettlementError=whaleSettlementError,
                renderedFractions=renderedFractions.ToArray(),errors=errors.ToArray(),gpu=SystemInfo.graphicsDeviceName};
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LIMINAL_CAVERNS_PROOF "+JsonUtility.ToJson(report));
            Application.Quit(report.passed?0:2);
        }

        void CheckWhaleRoute()
        {
            whaleFits=true;
            float low=float.MaxValue,high=float.MinValue;
            for(float t=0;t<280;t+=.25f) {
                MarineLife.EvaluateWhalePose(t,out Vector3 p,out Quaternion q);
                MarineLife.EvaluateWhalePose(t+.02f,out Vector3 next,out _);
                float speed=(next-p).magnitude/.02f;
                whaleMinSpeed=Mathf.Min(whaleMinSpeed,speed);whaleMaxSpeed=Mathf.Max(whaleMaxSpeed,speed);
                low=Mathf.Min(low,p.y);high=Mathf.Max(high,p.y);
                foreach(var local in new[] {Vector3.zero,new Vector3(0,0,77),new Vector3(0,0,-85),new Vector3(58,-5,-24),new Vector3(-58,-5,-24)})
                    whaleFits&=CaveLayout.RoomDistance(2,p+q*(MarineLife.DeformWhaleLocal(local,t)*1.8f))<.99f;
            }
            whaleVerticalSpan=high-low;
            Require(whaleFits,"Whale body and fins must remain inside the expanded chamber throughout its orbit");
            Require(whaleMinSpeed>12 && whaleMaxSpeed<24,"Whale must swim purposefully but remain catchable at normal cruise");
            Require(whaleVerticalSpan>150 && high>CaveLayout.HorizonSurfaceY,"Whale must rise through the second horizon then dive");
        }

        IEnumerator InspectHorizon()
        {
            Require(experience.Horizon && experience.Horizon.Ready,"Horizon surface and spray must initialize");
            Vector3 firstColor=Vector3.zero;
            for(int shot=0;shot<3;shot++) {
                float at=shot==0?5:shot==1?14:24;
                while(experience.Music.Time<at) yield return null;
                Vector3 whale=experience.Marine.WhalePosition;
                Frame(whale,experience.Marine.WhaleRotation*new Vector3(320,85,-30));
                yield return null;
                Capture("00"+shot+"-whale-surface-motion.png");
                var particles=experience.Marine.Matter.Readback();
                Vector3 color=Vector3.zero;float error=0;int count=0;
                var pose=Matrix4x4.TRS(experience.Marine.WhalePosition,experience.Marine.WhaleRotation,Vector3.one*1.8f);
                for(int i=0;i<particles.Length;i+=47) if(particles[i].identityState.y==experience.Marine.WhaleGroup) {
                    color+=(Vector3)particles[i].colorSize;
                    Vector3 local=experience.Marine.SeedAt(i).form;
                    Vector3 desired=pose.MultiplyPoint3x4(MarineLife.DeformWhaleLocal(local,(float)experience.Music.Time));
                    error+=Vector3.Distance(desired,particles[i].positionAge);count++;
                }
                color/=Mathf.Max(1,count);error/=Mathf.Max(1,count);
                whaleTrackingError=Mathf.Max(whaleTrackingError,error);
                if(shot==0) firstColor=color;
                else whaleColorChange=Mathf.Max(whaleColorChange,Vector3.Distance(color,firstColor));
            }
            surfaceCrossings=experience.Horizon.SurfaceCrossings;
            waveEvents=experience.Horizon.WaveEventCount;
            Require(surfaceCrossings>0 && waveEvents>0,"Actual whale motion must create surface crossings, waves and spray");
            Require(whaleColorChange>.025f,"Whale GPU body color must change with its motion");
            Require(whaleTrackingError<7,"Whale GPU body and fins must track their CPU anatomy at chase speed");
        }

        IEnumerator WaitForRearm(IReadOnlyList<LockTarget> targets)
        {
            float deadline=Time.realtimeSinceStartup+4;
            while(Time.realtimeSinceStartup<deadline) {
                bool all=true;
                foreach(var target in targets) all&=target.Available;
                if(all) yield break;
                yield return null;
            }
        }

        void Frame(Vector3 focus,Vector3 offset)
        {
            Vector3 velocity=Vector3.zero;
            Vector3 position=CaveLayout.Constrain(focus+offset,ref velocity);
            experience.Flight.SetPose(position,Quaternion.LookRotation(focus-position));
        }

        void CheckMatter(MatterParticle[] before,MatterParticle[] after)
        {
            particleIdentityStable&=before.Length==after.Length;
            for(int i=0;i<Mathf.Min(before.Length,after.Length);i++) {
                particleIdentityStable&=before[i].identityState.x==after[i].identityState.x &&
                    before[i].identityState.y==after[i].identityState.y;
                Vector3 position=after[i].positionAge;
                if(float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude)) {
                    Require(false,"Persistent particle positions must remain finite");break;
                }
            }
            Require(particleIdentityStable,"All particle IDs and pool membership must survive phase changes");
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
            public bool particleIdentityStable,permanentGarden,fishReassembled,whaleReleased;
            public int persistentCount,settledParticles;
            public float jellyDisplacement;
            public float settlementError;
            public float whaleMinSpeed,whaleMaxSpeed,whaleVerticalSpan;
            public int surfaceCrossings,waveEvents;
            public bool whaleFits,serpentRearmed,whaleRearmed;
            public float whaleColorChange,whaleTrackingError;
            public float whaleSettlementError;
            public float halfSpeed,cruiseSpeed,coastSpeed;
            public int jellyHits,fishResponses,serpentHits,whaleHits;
            public double gridError;
            public float[] renderedFractions;
            public string[] errors;
            public string gpu;
        }
    }
}
