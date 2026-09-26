using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public enum TargetKind { Organ, Ray, Threat }
    public sealed class LockTarget
    {
        public int id, hp, reserved;
        public TargetKind kind;
        public float u, born, deadline;
        public Vector3 position, origin, destination;
        public GameObject visual;
        public bool Available => hp-reserved>0 && visual.activeSelf;
    }

    public sealed class Encounter : MonoBehaviour
    {
        public readonly List<LockTarget> Targets = new();
        public readonly List<LockTarget> Locks = new();
        readonly List<Shot> shots = new();
        readonly Stack<LineRenderer> lines = new();
        readonly List<LineRenderer> allLines = new();
        MusicTransport music;
        ParticleWorld world;
        Flight flight;
        int nextId, lastSection=-1, lastBeat=-1, nextWave;
        public int Points { get; private set; }
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        public int Hits { get; private set; }
        public int Fired { get; private set; }
        public int BossDamage { get; private set; }
        public int Life { get; private set; } = 8;
        public float Charge { get; private set; }
        public bool Won { get; private set; }
        public bool Lost { get; private set; }
        public bool Ended => Won||Lost;
        public float EndTime { get; private set; }
        public float DamageFlash { get; private set; }
        public float LastHitTime { get; private set; } = -10;
        public int Section { get; private set; }
        public int MaxLocks { get; private set; }
        public double MaxImpactDelay { get; private set; }
        public bool HasPending => shots.Count>0;
        public int Dodged { get; private set; }
        public int DamageTaken { get; private set; }
        public event Action Hit;
        static readonly Color Cyan = new(0.3f,1,0.91f), Amber = new(1,0.65f,0.22f);
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
            music=transport; world=particles; flight=pilot;
            for(int i=0;i<16;i++) {
                var t=new LockTarget {id=nextId++,kind=TargetKind.Organ,u=0.025f+i*0.058f};
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
            foreach(var s in shots) { s.line.enabled=false; lines.Push(s.line); }
            shots.Clear(); Locks.Clear();
            for(int i=Targets.Count-1;i>=16;i--) { Destroy(Targets[i].visual); Targets.RemoveAt(i); }
            foreach(var t in Targets) { t.hp=0;t.reserved=0;t.visual.SetActive(false); }
            Points=Combo=BestCombo=Hits=Fired=BossDamage=Dodged=DamageTaken=0;
            Life=8;Charge=0;Won=Lost=false;EndTime=0;DamageFlash=0;
            lastSection=-1;lastBeat=-1;nextWave=16;MaxImpactDelay=0;MaxLocks=0;LastHitTime=-10;
            world.ResetEffects(); flight.ResetFlight();
        }
        public void Tick(float dt,bool input)
        {
            float song=(float)music.Time;
            Section=Score.Section(song);
            DamageFlash=Mathf.MoveTowards(DamageFlash,0,dt*1.6f);
            if(Section!=lastSection) {
                if(!Ended && Section>=2 && Section<=4) foreach(var t in Targets) if(t.kind==TargetKind.Organ) {
                    t.hp+=5; t.visual.SetActive(true);
                    world.BurstAt(Anatomy.Node(t.u,song),song,Cyan,0.35f);
                }
                lastSection=Section;
            }
            int beat=Mathf.FloorToInt(song/(float)Score.BeatSeconds);
            if(beat!=lastBeat && !Ended) {
                if(beat>=nextWave && beat<376) { SpawnWave(song,beat); nextWave=beat+(Section==1?16:32); }
                if(beat>=104 && beat<384 && beat%8==0) SpawnThreat(song);
                lastBeat=beat;
            }
            UpdateTargets(song);
            UpdateShots(song);
            Locks.RemoveAll(t=>!t.Available);
            if(!Ended && input) {
                if(Input.GetMouseButton(0)) AcquireAt(Input.mousePosition);
                if(Input.GetMouseButtonUp(0)) Release();
                if(Input.GetKeyDown(KeyCode.Space)) Nova();
            }
            if(!Ended && BossDamage>=240) Finish(true,song);
            if(!Ended && (Life<=0 || song>=Score.Duration-8)) Finish(false,song);
        }
        void UpdateTargets(float song)
        {
            for(int i=Targets.Count-1;i>=0;i--) {
                var t=Targets[i];
                if(t.kind==TargetKind.Organ) t.position=Anatomy.Node(t.u,song);
                else if(t.kind==TargetKind.Ray) {
                    float age=song-t.born;
                    t.position=t.origin+new Vector3(Mathf.Sin(age*0.65f+t.u)*4,Mathf.Sin(age*0.85f+t.u)*2,-age*0.44f);
                    t.visual.transform.rotation=Quaternion.Euler(Mathf.Sin(age)*12,180+Mathf.Sin(age*0.4f)*22,Mathf.Sin(age*0.7f)*18);
                } else {
                    float f=Mathf.InverseLerp(t.born,t.deadline,song);
                    t.position=Vector3.Lerp(t.origin,t.destination,f)+Vector3.up*Mathf.Sin(f*Mathf.PI)*3;
                    t.visual.transform.localScale=Vector3.one*(1.2f+Score.Pulse(song)*0.5f);
                    if(song>=t.deadline && t.hp>0 && !Ended) {
                        if(Vector3.Distance(flight.Emitter,t.destination)<2.6f) ReceiveDamage(); else Dodged++;
                        t.hp=0;
                    }
                }
                t.visual.transform.position=t.position;
                t.visual.SetActive(t.hp>0 && !Ended);
                if(t.kind!=TargetKind.Organ && t.reserved==0 && (t.hp<=0 || song-t.born>26)) {
                    Locks.Remove(t);Destroy(t.visual);Targets.RemoveAt(i);
                }
            }
        }
        void SpawnWave(float song,int beat)
        {
            int count=Section<=1?6:4;
            for(int i=0;i<count;i++) {
                var t=new LockTarget {id=nextId++,kind=TargetKind.Ray,hp=1,born=song,u=i*0.9f};
                t.origin=new Vector3((i-(count-1)*0.5f)*5,9+Mathf.Sin(i+beat)*3,21+Mathf.Cos(i*1.7f)*4);
                t.visual=PointCloud.Place("Choir ray",world.EnemyMesh,world.NodeMaterial,transform);
                t.visual.transform.localScale=Vector3.one*0.45f;
                Targets.Add(t);
            }
        }
        void SpawnThreat(float song)
        {
            var t=new LockTarget {id=nextId++,kind=TargetKind.Threat,hp=1,born=song,deadline=song+(float)Score.BeatSeconds*8};
            t.origin=Anatomy.Center(0.05f,song);t.destination=flight.Emitter;
            t.visual=PointCloud.Place("Pressure pulse",world.NodeMesh,world.NodeMaterial,transform);
            Targets.Add(t);
        }
        public void AcquireAt(Vector2 mouse)
        {
            LockTarget best=null; float distance=Mathf.Max(34,Screen.height*0.052f);
            foreach(var t in Targets) {
                if(!t.Available || Locks.Contains(t)) continue;
                Vector3 screen=flight.View.WorldToScreenPoint(t.position);
                if(screen.z<=0) continue;
                float d=Vector2.Distance(mouse,screen);
                if(d<distance) {distance=d;best=t;}
            }
            if(best!=null) Acquire(best);
        }
        public bool Acquire(LockTarget target)
        {
            if(Ended || !target.Available || Locks.Count>=8 || Locks.Contains(target)) return false;
            Locks.Add(target); music.LockSound(); MaxLocks=Mathf.Max(MaxLocks,Locks.Count); return true;
        }
        public void Release()
        {
            if(Ended) return;
            double start=Score.NextEighth(music.Time,0.38);
            int index=0;
            foreach(var t in Locks) {
                if(!t.Available || lines.Count==0) continue;
                t.reserved++;
                double arrival=start+index*Score.BeatSeconds*0.5;
                var line=lines.Pop();line.enabled=true;line.widthMultiplier=0.10f;
                Color color=Color.Lerp(Cyan,Amber,index/7f)*2.6f;
                line.startColor=new Color(color.r,color.g,color.b,0);line.endColor=color;
                shots.Add(new Shot {target=t,from=flight.Emitter,born=music.Time,arrival=arrival,index=index,line=line});
                Vector3 screen=flight.View.WorldToViewportPoint(t.position);
                music.ScheduleNote(index,arrival,(screen.x-0.5f)*1.4f);
                index++;Fired++;
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
            if(target.kind==TargetKind.Organ) BossDamage++;
            world.BurstAt(target.position,song,target.kind==TargetKind.Organ?Cyan:Amber);
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
            foreach(var t in Targets) if(t.kind==TargetKind.Threat && t.reserved==0) {world.BurstAt(t.position,(float)music.Time,Amber);t.hp=0;}
        }
        void Finish(bool won,float song)
        {
            Won=won;Lost=!won;EndTime=song;Locks.Clear();
            if(won) for(int i=0;i<16;i++) world.BurstAt(Anatomy.Center(i/16f,song),song,Cyan,2);
        }
        public void AbandonLocks() => Locks.Clear();
    }
}
