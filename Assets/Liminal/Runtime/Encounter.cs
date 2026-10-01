using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public enum TargetKind { Organ, Ray, Threat, Environment, Dolphin }
    public sealed class LockTarget
    {
        public int id, hp, reserved, organIndex;
        public TargetKind kind;
        public float u, born, deadline;
        public float acquireRange = Encounter.LockRange;
        public bool isWhale, transformed;
        public bool isPressureShot;
        public UnityEngine.Object pressureOwner;
        public Color pressureColor;
        public float pressureSpeed, pressureAge, pressureLifetime;
        public Vector3 previousPlayerPosition;
        public Vector3 position, origin, destination, direction, lateral, vertical;
        public GameObject visual;
        public Action<LockTarget,float> onHit;
        public bool Available => hp-reserved>0 && visual && visual.activeSelf;
    }

    public sealed class Encounter : MonoBehaviour
    {
        public readonly List<LockTarget> Targets = new();
        public readonly List<LockTarget> Locks = new();
        readonly List<Shot> shots = new();
        readonly Stack<LineRenderer> lines = new();
        readonly List<LineRenderer> allLines = new();
        readonly HashSet<DolphinEncounter> dolphinSchools = new();
        MusicTransport music;
        ParticleWorld world;
        Flight flight;
        public DefeatedMarineForms Colonies { get; private set; }
        int nextId, lastSection=-1, lastBeat=-1, nextWave;
        public int Points { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Hits { get; private set; }
        public int Fired { get; private set; }
        public int CancelledAfterFinish { get; private set; }
        public int MissedScheduledHits { get; private set; }
        public int BossDamage { get; private set; }
        public int Life { get; private set; } = 8;
        public float Charge { get; private set; }
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool Ended => Won||Lost;
        public bool ExplorationMode { get; set; }
        public int ActiveRoom { get; set; } = 0;
        public int BossDamageGoal => ExplorationMode ? 80 : 240;
        public bool SerpentComplete => BossDamage >= BossDamageGoal;
        public float EndTime { get; private set; }
        public float DamageFlash { get; private set; }
        public float LastHitTime { get; private set; } = -10;
        public const float LockRange = 105f;
        public float LockRadiusPixels => Mathf.Clamp(Screen.height * 0.09f, 64f, 110f);
        public int Section { get; private set; }
        public int MaxLocks { get; private set; }
        public double MaxImpactDelay { get; private set; }
        public bool HasPending => shots.Count>0;
        public int Dodged { get; private set; }
        public int DamageTaken { get; private set; }
        public int SpawnedPressureShots { get; private set; }
        public int InterceptedPressureShots { get; private set; }
        public int DolphinPressureShots { get; private set; }
        public int DolphinPressureInterceptions { get; private set; }
        public event Action Hit;
        bool organsActivated;
        bool hostilesCleared;
        public int SerpentRound { get; private set; }
        float organWaveResetAt = -1;
        public bool OrganWaveResetPending => organWaveResetAt >= 0;
        static MaterialPropertyBlock OrganProperties;
        static readonly Color Cyan = new(0.3f,1,0.91f), Amber = new(1,0.65f,0.22f);
        static readonly Color ElectricBlue = new(0.06f,0.42f,1f);
        static readonly int TintId = Shader.PropertyToID("_Tint");
        const int MaxLivePressureShots = 24;
        const float PressureShotSpeed = 48f;
        static MaterialPropertyBlock MarkerProperties;
        sealed class Shot
        {
            public LockTarget target;
            public Vector3 from;
            public double born, arrival;
            public int index;
            public LineRenderer line;
        }
        public void Initialize(MusicTransport transport,ParticleWorld particles,Flight pilot)
        {
            OrganProperties ??= new MaterialPropertyBlock();
            MarkerProperties ??= new MaterialPropertyBlock();
            music=transport; world=particles; flight=pilot;
            if(ExplorationMode) {
                Colonies=gameObject.AddComponent<DefeatedMarineForms>();
                Colonies.Initialize(world.EnemyMesh,world.NodeMaterial);
            }
            for(int i=0;i<16;i++) {
                var t=new LockTarget {id=nextId++,kind=TargetKind.Organ,organIndex=i,u=0.025f+i*0.058f};
                t.visual=PointCloud.Place("Organ "+i,world.NodeMesh,world.NodeMaterial,transform);
                t.visual.SetActive(false); Targets.Add(t);
            }
            for(int i=0;i<48;i++) {
                var obj=new GameObject("Homing ribbon "+i); obj.transform.SetParent(transform);
                var line=obj.AddComponent<LineRenderer>();
                line.sharedMaterial=world.ribbonMaterial;
                line.positionCount=32; line.useWorldSpace=true;
                line.textureMode=LineTextureMode.Stretch;
                line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(0.6f,0.65f),new Keyframe(1,1));
                line.numCapVertices=4;
                line.enabled=false; lines.Push(line); allLines.Add(line);
            }
            Restart();
        }
        public void Restart()
        {
            foreach(var school in dolphinSchools) if(school) school.ResetSchool();
            foreach(var s in shots) { s.line.enabled=false; lines.Push(s.line); }
            shots.Clear(); Locks.Clear();
            for(int i=Targets.Count-1;i>=0;i--) {
                if(Targets[i].kind==TargetKind.Environment) { Targets.RemoveAt(i); continue; }
                if(Targets[i].kind==TargetKind.Dolphin) {
                    Targets[i].hp=Targets[i].reserved=0;
                    if(Targets[i].visual) Targets[i].visual.SetActive(false);
                    continue;
                }
                if(i>=16) { if(!Targets[i].transformed) Destroy(Targets[i].visual); Targets.RemoveAt(i); }
            }
            if(Colonies) Colonies.Reset();
            foreach(var t in Targets) { t.hp=0;t.reserved=0;t.visual.SetActive(false); }
            Points=Combo=BestCombo=Hits=Fired=BossDamage=Dodged=DamageTaken=CancelledAfterFinish=MissedScheduledHits=0;
            SpawnedPressureShots=InterceptedPressureShots=DolphinPressureShots=DolphinPressureInterceptions=0;
            Life=8;Charge=0;Won=Lost=false;EndTime=0;DamageFlash=0;
            lastSection=-1;lastBeat=-1;nextWave=16;MaxImpactDelay=0;MaxLocks=0;LastHitTime=-10;
            organsActivated=false;hostilesCleared=false;SerpentRound=0;organWaveResetAt=-1;
            world.ResetEffects(); flight.ResetFlight();
        }
        public void Tick(float dt,bool input)
        {
            float song=(float)music.Time;
            Section=Score.Section(song);
            DamageFlash=Mathf.MoveTowards(DamageFlash,0,dt*1.6f);
            if(ExplorationMode && ActiveRoom==1 && !organsActivated) ActivateOrganWave(song);
            if(Section!=lastSection) {
                if(!ExplorationMode && !Ended && Section>=2 && Section<=4)
                    foreach(var t in Targets) if(t.kind==TargetKind.Organ) {
                        t.hp+=5; t.visual.SetActive(true);
                        world.BurstAt(Anatomy.Node(t.u,song),song,Cyan,0.35f);
                    }
                lastSection=Section;
            }
            int beat=Mathf.FloorToInt(song/(float)Score.BeatSeconds);
            if(beat!=lastBeat && !Ended) {
                bool spawnHostiles=!ExplorationMode || (ActiveRoom==1 && !SerpentComplete);
                if(spawnHostiles && beat>=nextWave && (ExplorationMode || beat<376)) { SpawnWave(song,beat); nextWave=beat+(Section==1?16:32); }
                if(spawnHostiles && (ExplorationMode || beat>=104) && (ExplorationMode || beat<384) && beat%8==0) SpawnThreat(song);
                lastBeat=beat;
            }
            if(ExplorationMode && SerpentComplete && !hostilesCleared) {
                hostilesCleared=true;
                foreach(var t in Targets) if(t.kind==TargetKind.Ray || t.kind==TargetKind.Threat)
                    t.hp=t.reserved>0?t.reserved:0;
                Locks.RemoveAll(t=>t.kind==TargetKind.Ray || t.kind==TargetKind.Threat);
            }
            UpdateTargets(song,dt);
            UpdateShots(song);
            if(Colonies) Colonies.Tick(song);
            AdvanceOrganWaves(song);
            Locks.RemoveAll(t=>!t.Available);
            if(!Ended && input) {
                if(Input.GetMouseButton(0)) AcquireAt(flight.AimScreenPosition);
                if(Input.GetMouseButtonUp(0)) Release();
                if(Input.GetKeyDown(KeyCode.Space)) Nova();
            }
            if(!Ended && !ExplorationMode && BossDamage>=240) Finish(true,song);
            if(!Ended && (Life<=0 || (!ExplorationMode && song>=Score.Duration-8))) Finish(false,song);
        }
        void ActivateOrganWave(float song)
        {
            organsActivated=true; SerpentRound=1;
            foreach(var t in Targets) if(t.kind==TargetKind.Organ) {
                t.hp=1; t.visual.SetActive(true); SetOrganMarker(t,0);
                world.Serpent.SetOrganState(t.organIndex,t.u,0,song,true);
                world.BurstAt(Anatomy.Node(t.u,song),song,Cyan,0.35f);
            }
        }
        void AdvanceOrganWaves(float song)
        {
            if(!ExplorationMode || ActiveRoom!=1 || !organsActivated || SerpentComplete) return;
            bool depleted=true, pending=false;
            foreach(var t in Targets) if(t.kind==TargetKind.Organ) {
                depleted &= t.hp<=0;
                pending |= t.reserved>0;
            }
            if(!depleted || pending) return;
            if(organWaveResetAt<0) {
                float beat=(float)Score.BeatSeconds;
                organWaveResetAt=Mathf.Ceil((song+Mathf.Max(1f,beat*2))/beat)*beat;
            }
            if(song<organWaveResetAt || SerpentRound>=5) return;
            organWaveResetAt=-1; SerpentRound++;
            foreach(var t in Targets) if(t.kind==TargetKind.Organ) {
                t.hp=1; t.visual.SetActive(true); SetOrganMarker(t,0);
                world.Serpent.SetOrganState(t.organIndex,t.u,0,song,true);
                world.BurstAt(Anatomy.Node(t.u,song),song,Cyan,0.35f);
            }
        }
        static void SetOrganMarker(LockTarget target,float warmth)
        {
            var renderer=target.visual.GetComponent<Renderer>();
            renderer.GetPropertyBlock(OrganProperties);
            OrganProperties.SetColor("_Tint",Color.Lerp(Color.white,new Color(1f,0.24f,0.055f),warmth));
            renderer.SetPropertyBlock(OrganProperties);
        }
        void UpdateTargets(float song,float dt)
        {
            for(int i=Targets.Count-1;i>=0;i--) {
                var t=Targets[i];
                if(t.transformed) { Locks.Remove(t);Targets.RemoveAt(i);continue; }
                if(t.kind==TargetKind.Environment) continue;
                if(t.kind==TargetKind.Dolphin) {
                    t.visual.SetActive(t.hp>0 && !Ended);
                    continue;
                }
                if(t.kind==TargetKind.Organ) t.position=Anatomy.Node(t.u,song);
                else if(t.kind==TargetKind.Ray) {
                    float age=song-t.born;
                    Vector3 previous=t.position;
                    t.position=RayPosition(t,age);
                    Vector3 movement=t.position-previous;
                    if(movement.sqrMagnitude>0.0001f) {
                        Vector3 up=Mathf.Abs(Vector3.Dot(movement.normalized,Vector3.up))>0.98f?Vector3.forward:Vector3.up;
                        t.visual.transform.rotation=Quaternion.LookRotation(movement,up);
                    }
                } else if(t.isPressureShot) {
                    if(t.hp>0 && !Ended) {
                        Vector3 previous=t.position;
                        t.pressureAge+=Mathf.Max(0,dt);
                        t.position=t.origin+t.direction*(t.pressureSpeed*t.pressureAge);
                        Vector3 relativeStart=previous-t.previousPlayerPosition;
                        Vector3 relativeEnd=t.position-flight.Position;
                        Vector3 relativeStep=relativeEnd-relativeStart;
                        float closest=Mathf.Clamp01(-Vector3.Dot(relativeStart,relativeStep)/
                            Mathf.Max(0.0001f,relativeStep.sqrMagnitude));
                        if(t.reserved==0 && (relativeStart+relativeStep*closest).sqrMagnitude<=3.6f*3.6f) {
                            ReceiveDamage();
                            t.hp=0;
                        } else if(t.reserved==0 && t.pressureAge>=t.pressureLifetime) {
                            Dodged++;
                            t.hp=0;
                        }
                        t.previousPlayerPosition=flight.Position;
                    } else if(Ended) t.hp=0;
                    t.visual.transform.localScale=Vector3.one*1.4f;
                } else {
                    float f=Mathf.InverseLerp(t.born,t.deadline,song);
                    t.position=Vector3.Lerp(t.origin,t.destination,f)+Vector3.up*Mathf.Sin(f*Mathf.PI)*3;
                    t.visual.transform.localScale=Vector3.one*(1.2f+Score.Pulse(song)*0.5f);
                    // An accepted interception resolves on its scheduled note, not an earlier expiry.
                    if(song>=t.deadline && t.hp>0 && t.reserved==0 && !Ended) {
                        if(Vector3.Distance(flight.Position,t.destination)<2.6f) ReceiveDamage(); else Dodged++;
                        t.hp=0;
                    }
                }
                t.visual.transform.position=t.position;
                bool activeOrgan=t.kind==TargetKind.Organ && ExplorationMode
                    ? organsActivated && !SerpentComplete : t.hp>0;
                t.visual.SetActive(activeOrgan && !Ended);
                if(t.kind!=TargetKind.Organ && t.reserved==0 && (t.hp<=0 || song-t.born>26)) {
                    if(Colonies && t.kind==TargetKind.Ray) Colonies.Release(t.visual);
                    Locks.Remove(t);Destroy(t.visual);Targets.RemoveAt(i);
                }
            }
        }
        void SpawnWave(float song,int beat)
        {
            int count=Section<=1?6:4;
            if(Colonies) count=Mathf.Min(count,Colonies.AvailableCapacity);
            Transform camera=flight.View.transform;
            Vector3 center=Vector3.Lerp(flight.Position,Anatomy.Center(0.5f,song),0.55f);
            for(int i=0;i<count;i++) {
                var t=new LockTarget {id=nextId++,kind=TargetKind.Ray,hp=1,born=song,u=i*0.9f};
                t.origin=center+camera.forward*2f+camera.right*((i-(count-1)*0.5f)*2.6f)+camera.up*Mathf.Sin(i+beat)*1.8f;
                t.direction=(flight.Position-t.origin).normalized;
                if(t.direction.sqrMagnitude<0.001f) t.direction=-camera.forward;
                t.lateral=camera.right;
                t.vertical=camera.up;
                t.position=t.origin;
                t.visual=PointCloud.Place("Choir ray",world.EnemyMesh,world.NodeMaterial,transform);
                if(Colonies && !Colonies.TryReserve(t.visual)) { Destroy(t.visual);continue; }
                t.visual.transform.localScale=Vector3.one*0.45f;
                t.visual.transform.rotation=Quaternion.LookRotation(t.direction,Vector3.up);
                Targets.Add(t);
            }
        }
        static Vector3 RayPosition(LockTarget target,float age)
        {
            float side=Mathf.Sin(age*0.65f+target.u)-Mathf.Sin(target.u);
            float lift=Mathf.Sin(age*0.85f+target.u)-Mathf.Sin(target.u);
            return target.origin+target.direction*(age*0.44f)+target.lateral*(side*4)+target.vertical*(lift*2);
        }
        void SpawnThreat(float song)
        {
            var t=new LockTarget {id=nextId++,kind=TargetKind.Threat,hp=1,born=song};
            t.origin=Anatomy.Head(song);t.destination=flight.Position;
            float beat=(float)Score.BeatSeconds;
            float duration=Mathf.Clamp(Mathf.Ceil(Vector3.Distance(t.origin,t.destination)/12f/beat)*beat,beat*2,beat*16);
            t.deadline=Mathf.Ceil((song+duration)/beat)*beat;
            t.visual=PointCloud.Place("Pressure pulse",world.NodeMesh,world.NodeMaterial,transform);
            SetMarkerTint(t.visual,ElectricBlue);
            Targets.Add(t);
        }
        public void AcquireAt(Vector2 mouse)
        {
            LockTarget best=null; float distance=LockRadiusPixels;
            foreach(var t in Targets) {
                if(!CanAcquire(t)) continue;
                Vector3 screen=flight.View.WorldToScreenPoint(t.position);
                float d=Vector2.Distance(mouse,screen);
                if(d<distance) {distance=d;best=t;}
            }
            if(best!=null) Acquire(best);
        }
        public bool CanAcquire(LockTarget target)
        {
            if(Ended || target==null || !target.Available || Locks.Count>=8 || Locks.Contains(target)) return false;
            if(Vector3.Distance(flight.Position,target.position)>target.acquireRange) return false;
            if(ExplorationMode && !CaveLayout.LineOfSight(flight.Position,target.position)) return false;
            Vector3 projected=flight.View.WorldToViewportPoint(target.position);
            return projected.z>=flight.View.nearClipPlane && projected.z<=flight.View.farClipPlane &&
                projected.x>=0 && projected.x<=1 && projected.y>=0 && projected.y<=1;
        }
        public bool Acquire(LockTarget target)
        {
            if(!CanAcquire(target)) return false;
            Locks.Add(target); music.LockSound(); MaxLocks=Mathf.Max(MaxLocks,Locks.Count); return true;
        }
        public void Release()
        {
            if(Ended) return;
            double arrival=AuthoredScore.Next(music.Time,0.16,true);
            int index=0;
            foreach(var t in Locks) {
                if(!t.Available || lines.Count==0) continue;
                Vector3 screen=flight.View.WorldToViewportPoint(t.position);
                if(!music.ScheduleNote(index,arrival,(screen.x-0.5f)*1.4f)) continue;
                t.reserved++;
                var line=lines.Pop();line.enabled=true;line.widthMultiplier=0.10f;
                Color color=Color.Lerp(Cyan,Amber,index/7f)*2.6f;
                line.startColor=new Color(color.r,color.g,color.b,0);line.endColor=color;
                shots.Add(new Shot {target=t,from=flight.Emitter,born=music.Time,arrival=arrival,index=index,line=line});
                index++;Fired++;
                arrival=AuthoredScore.Next(arrival,1.0/AuthoredScore.Data.sampleRate,false);
            }
            Locks.Clear();
        }
        void UpdateShots(float song)
        {
            for(int i=shots.Count-1;i>=0;i--) {
                var s=shots[i];
                if(song>=s.arrival) {
                    MaxImpactDelay=Math.Max(MaxImpactDelay,song-s.arrival);
                    s.target.reserved=Mathf.Max(0,s.target.reserved-1);
                    if(!Ended && s.target.hp>0) ApplyHit(s.target,song);
                    else if(Ended) CancelledAfterFinish++;
                    else MissedScheduledHits++;
                    s.line.enabled=false;lines.Push(s.line);shots.RemoveAt(i);continue;
                }
                float f=Mathf.Clamp01((float)((song-s.born)/(s.arrival-s.born)));
                Vector3 end=s.target.position;
                Vector3 side=flight.View.transform.right*(s.index%2==0?1:-1)*(3+s.index*0.6f);
                Vector3 middle=(s.from+end)*0.5f+side+Vector3.up*(4+s.index*0.32f);
                for(int j=0;j<32;j++) {
                    float u=Mathf.Lerp(Mathf.Max(0,f-0.35f),f,j/31f);
                    Vector3 p=(1-u)*(1-u)*s.from+2*(1-u)*u*middle+u*u*end;
                    s.line.SetPosition(j,p);
                }
            }
        }
        void ApplyHit(LockTarget target,float song)
        {
            target.hp--;Hits++;Combo++;BestCombo=Mathf.Max(BestCombo,Combo);
            Points+=100*(1+Mathf.Min(7,Combo/8));Charge=Mathf.Min(1,Charge+0.018f);
            if(target.kind==TargetKind.Organ) {
                BossDamage++;
                if(ExplorationMode) {
                    SetOrganMarker(target,1);
                    world.Serpent.SetOrganState(target.organIndex,target.u,1,song,true);
                }
            }
            if(target.kind==TargetKind.Environment || target.kind==TargetKind.Dolphin) target.onHit?.Invoke(target,song);
            if(target.isPressureShot) {
                InterceptedPressureShots++;
                if(target.pressureOwner is DolphinEncounter) DolphinPressureInterceptions++;
            }
            if(Colonies && target.kind==TargetKind.Ray && target.hp<=0)
                target.transformed=Colonies.TryAdopt(target.visual,song,CaveLayout.NearestRoom(target.position));
            if(target.kind==TargetKind.Organ) world.BurstAt(target.position,song,Amber);
            else if(target.kind==TargetKind.Ray || target.kind==TargetKind.Threat)
                world.BurstAt(target.position,song,target.isPressureShot ? target.pressureColor : ElectricBlue);
            else if(target.kind==TargetKind.Dolphin) world.BurstAt(target.position,song,ElectricBlue);
            LastHitTime=song;Hit?.Invoke();
        }
        internal void ReceiveDamage()
        {
            Life--;DamageTaken++;Combo=0;DamageFlash=1;music.DamageSound();
            world.BurstAt(flight.Emitter,(float)music.Time,new Color(1,0.2f,0.1f));
        }
        public void Nova()
        {
            if(Charge<1 || Ended) return;
            Charge=0;
            foreach(var t in Targets) if(t.Available) Acquire(t);
            Release();
            foreach(var t in Targets) if(t.kind==TargetKind.Threat && t.reserved==0) {world.BurstAt(t.position,(float)music.Time,ElectricBlue);t.hp=0;}
        }
        void Finish(bool won,float song)
        {
            Won=won;Lost=!won;EndTime=song;Locks.Clear();
            if(won) for(int i=0;i<16;i++) world.BurstAt(Anatomy.Center(i/16f,song),song,Cyan,2);
        }
        public void AbandonLocks() => Locks.Clear();

        public LockTarget RegisterEnvironment(GameObject visual,Action<LockTarget,float> onHit)
        {
            if(visual==null) return null;
            var target=new LockTarget { id=nextId++,kind=TargetKind.Environment,hp=1,
                position=visual.transform.position,visual=visual,onHit=onHit };
            Targets.Add(target);
            return target;
        }

        internal void RegisterDolphinSchool(DolphinEncounter school)
        {
            if(school) dolphinSchools.Add(school);
        }

        internal void UnregisterDolphinSchool(DolphinEncounter school)
        {
            if(school) dolphinSchools.Remove(school);
        }

        public void UnregisterEnvironment(LockTarget target)
        {
            if(target!=null && target.kind==TargetKind.Environment) UnregisterDolphinTarget(target);
        }

        internal void UnregisterDolphinTarget(LockTarget target)
        {
            if(target==null) return;
            CancelScheduledShots(target);
            Locks.Remove(target);
            target.hp=0;
            target.reserved=0;
            target.onHit=null;
            if(target.visual) {
                target.visual.SetActive(false);
                Destroy(target.visual);
            }
            Targets.Remove(target);
        }

        internal void ResetDolphinTarget(LockTarget target)
        {
            if(target==null) return;
            CancelScheduledShots(target);
            Locks.Remove(target);
            target.hp=0;
            target.reserved=0;
        }

        void CancelScheduledShots(LockTarget target)
        {
            for(int i=shots.Count-1;i>=0;i--) {
                var shot=shots[i];
                if(shot.target!=target) continue;
                target.reserved=Mathf.Max(0,target.reserved-1);
                shot.line.enabled=false;
                lines.Push(shot.line);
                shots.RemoveAt(i);
            }
        }

        public LockTarget RegisterDolphinTarget(GameObject visual,Action<LockTarget,float> onHit)
        {
            if(!visual) return null;
            var target=new LockTarget { id=nextId++,kind=TargetKind.Dolphin,hp=0,
                position=visual.transform.position,visual=visual,onHit=onHit,acquireRange=LockRange };
            visual.SetActive(false);
            Targets.Add(target);
            return target;
        }

        internal bool CanRegisterPressureShots(int count)
        {
            if(Ended || !world || !flight || count<=0) return false;
            int live=0;
            foreach(var target in Targets)
                if(target.isPressureShot && target.hp>0) live++;
            return live+count<=MaxLivePressureShots;
        }

        public LockTarget RegisterPressureShot(Vector3 origin,Vector3 direction,float song,Color color,
            UnityEngine.Object owner=null,float speed=PressureShotSpeed)
        {
            if(Ended || !world || !flight || direction.sqrMagnitude<0.0001f) return null;
            int live=0;
            foreach(var target in Targets)
                if(target.isPressureShot && target.hp>0) live++;
            if(live>=MaxLivePressureShots) return null;

            direction.Normalize();
            speed=Mathf.Max(1f,speed);
            float lifetime=Mathf.Clamp(Vector3.Distance(origin,flight.Position)/speed+1.1f,1.35f,6f);
            var shot=new LockTarget {
                id=nextId++,kind=TargetKind.Threat,hp=1,born=song,deadline=song+lifetime,
                origin=origin,direction=direction,position=origin,
                destination=origin+direction*(speed*lifetime),
                isPressureShot=true,pressureOwner=owner,pressureColor=color,
                pressureSpeed=speed,pressureLifetime=lifetime,
                previousPlayerPosition=flight.Position
            };
            shot.visual=PointCloud.Place("Dolphin pressure shot",world.NodeMesh,world.NodeMaterial,transform);
            shot.visual.transform.localScale=Vector3.one*1.4f;
            SetMarkerTint(shot.visual,color);
            Targets.Add(shot);
            SpawnedPressureShots++;
            if(owner is DolphinEncounter) DolphinPressureShots++;
            return shot;
        }

        public int LivePressureShots(UnityEngine.Object owner)
        {
            int count=0;
            foreach(var target in Targets)
                if(target.isPressureShot && target.hp>0 && target.pressureOwner==owner) count++;
            return count;
        }

        public void ClearPressureShots(UnityEngine.Object owner)
        {
            for(int i=Targets.Count-1;i>=0;i--) {
                var target=Targets[i];
                if(!target.isPressureShot || target.pressureOwner!=owner) continue;
                target.hp=target.reserved;
                Locks.Remove(target);
                if(target.visual && target.reserved==0) target.visual.SetActive(false);
                if(target.reserved==0) {
                    if(target.visual) Destroy(target.visual);
                    Targets.RemoveAt(i);
                }
            }
        }

        static void SetMarkerTint(GameObject visual,Color color)
        {
            if(!visual) return;
            var renderer=visual.GetComponent<Renderer>();
            if(!renderer) return;
            renderer.GetPropertyBlock(MarkerProperties);
            MarkerProperties.SetColor(TintId,color);
            renderer.SetPropertyBlock(MarkerProperties);
        }
    }
}
