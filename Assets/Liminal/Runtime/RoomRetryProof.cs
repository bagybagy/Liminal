using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Liminal
{
    public sealed class RoomRetryProof : MonoBehaviour
    {
        Experience game;
        bool complete;
        float started;

        public void Initialize(Experience owner)
        {
            game=owner;
            started=Time.realtimeSinceStartup;
        }

        void Update()
        {
            if(!complete && Time.realtimeSinceStartup-started>25f)
            {
                complete=true;
                Debug.LogError("[ROOM RETRY PROOF] Bounded deadline exceeded.",this);
                Application.Quit(2);
            }
        }

        IEnumerator Start()
        {
            float deadline=Time.realtimeSinceStartup+8f;
            while(!game.Ready && Time.realtimeSinceStartup<deadline) yield return null;
            Check(game.Ready,"Experience did not become ready before the proof deadline.");
            game.ManualProofTick=true;

            var progress=new RunProgress();
            progress.Record(BossId.Serpent);
            BossId copiedMask=progress.Defeated;
            progress.Record(BossId.Hermit);
            Check(copiedMask==BossId.Serpent,"Checkpoint boss flags must be an immutable value snapshot.");
            progress.RestoreCheckpoint(copiedMask);
            Check(progress.Defeated==BossId.Serpent,"Retry must restore precisely the copied entry boss flags.");
            Check(game.HasRoomCheckpoint && game.RoomCheckpointRoom==0 &&
                Vector3.Distance(game.RoomCheckpointPosition,CaveLayout.Spawn)<.001f,
                "The initial room checkpoint must be at the room entrance.");
            Check(!PcVrSession.MenuInputActive(false,false) && PcVrSession.MenuInputActive(false,true) &&
                PcVrSession.MenuInputActive(true,false),"PCVR menu input must be active while paused or at game over.");

            Vector3 initialEntry=game.RoomCheckpointPosition;
            for(int hit=0;hit<8;hit++) game.Combat.ReceiveDamage();
            game.Combat.Tick(.016f,false);
            Check(game.Combat.Lost && game.CanRetryRoom &&
                game.RoomCheckpointRoom==0 && game.RoomCheckpointPosition==initialEntry,
                "Death before another Experience update must retain the entry checkpoint and expose retry.");

            Check(game.RetryCurrentRoom(),"A lost run with a valid room checkpoint must retry.");
            Check(!game.Combat.Ended && game.Combat.Life==8 && game.Combat.Locks.Count==0 &&
                !game.Combat.HasPending && Vector3.Distance(game.Flight.Position,initialEntry)<.001f,
                "Retry must clear combat, restore full life, and return to the initial entrance pose.");
            Check(game.Progress.Defeated==BossId.None,"Retry must restore the entry boss mask.");
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            Check(game.RoomCheckpointPosition==initialEntry && game.RoomCheckpointBossMask==BossId.None,
                "A retry spawn must not immediately refresh its own checkpoint.");

            var serpentRoom=CaveLayout.Rooms[1];
            Vector3 outside=serpentRoom.Center+Vector3.forward*(serpentRoom.Radius.z*1.25f);
            game.Flight.SetPose(outside,Quaternion.identity);
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            Vector3 entry=serpentRoom.Center+Vector3.back*(serpentRoom.Radius.z*.88f);
            Quaternion facing=Quaternion.identity;
            game.Flight.SetPose(entry,facing);
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            Check(game.RoomCheckpointRoom==1 && game.RoomCheckpointPosition==entry &&
                game.RoomCheckpointBossMask==BossId.None,
                "The serpent checkpoint must be captured at its physical room entry.");

            ApplyBossDamage(game.Combat,(float)game.Music.Time);
            Check(game.Combat.BossDamage==1,"Proof setup must damage the current serpent encounter.");
            LockTarget incoming=game.Combat.RegisterPressureShot(entry+Vector3.forward*30f,Vector3.back,
                (float)game.Music.Time,Color.red,game.Marine.Inheritance);
            Check(incoming!=null && game.Combat.LivePressureShots(game.Marine.Inheritance)==1,
                "Proof setup must create an incoming inherited projectile.");
            game.Horizon.MajorImpact(entry,Vector3.down*30f,(float)game.Music.Time);
            for(int hit=0;hit<8;hit++) game.Combat.ReceiveDamage();
            game.Combat.Tick(.016f,false);
            Check(game.Combat.Lost && game.RetryCurrentRoom(),"A second death must allow another retry.");
            Check(!game.Combat.Ended && game.Combat.Life==8 && game.Combat.BossDamage==0 &&
                game.Combat.LivePressureShots(game.Marine.Inheritance)==0 &&
                game.Horizon.ImpactSplash.ImpactCount==0 && game.Horizon.ImpactSplash.Age<0 &&
                Vector3.Distance(game.Flight.Position,entry)<.001f &&
                Quaternion.Angle(game.Flight.transform.rotation,facing)<.01f,
                "Retry must clear incoming shots, reset current-encounter damage, refill health, and restore its entrance pose.");
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            Check(game.RoomCheckpointPosition==entry && game.RoomCheckpointBossMask==BossId.None,
                "Retry at an encounter entrance must not immediately refresh the checkpoint.");

            game.Flight.SetPose(outside,Quaternion.identity);
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            game.Progress.Record(BossId.Serpent);
            game.Combat.RestoreCompletedSerpent((float)game.Music.Time);
            Vector3 reentry=serpentRoom.Center+Vector3.right*18f;
            game.Flight.SetPose(reentry,Quaternion.identity);
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            Check(game.RoomCheckpointRoom==1 && game.RoomCheckpointPosition==reentry &&
                game.RoomCheckpointBossMask==BossId.Serpent,
                "Leaving and re-entering a room must refresh its entrance pose and boss-mask snapshot.");

            game.Progress.Record(BossId.Hermit);
            Check(game.RoomCheckpointBossMask==BossId.Serpent,
                "Progress earned after entry must not mutate the checkpoint's copied boss mask.");
            SetPrivate(game,"whaleCalled",true);
            SetPrivate(game,"<WhaleAwakenedAt>k__BackingField",12d);
            for(int hit=0;hit<8;hit++) game.Combat.ReceiveDamage();
            game.Combat.Tick(.016f,false);
            Check(game.Combat.Lost && game.RetryCurrentRoom(),"A third death must allow retry after re-entry.");
            Check(game.Progress.Defeated==BossId.Serpent && game.Combat.SerpentComplete &&
                game.Combat.BossDamage==game.Combat.BossDamageGoal && game.World.Serpent.Released &&
                !game.Hermits.Complete && !game.Submarines.Complete,
                "Retry must roll back later flags, preserve the defeated serpent, and reset unfinished bosses.");
            Check(game.Marine.GrowthMask==BossId.Serpent && !game.Marine.WhaleEntranceComplete &&
                !game.Marine.WhaleReleased && game.WhaleAwakenedAt<0 && !GetPrivateBool(game,"whaleCalled") &&
                game.Marine.InheritedShotsLive==0 &&
                game.Combat.LivePressureShots(game.Marine.Inheritance)==0 &&
                game.Combat.LivePressureShots(game.Marine.Dolphins)==0,
                "Retry must reset whale intro/inherited attacks and restore the saved inheritance mask.");
            game.Combat.ActiveRoom=1;
            game.Combat.Tick(.016f,false);
            Check(!game.Combat.Targets.Exists(target=>(target.kind==TargetKind.Ray || target.kind==TargetKind.Threat) && target.Available),
                "A defeated serpent must remain quiet after retry.");

            game.Progress.Record(BossId.Whale);
            Vector3 endingEntry=serpentRoom.Center+Vector3.up*20f;
            game.Flight.SetPose(outside,Quaternion.identity);
            game.ManualProofTick=false;
            yield return null;
            yield return null;
            game.Flight.SetPose(endingEntry,Quaternion.identity);
            yield return null;
            yield return null;
            game.ManualProofTick=true;
            Check(game.RoomCheckpointPosition==reentry,
                "Room checkpoint must not be replaced after the ending has started.");
            for(int hit=0;hit<8;hit++) game.Combat.ReceiveDamage();
            game.Combat.Tick(.016f,false);
            Check(game.Combat.Lost && !game.CanRetryRoom,
                "Room retry must stay unavailable after the run ending has started.");
            game.Restart();
            Check(game.Progress.Defeated==BossId.None && game.Combat.Life==8 &&
                !game.Progress.EndingStarted &&
                Vector3.Distance(game.Flight.Position,CaveLayout.Spawn)<.001f &&
                game.RoomCheckpointRoom==0 && game.RoomCheckpointBossMask==BossId.None &&
                game.Combat.BossDamage==0 && !game.Combat.SerpentComplete &&
                !game.Hermits.Complete && !game.Submarines.Complete,
                "Whole-run restart must clear progress and return to the initial spawn.");
            complete=true;
            Debug.Log("[ROOM RETRY PROOF] PASS entry/re-entry snapshot; immediate death; repeated retries; rollback; defeated-boss quiet state; VR menu contract; ending guard; whole-run restart.");
            Application.Quit(0);
        }

        static void ApplyBossDamage(Encounter combat,float song)
        {
            MethodInfo applyHit=typeof(Encounter).GetMethod("ApplyHit",BindingFlags.Instance|BindingFlags.NonPublic);
            if(applyHit==null) throw new MissingMethodException(typeof(Encounter).FullName,"ApplyHit");
            applyHit.Invoke(combat,new object[] {combat.Targets[0],song});
        }

        static void SetPrivate(object target,string field,object value)
        {
            FieldInfo member=target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);
            if(member==null) throw new MissingFieldException(target.GetType().FullName,field);
            member.SetValue(target,value);
        }

        static bool GetPrivateBool(object target,string field)
        {
            FieldInfo member=target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);
            if(member==null) throw new MissingFieldException(target.GetType().FullName,field);
            return (bool)member.GetValue(target);
        }

        void Check(bool passed,string message)
        {
            if(passed) return;
            Debug.LogError("[ROOM RETRY PROOF] "+message,this);
            complete=true;
            Application.Quit(2);
            throw new InvalidOperationException(message);
        }

        void OnDestroy()
        {
            if(game!=null) game.ManualProofTick=false;
            if(!complete) Debug.LogError("[ROOM RETRY PROOF] Incomplete.",this);
        }
    }
}
