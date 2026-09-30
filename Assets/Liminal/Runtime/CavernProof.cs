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
        float whaleChaseSeconds, whaleChaseTravel, whaleChaseMaxSpeed;
        double playbackPhaseError;
        bool rayMatterRetained;
        bool arrivalTriggered,arrivalCompleted,arrivalSlammed,arrivalResetClean;
        bool arrivalPretriggerHidden,arrivalPretriggerTargetsUnavailable;
        bool dolphinSpawned,dolphinIdentityStable,dolphinMorphed,dolphinPressureIntercepted;
        bool dolphinRetired,dolphinPressureCleared,dolphinReefTransferred,dolphinAboveBoostSpeed;
        float arrivalApproachSeconds,arrivalTriggerDistance,arrivalCompletionAge,arrivalWaveAmplitude,arrivalWaveRadius;
        float arrivalChargeVisibility,arrivalBreachVisibility;
        float dolphinBirthAge,dolphinMatureAge,dolphinPoseDisplacement,dolphinPoseTravel;
        float dolphinGpuDisplacement,dolphinReefProgress,dolphinMinSpeed=float.MaxValue,dolphinMaxSpeed,dolphinSpeedWindow;
        int arrivalMajorWaves,dolphinParticleCount,dolphinSpawnedCount,dolphinNormalHits,dolphinPressureShots,dolphinPressureInterceptions;
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
            for(int reveal=0;reveal<6;reveal++) {
                yield return new WaitForSecondsRealtime(.28f);
                Capture("01"+reveal+"-grotto-visibility.png");
            }
            CheckWhaleRoute();
            arrivalResetClean=ArrivalIsDormant();
            Require(arrivalResetClean,"Restart must leave the whale arrival dormant, targets hidden, dolphins unspawned, and pressure shots absent");
            yield return InspectArrival();
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
            LockTarget ray=null;
            foreach(var target in experience.Combat.Targets) if(target.kind==TargetKind.Ray && target.Available) {ray=target;break;}
            Require(ray!=null,"Serpent stage must spawn a small enemy for the material transition test");
            if(ray!=null) {
                var originalMesh=ray.visual.GetComponent<MeshFilter>().sharedMesh;
                Frame(ray.position,new Vector3(9,6,-20));
                yield return HitTargets(new[]{ray},1);
                Require(ray.transformed && experience.Combat.Colonies.Count==1,"A killed ray must enter the colony pool");
                Vector3 rayFocus=ray.visual.transform.position;
                Vector3 rayOffset=(CaveLayout.Rooms[1].Center-rayFocus).normalized*18f;
                Frame(rayFocus,rayOffset);
                Capture("03da-ray-source-flash.png");
                yield return new WaitForSecondsRealtime(.35f);
                Capture("03db-ray-peeling.png");
                yield return new WaitForSecondsRealtime(.55f);
                Capture("03d-ray-transfer.png");
                yield return new WaitForSecondsRealtime(7.5f);
                rayMatterRetained=ray.visual && ray.visual.activeSelf &&
                    ray.visual.GetComponent<MeshFilter>().sharedMesh==originalMesh &&
                    experience.Combat.Colonies.IsSettled(0,(float)experience.Music.Time);
                Require(rayMatterRetained,"The defeated ray must retain its exact mesh and remain as settled matter");
                Frame(experience.Combat.Colonies.RootAt(0)+Vector3.up*5,new Vector3(14,9,-24));
                Capture("03e-ray-reef.png");
            }
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
            Require(whaleOrgans.Count==MarineLife.WhaleOrganCount && whaleOrgans.Count>=48,"Whale must expose dense independently aimed organs");
            int dolphinGroup=experience.Marine.DolphinGroupAt(0);
            dolphinParticleCount=experience.Marine.DolphinParticleCount;
            yield return HitTargets(new[]{whaleOrgans[8]},1);
            var dolphinTarget=FindDolphinTarget(0);
            var dolphinPose=experience.Marine.Dolphins.PoseAt(0);
            dolphinSpawned=experience.Marine.SpawnedDolphins==1 && dolphinPose.Active &&
                dolphinGroup>=0 && experience.Marine.DolphinGroupAt(0)==dolphinGroup && dolphinTarget!=null && dolphinTarget.Available;
            dolphinSpawnedCount=experience.Marine.SpawnedDolphins;
            Require(dolphinSpawned,"A normal hit on whale resonator 8 must spawn dolphin 0 and expose its real target");
            Require(dolphinParticleCount>0,"Dolphin forms must occupy the persistent GPU particle pool");
            Capture("04aa-whale-dolphin-birth-hit.png");
            yield return InspectDolphin(dolphinTarget,dolphinGroup);
            yield return ChaseWhale();
            whaleHits=experience.Marine.WhaleResonance;
            Require(whaleHits==MarineLife.WhaleDamageGoal && !experience.Combat.Ended,"Whale must be completed by continuous flight and central reticle aiming");
            Frame(experience.Marine.WhalePosition,experience.Marine.WhaleRotation*new Vector3(310,65,0));
            Capture("04ba-whale-source-flash.png");
            yield return new WaitForSecondsRealtime(.4f);
            Capture("04bb-whale-peeling.png");
            yield return new WaitForSecondsRealtime(.6f);
            Capture("04b-whale-release.png");
            yield return new WaitForSecondsRealtime(1.3f);
            Capture("04bc-whale-cloud.png");
            yield return new WaitForSecondsRealtime(9.7f);
            whaleReleased=experience.Marine.WhaleReleased;
            foreach(var target in experience.Marine.WhaleResonatorTargets) whaleReleased&=!target.Available;
            Require(whaleReleased,"Whale must release its particles and permanently retire all organs");
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
            Require(experience.Music.MaxGridError<.00001,"Hits must remain aligned with authored audio sample marks");
            double gridError=experience.Music.MaxGridError;
            playbackPhaseError=experience.Music.MaxPlaybackPhaseError;
            Require(playbackPhaseError<.08,"Actual AudioSource sample cursor must follow DSP transport within mixer buffer tolerance");
            Require(experience.Music.DroppedNotes==0,"Scheduled notes must not be silently dropped");
            CheckScoreLoops();
            Require(experience.Combat.MissedScheduledHits==0,"Neighbour reactions must not invalidate already scheduled hits");
            experience.TogglePause();
            double song=experience.Music.Time;
            yield return new WaitForSecondsRealtime(.15f);
            pauseStable=Math.Abs(experience.Music.Time-song)<.005;
            Require(pauseStable,"Pause must freeze song time");
            experience.TogglePause();
            experience.Restart();
            yield return null;
            arrivalResetClean=ArrivalIsDormant();
            restartClean=experience.Combat.BossDamage==0 && experience.Marine.WhaleResonance==0 &&
                experience.Marine.IlluminatedJellies==0 && !experience.Combat.HasPending &&
                Vector3.Distance(flight.Position,CaveLayout.Spawn)<.01f && !experience.Marine.WhaleReleased &&
                experience.Marine.CompletedJellies==0 && !experience.World.Serpent.Released && arrivalResetClean;
            restartClean &= experience.Combat.Colonies.Count==0 && experience.Combat.Colonies.ReservedCount==0;
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
                whaleChaseSeconds=whaleChaseSeconds,whaleChaseTravel=whaleChaseTravel,whaleChaseMaxSpeed=whaleChaseMaxSpeed,
                playbackPhaseError=playbackPhaseError,
                rayMatterRetained=rayMatterRetained,
                arrivalTriggered=arrivalTriggered,arrivalCompleted=arrivalCompleted,arrivalSlammed=arrivalSlammed,
                arrivalResetClean=arrivalResetClean,arrivalApproachSeconds=arrivalApproachSeconds,
                arrivalTriggerDistance=arrivalTriggerDistance,arrivalCompletionAge=arrivalCompletionAge,
                arrivalPretriggerHidden=arrivalPretriggerHidden,
                arrivalPretriggerTargetsUnavailable=arrivalPretriggerTargetsUnavailable,
                arrivalChargeVisibility=arrivalChargeVisibility,arrivalBreachVisibility=arrivalBreachVisibility,
                arrivalMajorWaves=arrivalMajorWaves,arrivalWaveAmplitude=arrivalWaveAmplitude,arrivalWaveRadius=arrivalWaveRadius,
                dolphinSpawned=dolphinSpawned,dolphinParticleCount=dolphinParticleCount,dolphinIdentityStable=dolphinIdentityStable,
                dolphinMorphed=dolphinMorphed,dolphinBirthAge=dolphinBirthAge,dolphinMatureAge=dolphinMatureAge,
                dolphinPoseDisplacement=dolphinPoseDisplacement,dolphinPoseTravel=dolphinPoseTravel,
                dolphinGpuDisplacement=dolphinGpuDisplacement,dolphinMinSpeed=dolphinMinSpeed,
                dolphinMaxSpeed=dolphinMaxSpeed,dolphinAboveBoostSpeed=dolphinAboveBoostSpeed,
                dolphinPressureShots=dolphinPressureShots,dolphinPressureInterceptions=dolphinPressureInterceptions,
                dolphinPressureIntercepted=dolphinPressureIntercepted,dolphinRetired=dolphinRetired,
                dolphinPressureCleared=dolphinPressureCleared,dolphinReefTransferred=dolphinReefTransferred,
                dolphinReefProgress=dolphinReefProgress,dolphinSpawnedCount=dolphinSpawnedCount,
                dolphinNormalHits=dolphinNormalHits,dolphinSpeedWindow=dolphinSpeedWindow,
                renderedFractions=renderedFractions.ToArray(),errors=errors.ToArray(),gpu=SystemInfo.graphicsDeviceName};
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LIMINAL_CAVERNS_PROOF "+JsonUtility.ToJson(report));
            Application.Quit(report.passed?0:2);
        }

        void CheckScoreLoops()
        {
            double duration=AuthoredScore.Duration;
            for(int loop=0;loop<10000;loop+=127) {
                double edge=(loop+1)*duration;
                Require(Math.Abs(AuthoredScore.Next(edge-.05,.1,true)-(edge+AuthoredScore.Data.beats[1]/(double)AuthoredScore.Data.sampleRate))<.000001,
                    "Quantization must use exact clip samples at loop boundaries");
            }
            double chordChange=AuthoredScore.Data.harmony[2].sample/(double)AuthoredScore.Data.sampleRate;
            Require(AuthoredScore.Note(0,0)!=AuthoredScore.Note(0,chordChange),"Shot harmony must follow the authored chord change");
        }

        IEnumerator ChaseWhale()
        {
            // Only the initial room entry uses SetPose. Every chase frame goes through real flight dynamics.
            var flight=experience.Flight;
            var combat=experience.Combat;
            float start=Time.realtimeSinceStartup,deadline=start+110,lastRelease=start;
            int lastRound=experience.Marine.WhaleRound;
            LockTarget focus=null;
            bool captured=false;
            while(!experience.Marine.WhaleReleased && Time.realtimeSinceStartup<deadline) {
                float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
                if(focus==null || !focus.Available || combat.Locks.Contains(focus)) {
                    focus=null;float best=float.MaxValue;
                    foreach(var target in experience.Marine.WhaleResonatorTargets) {
                        if(!target.Available || combat.Locks.Contains(target)) continue;
                        Vector3 delta=target.position-flight.Position;
                        float cost=Vector3.Angle(flight.transform.forward,delta)*3+delta.magnitude;
                        if(cost<best) {best=cost;focus=target;}
                    }
                }
                Vector3 aim=focus!=null?focus.position:experience.Marine.WhalePosition;
                Vector3 offset=aim-flight.Position;
                Vector3 forward=flight.transform.forward;
                float desiredYaw=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg;
                float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
                float desiredPitch=Mathf.Asin(Mathf.Clamp(offset.normalized.y,-1,1))*Mathf.Rad2Deg;
                float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
                Vector2 look=new(Mathf.Clamp(Mathf.DeltaAngle(yaw,desiredYaw),-90*dt,90*dt)/2.1f,
                    Mathf.Clamp(desiredPitch-pitch,-65*dt,65*dt)/1.7f);
                Vector3 velocity=experience.Marine.WhaleVelocity+offset.normalized*(offset.magnitude-110)*.65f;
                bool boost=velocity.magnitude>24;
                float speed=boost?52:24;
                Vector3 local=Quaternion.Inverse(flight.transform.rotation)*velocity/speed;
                Vector3 before=flight.Position;
                flight.Step((float)experience.Music.Time,dt,local,look,boost);
                whaleChaseTravel+=Vector3.Distance(before,flight.Position);
                whaleChaseMaxSpeed=Mathf.Max(whaleChaseMaxSpeed,flight.Speed);
                combat.AcquireAt(flight.AimScreenPosition);
                if(combat.Locks.Count==8 || (combat.Locks.Count>0 && Time.realtimeSinceStartup-lastRelease>1.8f)) {
                    combat.Release();lastRelease=Time.realtimeSinceStartup;
                }
                if(!captured && experience.Marine.WhaleResonance>=8) {
                    captured=true;Capture("04a-whale-hit-patches.png");
                    bool hot=false,cold=false;
                    for(int i=0;i<MarineLife.WhaleOrganCount;i++) {
                        hot|=experience.Marine.WhaleOrganHeat(i)>=.45f;
                        cold|=experience.Marine.WhaleOrganHeat(i)==0;
                    }
                    Require(hot&&cold,"Chase hits must leave local, not whole-body, patches");
                }
                if(experience.Marine.WhaleRound!=lastRound) {
                    whaleRearmed=true;lastRound=experience.Marine.WhaleRound;
                    foreach(var target in experience.Marine.WhaleResonatorTargets)
                        Require(experience.Marine.WhaleOrganHeat(target.organIndex)==0,"Rearmed whale patches must clear their hit colors");
                }
                yield return null;
            }
            whaleChaseSeconds=Time.realtimeSinceStartup-start;
            Require(whaleRearmed && whaleChaseTravel>50 && whaleChaseMaxSpeed<=52.1f,"Whale chase must move normally and complete both target rounds");
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

        IEnumerator InspectArrival()
        {
            var marine=experience.Marine;
            var arrival=marine.Arrival;
            float start=Time.realtimeSinceStartup;
            float outsideDistance=Vector3.Distance(experience.Flight.Position,arrival.Origin);
            arrivalPretriggerHidden=!arrival.Triggered && !marine.WhaleVisible && marine.WhaleVisibility==0f;
            arrivalPretriggerTargetsUnavailable=true;
            foreach(var target in marine.WhaleResonatorTargets)
                arrivalPretriggerTargetsUnavailable&=!target.Available;
            Require(ArrivalIsDormant() && outsideDistance>190f,
                "Whale arrival must begin hidden and dormant while the swimmer is genuinely outside its 190 m trigger");
            Require(arrivalPretriggerHidden && arrivalPretriggerTargetsUnavailable,
                "Before proximity entry, visibility must be zero and every whale target unavailable");

            var route=new List<Vector3> {CaveLayout.Spawn,CaveLayout.Rooms[0].Center};
            route.AddRange(CaveLayout.Passages[0]);
            route.Add(CaveLayout.Rooms[1].Center);
            route.AddRange(CaveLayout.Passages[1]);
            route.Add(CaveLayout.Rooms[2].Center);
            route.Add(arrival.Origin);
            int waypoint=1;
            float deadline=start+75f;
            while(!arrival.Triggered && Time.realtimeSinceStartup<deadline)
            {
                while(waypoint<route.Count-1 && Vector3.Distance(experience.Flight.Position,route[waypoint])<24f) waypoint++;
                Vector3 destination=route[Mathf.Min(waypoint,route.Count-1)];
                StepFlightToward(experience.Flight,destination,Mathf.Min(Time.unscaledDeltaTime,.05f));
                yield return null;
            }
            arrivalApproachSeconds=Time.realtimeSinceStartup-start;
            arrivalTriggerDistance=Vector3.Distance(experience.Flight.Position,arrival.Origin);
            arrivalTriggered=arrival.Triggered;
            Require(arrivalTriggered && arrivalTriggerDistance<=190.5f,
                "The whale arrival must trigger from a real Flight.Step approach inside 190 m");
            if(!arrivalTriggered) yield break;
            Require(Mathf.Approximately(marine.WhaleVisibility,0f) && !marine.WhaleVisible,
                "The whale must remain hidden during the initial blue-vortex charge");

            yield return WaitForArrivalAge(.9f);
            arrivalChargeVisibility=marine.WhaleVisibility;
            Frame(arrival.Origin,new Vector3(0,48,-205));
            yield return null;
            Capture("00-arrival-blue-vortex.png");

            yield return WaitForArrivalAge(2.8f);
            arrivalBreachVisibility=marine.WhaleVisibility;
            Require(marine.WhaleVisibility>.99f && marine.WhaleVisible,
                "The whale outline must emerge during the breach, not before the proximity-triggered charge");
            Vector3 whale=marine.WhalePosition;
            Frame(whale,marine.WhaleRotation*new Vector3(0,36,-225));
            yield return null;
            Capture("00a-arrival-large-whale-outline.png");

            yield return WaitForArrivalAge(4f);
            whale=marine.WhalePosition;
            Frame(whale,marine.WhaleRotation*new Vector3(0,42,-235));
            yield return null;
            Capture("00b-arrival-breach-apex.png");

            yield return WaitForArrivalAge(6.1f);
            Vector3 slamFocus=experience.Horizon.LastMajorOrigin;
            Frame(slamFocus+Vector3.up*24,new Vector3(115,62,-185));
            yield return null;
            Capture("00c-arrival-slam-wave.png");

            float completeDeadline=Time.realtimeSinceStartup+3f;
            while(!arrival.Complete && Time.realtimeSinceStartup<completeDeadline) yield return null;
            arrivalCompleted=arrival.Complete;
            arrivalCompletionAge=arrival.Age;
            arrivalSlammed=arrival.Slammed;
            arrivalMajorWaves=experience.Horizon.MajorWaveCount;
            Require(arrivalCompleted && arrivalCompletionAge<=9.05f,
                "The whale entrance must complete within about nine seconds of its trigger");
            Require(arrivalSlammed && arrivalMajorWaves>=1,
                "The arrival slam must create a major horizon wave");

            if(experience.Horizon.LastMajorTime>=0f)
            {
                float sampleSong=experience.Horizon.LastMajorTime+2f;
                Vector3 origin=experience.Horizon.LastMajorOrigin;
                for(float radius=100f;radius<=240f;radius+=5f)
                    for(int angle=0;angle<72;angle++)
                    {
                        float radians=angle*Mathf.PI*2f/72f;
                        float height=experience.Horizon.SampleHeight(origin.x+Mathf.Cos(radians)*radius,
                            origin.z+Mathf.Sin(radians)*radius,sampleSong);
                        float amplitude=Mathf.Abs(height-experience.Horizon.SurfaceHeight);
                        if(amplitude>arrivalWaveAmplitude)
                        {
                            arrivalWaveAmplitude=amplitude;
                            arrivalWaveRadius=radius;
                        }
                    }
            }
            Require(arrivalWaveAmplitude>4f && arrivalWaveRadius>=100f,
                "The public major-wave origin and time must produce a greater-than-4 m far-ring surface sample");
        }

        IEnumerator WaitForArrivalAge(float age)
        {
            while(experience.Marine.Arrival && experience.Marine.Arrival.Age<age) yield return null;
        }

        bool ArrivalIsDormant()
        {
            var marine=experience.Marine;
            if(!marine || !marine.Arrival || marine.Arrival.Triggered || marine.WhaleEntranceComplete ||
                marine.WhaleVisibility!=0f || marine.SpawnedDolphins!=0 ||
                experience.Combat.LivePressureShots(marine.Dolphins)!=0) return false;
            foreach(var target in marine.WhaleResonatorTargets) if(target.Available) return false;
            return true;
        }

        void StepFlightToward(Flight flight,Vector3 destination,float dt)
        {
            Vector3 offset=destination-flight.Position;
            if(offset.sqrMagnitude<.01f) return;
            Vector3 forward=flight.transform.forward;
            float desiredYaw=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg;
            float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            float desiredPitch=Mathf.Asin(Mathf.Clamp(offset.normalized.y,-1f,1f))*Mathf.Rad2Deg;
            float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1f,1f))*Mathf.Rad2Deg;
            Vector2 look=new(Mathf.Clamp(Mathf.DeltaAngle(yaw,desiredYaw),-90f*dt,90f*dt)/2.1f,
                Mathf.Clamp(desiredPitch-pitch,-65f*dt,65f*dt)/1.7f);
            Vector3 move=Vector3.Dot(forward,offset.normalized)>.45f?Vector3.forward:Vector3.zero;
            flight.Step((float)experience.Music.Time,dt,move,look,offset.magnitude>70f);
        }

        void StepFlightLookAt(Flight flight,Vector3 point,float dt,Vector3 localMove)
        {
            Vector3 offset=point-flight.Position;
            if(offset.sqrMagnitude<.01f)
            {
                flight.Step((float)experience.Music.Time,dt,localMove,Vector2.zero,false);
                return;
            }
            Vector3 forward=flight.transform.forward;
            float desiredYaw=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg;
            float yaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            float desiredPitch=Mathf.Asin(Mathf.Clamp(offset.normalized.y,-1f,1f))*Mathf.Rad2Deg;
            float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1f,1f))*Mathf.Rad2Deg;
            Vector2 look=new(Mathf.Clamp(Mathf.DeltaAngle(yaw,desiredYaw),-90f*dt,90f*dt)/2.1f,
                Mathf.Clamp(desiredPitch-pitch,-65f*dt,65f*dt)/1.7f);
            flight.Step((float)experience.Music.Time,dt,localMove,look,false);
        }

        IEnumerator InspectDolphin(LockTarget dolphinTarget,int expectedGroup)
        {
            if(dolphinTarget==null) yield break;
            var marine=experience.Marine;
            var dolphin=marine.Dolphins;
            float birthDeadline=Time.realtimeSinceStartup+3f;
            while(!dolphin.PoseAt(0).Active && Time.realtimeSinceStartup<birthDeadline) yield return null;
            Require(dolphin.PoseAt(0).Active && marine.DolphinGroupAt(0)==expectedGroup,
                "The first dolphin must retain its preallocated particle group when it is born");

            while(dolphin.PoseAt(0).Age<.5f && Time.realtimeSinceStartup<birthDeadline+2f) yield return null;
            var birthPose=dolphin.PoseAt(0);
            dolphinBirthAge=birthPose.Age;
            Require(dolphinBirthAge>=.4f && dolphinBirthAge<=.8f,
                "Dolphin birth capture must sample the early peel near age 0.5 seconds");
            Frame(birthPose.Position,new Vector3(0,22,-88));
            yield return null;
            Capture("04ab-dolphin-birth-peel.png");
            MatterParticle[] birthParticles=marine.Matter.Readback();

            float matureDeadline=Time.realtimeSinceStartup+4f;
            while(dolphin.PoseAt(0).Age<2f && Time.realtimeSinceStartup<matureDeadline) yield return null;
            float matureAimDeadline=Time.realtimeSinceStartup+.55f;
            while(Time.realtimeSinceStartup<matureAimDeadline)
            {
                var pose=dolphin.PoseAt(0);
                StepFlightLookAt(experience.Flight,pose.Position,Mathf.Min(Time.unscaledDeltaTime,.05f),Vector3.right);
                Vector3 screen=experience.Flight.View.WorldToScreenPoint(pose.Position);
                if(screen.z>0 && Vector2.Distance(screen,experience.Flight.AimScreenPosition)<experience.Combat.LockRadiusPixels*.7f) break;
                yield return null;
            }
            var maturePose=dolphin.PoseAt(0);
            dolphinMatureAge=maturePose.Age;
            Require(dolphinMatureAge>=1.8f && dolphinMatureAge<=2.8f,
                "Dolphin mature-form capture must sample near age two seconds");
            dolphinPoseDisplacement=Vector3.Distance(birthPose.Position,maturePose.Position);
            Frame(maturePose.Position,maturePose.Rotation*new Vector3(36,12,-6));
            yield return null;
            Capture("04ac-dolphin-mature-form.png");
            MatterParticle[] matureParticles=marine.Matter.Readback();
            dolphinIdentityStable=marine.DolphinGroupAt(0)==expectedGroup && birthParticles.Length==matureParticles.Length;
            int groupCount=0,matureFormCount=0;
            double displacement=0;
            for(int i=0;i<Mathf.Min(birthParticles.Length,matureParticles.Length);i++)
            {
                MatterParticle before=birthParticles[i],after=matureParticles[i];
                if(Mathf.RoundToInt(before.identityState.y)!=expectedGroup) continue;
                groupCount++;
                dolphinIdentityStable&=before.identityState.x==after.identityState.x &&
                    before.identityState.y==after.identityState.y;
                displacement+=Vector3.Distance(before.positionAge,after.positionAge);
                if(Mathf.RoundToInt(after.identityState.z)==(int)MatterPhase.Dolphin) matureFormCount++;
            }
            dolphinGpuDisplacement=(float)(displacement/Math.Max(1,groupCount));
            dolphinMorphed=dolphinIdentityStable && groupCount>0 && matureFormCount==groupCount &&
                dolphinPoseDisplacement>2f && dolphinGpuDisplacement>1f;
            Require(dolphinIdentityStable && groupCount>0,
                "Dolphin particle IDs and immutable group membership must survive the birth-to-mature transition");
            Require(dolphinMorphed,
                "The dolphin must move and morph its persistent GPU particles, not merely switch a phase flag");

            yield return ExerciseDolphinCombat(dolphinTarget,maturePose.Age);
            yield return null;
            dolphinPressureShots=experience.Combat.DolphinPressureShots;
            dolphinPressureInterceptions=experience.Combat.DolphinPressureInterceptions;
            var retiredPose=dolphin.PoseAt(0);
            dolphinRetired=retiredPose.Retired && dolphinTarget.hp==0 && !dolphinTarget.Available &&
                !experience.Combat.Locks.Contains(dolphinTarget);
            dolphinPressureIntercepted=dolphinPressureShots>0 && dolphinPressureInterceptions>0;
            dolphinPressureCleared=dolphinRetired && experience.Combat.LivePressureShots(dolphin)==0 &&
                !experience.Combat.HasPending;
            dolphinAboveBoostSpeed=dolphinMaxSpeed>52f;
            Require(dolphinPressureIntercepted,
                "At least one automatically fired dolphin pressure shot must be intercepted through normal target acquisition");
            Require(dolphinRetired,
                "Three normal hits must retire the actual dolphin target and close its encounter callback");
            Require(dolphinPressureCleared,
                "Retiring the last spawned dolphin must leave no live pressure shots before the whale chase");
            Require(dolphinMaxSpeed>40f,
                "The dolphin's measured swim speed must exceed 40 m/s during its six-second mature window");
            Require(dolphinAboveBoostSpeed,
                "The dolphin sprint must actually exceed the player's 52 m/s boost speed");

            MatterParticle[] retiredParticles=marine.Matter.Readback();
            float transferAt=(float)experience.Music.Time+2.2f;
            while(experience.Music.Time<transferAt) yield return null;
            MatterParticle[] reefParticles=marine.Matter.Readback();
            double beforeError=0,afterError=0;
            int transferCount=0,reefCount=0;
            for(int i=0;i<Mathf.Min(retiredParticles.Length,reefParticles.Length);i++)
            {
                if(Mathf.RoundToInt(retiredParticles[i].identityState.y)!=expectedGroup) continue;
                MatterParticle before=retiredParticles[i],after=reefParticles[i];
                Vector3 destination=marine.SeedAt(i).destination;
                beforeError+=Vector3.Distance(before.positionAge,destination);
                afterError+=Vector3.Distance(after.positionAge,destination);
                reefCount++;
                if(Mathf.RoundToInt(after.identityState.z)==(int)MatterPhase.Transfer) transferCount++;
            }
            dolphinReefProgress=(float)((beforeError-afterError)/Math.Max(1,reefCount));
            dolphinReefTransferred=reefCount>0 && transferCount==reefCount && dolphinReefProgress>.25f;
            Require(dolphinReefTransferred,
                "Retired dolphin particles must leave the animal form and physically transfer toward their reef destinations");
            Frame(marine.WhalePosition,new Vector3(180,58,-320));
            Capture("04ad-dolphin-reef-transfer.png");
        }

        IEnumerator ExerciseDolphinCombat(LockTarget dolphinTarget,float matureAge)
        {
            var flight=experience.Flight;
            var combat=experience.Combat;
            var marine=experience.Marine;
            float deadline=Time.realtimeSinceStartup+16f;
            float sampleThrough=matureAge+6f;
            float sampleStart=matureAge,lastPoseSample=matureAge;
            Vector3 previousPose=marine.Dolphins.PoseAt(0).Position;
            int hitsFired=0;
            while(Time.realtimeSinceStartup<deadline)
            {
                var pose=marine.Dolphins.PoseAt(0);
                if(pose.Active && !pose.Retired)
                {
                    dolphinMinSpeed=Mathf.Min(dolphinMinSpeed,pose.Speed);
                    dolphinMaxSpeed=Mathf.Max(dolphinMaxSpeed,pose.Speed);
                    dolphinPoseTravel+=Vector3.Distance(previousPose,pose.Position);
                    previousPose=pose.Position;
                    lastPoseSample=pose.Age;
                }
                LockTarget pressure=FindLiveDolphinPressure(marine.Dolphins);
                bool sampleDone=lastPoseSample>=sampleThrough;
                bool canRetire=sampleDone && !pose.Retired && dolphinTarget.Available;
                Vector3 aim=pressure!=null?pressure.position:dolphinTarget.position;
                float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
                if (canRetire && pressure == null) StepFlightToward(flight,aim,dt);
                else StepFlightLookAt(flight,aim,dt,Vector3.right);

                if(!combat.HasPending)
                {
                    if(pressure!=null)
                    {
                        Vector3 screen=flight.View.WorldToScreenPoint(pressure.position);
                        int locks=combat.Locks.Count;
                        combat.AcquireAt(new Vector2(screen.x,screen.y));
                        if(combat.Locks.Contains(pressure)) combat.Release();
                        else if(combat.Locks.Count>locks) combat.AbandonLocks();
                    }
                    else if(canRetire)
                    {
                        Vector3 screen=flight.View.WorldToScreenPoint(dolphinTarget.position);
                        int locks=combat.Locks.Count;
                        combat.AcquireAt(new Vector2(screen.x,screen.y));
                        if(combat.Locks.Contains(dolphinTarget))
                        {
                            combat.Release();
                            hitsFired++;
                        }
                        else if(combat.Locks.Count>locks) combat.AbandonLocks();
                    }
                }

                bool retired=marine.Dolphins.PoseAt(0).Retired;
                if(lastPoseSample>=sampleThrough && retired &&
                    combat.LivePressureShots(marine.Dolphins)==0 && !combat.HasPending) break;
                yield return null;
            }
            float sampledSeconds=Mathf.Max(0f,lastPoseSample-sampleStart);
            dolphinSpeedWindow=sampledSeconds;
            dolphinNormalHits=hitsFired;
            Require(sampledSeconds>=5.7f,
                "Dolphin speed must be measured across approximately six seconds of its mature swim");
            Require(hitsFired==3,
                "The dolphin must receive all three scheduled hits through normal AcquireAt and Release calls");
        }

        LockTarget FindDolphinTarget(int slot)
        {
            int found=0;
            foreach(var target in experience.Combat.Targets)
                if(target.kind==TargetKind.Dolphin)
                {
                    if(found++==slot) return target;
                }
            return null;
        }

        LockTarget FindLiveDolphinPressure(DolphinEncounter owner)
        {
            LockTarget best=null;
            float nearest=float.MaxValue;
            foreach(var target in experience.Combat.Targets)
            {
                if(!target.isPressureShot || target.pressureOwner!=owner || target.hp<=0 || target.reserved>0) continue;
                float distance=Vector3.Distance(experience.Flight.Position,target.position);
                if(distance<nearest) {nearest=distance;best=target;}
            }
            return best;
        }

        IEnumerator InspectHorizon()
        {
            Require(experience.Horizon && experience.Horizon.Ready,"Horizon surface and spray must initialize");
            Vector3 firstColor=Vector3.zero;
            float start=(float)experience.Music.Time;
            for(int shot=0;shot<3;shot++) {
                float at=start+(shot==0?0:shot==1?5:14);
                while(experience.Music.Time<at) yield return null;
                Vector3 whale=experience.Marine.WhalePosition;
                Frame(whale,experience.Marine.WhaleRotation*new Vector3(260,45,-20));
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
            public float whaleChaseSeconds,whaleChaseTravel,whaleChaseMaxSpeed;
            public double playbackPhaseError;
            public bool rayMatterRetained;
            public bool arrivalTriggered,arrivalCompleted,arrivalSlammed,arrivalResetClean;
            public bool arrivalPretriggerHidden,arrivalPretriggerTargetsUnavailable;
            public bool dolphinSpawned,dolphinIdentityStable,dolphinMorphed,dolphinPressureIntercepted;
            public bool dolphinRetired,dolphinPressureCleared,dolphinReefTransferred,dolphinAboveBoostSpeed;
            public float arrivalApproachSeconds,arrivalTriggerDistance,arrivalCompletionAge,arrivalWaveAmplitude,arrivalWaveRadius;
            public float arrivalChargeVisibility,arrivalBreachVisibility;
            public float dolphinBirthAge,dolphinMatureAge,dolphinPoseDisplacement,dolphinPoseTravel;
            public float dolphinGpuDisplacement,dolphinMinSpeed,dolphinMaxSpeed,dolphinReefProgress,dolphinSpeedWindow;
            public int arrivalMajorWaves,dolphinParticleCount,dolphinSpawnedCount,dolphinNormalHits;
            public int dolphinPressureShots,dolphinPressureInterceptions;
            public float halfSpeed,cruiseSpeed,coastSpeed;
            public int jellyHits,fishResponses,serpentHits,whaleHits;
            public double gridError;
            public float[] renderedFractions;
            public string[] errors;
            public string gpu;
        }
    }
}
