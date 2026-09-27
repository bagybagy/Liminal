using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class MarineLife : MonoBehaviour
    {
        const int JellyTotal = 15;
        const int SchoolTotal = 7;
        const int WhaleOrganTotal = 8;
        static readonly Color Aqua = new(0.24f, 1f, 0.87f);
        static readonly Color Pearl = new(0.76f, 0.94f, 1f);
        static readonly Color Blue = new(0.28f, 0.63f, 1f);

        sealed class Creature
        {
            public GameObject visual;
            public Material material;
            public LockTarget target;
            public Vector3 home, scatterDirection;
            public float phase, scale, activeUntil, nextReady, scatterUntil;
            public bool activated;
        }

        readonly List<Creature> jellies = new();
        readonly List<Creature> schools = new();
        readonly List<Creature> resonators = new();
        readonly List<LockTarget> jellyTargets = new();
        readonly List<LockTarget> fishTargets = new();
        readonly List<LockTarget> whaleTargets = new();
        readonly HashSet<int> litJellies = new();
        readonly HashSet<int> litResonators = new();
        readonly List<Mesh> meshes = new();
        Material marineTemplate, pulseMaterial;
        Mesh pulseMesh;
        GameObject whale, pulse;
        ParticleWorld world;
        Encounter combat;
        Vector3 whalePosition;
        float pulseStarted = -100;
        int fishResponses;

        public int IlluminatedJellies => litJellies.Count;
        public int JellyCount => jellies.Count;
        public int FishResponses => fishResponses;
        public int WhaleResonance => litResonators.Count;
        public Vector3 WhalePosition => whalePosition;
        public Quaternion WhaleRotation => whale.transform.rotation;
        public IReadOnlyList<LockTarget> JellyTargets => jellyTargets;
        public IReadOnlyList<LockTarget> FishTargets => fishTargets;
        public IReadOnlyList<LockTarget> WhaleResonatorTargets => whaleTargets;
        public int ParticleCount { get; private set; }

        public void Initialize(ParticleWorld particleWorld, Encounter encounter)
        {
            world = particleWorld;
            combat = encounter;
            if (!particleWorld.marineLight)
                throw new InvalidOperationException("MarineLife requires ParticleWorld.marineLight to reference Liminal/Marine Light.");
            marineTemplate = new Material(particleWorld.marineLight) { name = "Marine life light" };
            BuildJellies();
            BuildSchools();
            BuildWhale();
            ResetLife();
        }

        void BuildJellies()
        {
            var room = CaveLayout.Rooms[0];
            for (int i = 0; i < JellyTotal; i++) {
                float lane = (i % 5 - 2) * 22f;
                Vector3 home = room.Center + new Vector3(lane, 14f + (i % 3) * 14f, -57f + i / 5 * 37f);
                if (i < 4) home = CaveLayout.Spawn + new Vector3((i - 1.5f) * 10f, 4f + i * 2f, 20f + i * 18f);
                float scale = i < 4 ? 1.25f - i * 0.09f : 0.72f + (i % 4) * 0.12f;
                var points = new PointCloud();
                BuildJelly(points, i);
                var mesh = Track(points.Build("Jellyfish bell ribs and tentacles", 70));
                var obj = PointCloud.Place("Lantern jelly " + i, mesh, NewMaterial(Pearl, 1.65f, 1), transform);
                obj.transform.localScale = Vector3.one * scale;
                var creature = new Creature { visual=obj, material=obj.GetComponent<Renderer>().sharedMaterial,
                    home=home, phase=i*1.71f, scale=scale };
                jellies.Add(creature);
            }
        }

        static void BuildJelly(PointCloud points, int index)
        {
            int ribs = 18 + index % 7;
            for (int r = 0; r < ribs; r++) {
                float a = r * Mathf.PI * 2 / ribs;
                for (int j = 0; j <= 13; j++) {
                    float u = j / 13f, radius = Mathf.Sin(u * Mathf.PI * 0.5f) * 4.6f;
                    Vector3 p = new(Mathf.Cos(a) * radius, 0.25f + Mathf.Cos(u * Mathf.PI * 0.5f) * 3.5f,
                        Mathf.Sin(a) * radius);
                    points.Add(p, 0.095f, Color.Lerp(Aqua, Pearl, u * 0.65f) * (0.56f + 0.44f * u),
                        index * 0.31f + r * 0.13f, u);
                }
            }
            for (int ring = 0; ring < 2; ring++) for (int j = 0; j < 64; j++) {
                float a = j * Mathf.PI * 2 / 64;
                float radius = ring == 0 ? 4.5f : 3.1f;
                points.Add(new Vector3(Mathf.Cos(a)*radius, 0.18f-ring*0.25f, Mathf.Sin(a)*radius),
                    ring == 0 ? 0.10f : 0.065f, ring == 0 ? Pearl*0.75f : Aqua*0.48f, a, ring);
            }
            int tentacles = 9 + index % 5;
            for (int t = 0; t < tentacles; t++) {
                float a = t * Mathf.PI * 2 / tentacles;
                float length = 8f + (t * 17 % 9) * 1.15f;
                for (int j = 0; j < 30; j++) {
                    float u = j / 29f, sway = Mathf.Sin(u * 5.2f + t * 1.7f) * u * 1.25f;
                    Vector3 p = new(Mathf.Cos(a) * (2.2f + u*0.6f) + Mathf.Cos(a+Mathf.PI/2)*sway,
                        -0.15f-u*length, Mathf.Sin(a) * (2.2f + u*0.6f) + Mathf.Sin(a+Mathf.PI/2)*sway);
                    points.Add(p, Mathf.Lerp(0.075f, 0.035f, u), Color.Lerp(Aqua, Pearl, u*0.45f)*(1-u*0.35f),
                        t*0.7f, u);
                }
            }
            for (int j = 0; j < 80; j++) {
                float y = 0.2f + (j%10)*0.26f, a=j*2.399f, radius=(j%10)*0.28f;
                points.Add(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius),0.12f,
                    Color.Lerp(Pearl,Aqua,(j%10)/10f),j*0.18f,0);
            }
        }

        void BuildSchools()
        {
            Vector3[] homes = {
                CaveLayout.Rooms[0].Center + new Vector3(36,-18,28),
                CaveLayout.Rooms[0].Center + new Vector3(-44,4,65),
                CaveLayout.Rooms[1].Center + new Vector3(60,15,-55),
                CaveLayout.Rooms[1].Center + new Vector3(-80,-20,75),
                (CaveLayout.Passages[1][1]+CaveLayout.Passages[1][2])*0.5f,
                CaveLayout.Rooms[2].Center+new Vector3(-105,28,-72),
                CaveLayout.Rooms[2].Center+new Vector3(96,-48,112)
            };
            for (int school = 0; school < SchoolTotal; school++) {
                var points = new PointCloud();
                int fishCount = school < 3 ? 58 : 50;
                for (int f = 0; f < fishCount; f++) AddFish(points, school, f, fishCount);
                var mesh = Track(points.Build("School of silver fish", 100));
                var obj = PointCloud.Place("Fish shoal " + school, mesh,
                    NewMaterial(Color.Lerp(Pearl, Blue, school/8f), 0.8f, 2), transform);
                schools.Add(new Creature { visual=obj, material=obj.GetComponent<Renderer>().sharedMaterial,
                    home=homes[school], phase=school*1.93f, scale=1 });
            }
        }

        static void AddFish(PointCloud points, int school, int fish, int count)
        {
            float a = fish * 2.399f + school * 0.31f;
            float shell = Mathf.Sqrt((fish + 0.5f) / count);
            Vector3 center = new(Mathf.Cos(a)*shell*13f, Mathf.Sin(fish*1.31f+school)*2.7f,
                Mathf.Sin(a)*shell*18f - shell*6f);
            Quaternion yaw = Quaternion.Euler(0, Mathf.Sin(a)*14f, 0);
            void Dot(Vector3 p, float size, Color col, float tailWeight) => points.Add(center+yaw*p,size,col,fish*0.47f+school,tailWeight);
            Color silver = Color.Lerp(Pearl,new Color(0.32f,0.89f,1f),((fish+school)%7)/12f);
            for (int j=0;j<8;j++) {
                float z=0.95f-j*0.27f, taper=1-Mathf.Abs(z)*0.58f;
                for(int side=-1;side<=1;side+=2)
                    Dot(new Vector3(side*(0.11f+0.13f*taper),0,z),0.105f*taper,silver,0);
                Dot(new Vector3(0,0.06f,z),0.09f*taper,silver,0);
            }
            for(int j=0;j<4;j++) {
                float z=-1.12f-j*0.23f, width=(j+1)*0.095f;
                Dot(new Vector3(width,0,z),0.09f,Color.Lerp(silver,Aqua,0.35f),1);
                Dot(new Vector3(-width,0,z),0.09f,Color.Lerp(silver,Aqua,0.35f),1);
                Dot(new Vector3(0,0.38f+j*0.03f,z),0.075f,silver,1);
            }
            Dot(new Vector3(0,0,-2.05f),0.09f,Pearl,0);
            Dot(new Vector3(0,0.23f,-1.95f),0.075f,Aqua,0);
            Dot(new Vector3(0,0,0.7f),0.06f,new Color(0.06f,0.18f,0.23f),0);
        }

        void BuildWhale()
        {
            var points = new PointCloud();
            var random = new System.Random(72941);
            float R(float min,float max) => min+(float)random.NextDouble()*(max-min);
            const int body = 68000;
            for (int i=0;i<body;i++) {
                float z=R(-82,76);
                Vector2 radius=WhaleRadius(z);
                float width=radius.x, height=radius.y;
                float a=R(0,Mathf.PI*2), shell=R(0.86f,1.04f);
                Vector3 p=new(Mathf.Cos(a)*width*shell,Mathf.Sin(a)*height*shell,z);
                float pleat=0.5f+0.5f*Mathf.Cos(a*9+z*0.045f);
                Color c=Color.Lerp(new Color(0.30f,0.58f,0.85f),new Color(0.85f,0.94f,1f),Mathf.Pow(pleat,12)*0.85f);
                if(Mathf.Sin(a)<-0.58f) c=Color.Lerp(c,new Color(0.36f,0.72f,1f),0.43f);
                points.Add(p,R(0.08f,0.19f),c*R(0.64f,1.12f),R(0,1),Mathf.Abs(z)/82f);
            }
            AddWhaleFin(points, random, new Vector3(-10,-5,4), -1);
            AddWhaleFin(points, random, new Vector3(10,-5,4), 1);
            AddFluke(points, random);
            for(int side=-1;side<=1;side+=2) {
                for(int j=0;j<220;j++) {
                    float u=j/219f,z=47+u*27;
                    float x=side*(3.5f+u*4.2f),y=2.2f+u*0.25f;
                    points.Add(new Vector3(x,y,z),0.16f,Color.Lerp(Pearl,new Color(0.34f,0.88f,1f),u)*0.8f,j,0);
                }
                for(int j=0;j<48;j++) {
                    float a=j*Mathf.PI*2/48;
                    points.Add(new Vector3(side*6.1f+Mathf.Cos(a)*0.46f,3.2f+Mathf.Sin(a)*0.46f,65.5f),0.21f,Pearl,j,0);
                }
            }
            var mesh=Track(points.Build("Horizon whale, pleated luminous skin",260));
            whale=PointCloud.Place("THE HORIZON WHALE",mesh,NewMaterial(new Color(0.85f,0.93f,1f),1.65f,3),transform);

            var organPoints=new PointCloud();
            for(int i=0;i<90;i++) {
                float a=i*Mathf.PI*2/90;
                organPoints.Add(new Vector3(Mathf.Cos(a)*0.52f,Mathf.Sin(a)*0.52f,0),0.10f,
                    Color.Lerp(Aqua,Pearl,(Mathf.Sin(a)+1)*0.5f),a,0);
                organPoints.Add(new Vector3(Mathf.Cos(a)*0.24f,Mathf.Sin(a)*0.24f,0),0.10f,Pearl,a,0);
            }
            var organMesh=Track(organPoints.Build("Whale resonance pearl",4));
            for(int i=0;i<WhaleOrganTotal;i++) {
                var obj=PointCloud.Place("Whale resonator " + (i+1),organMesh,NewMaterial(Aqua,2.1f,0),transform);
                var c=new Creature {visual=obj,material=obj.GetComponent<Renderer>().sharedMaterial,phase=i*1.3f};
                resonators.Add(c);
            }
            var ring=new PointCloud();
            for(int i=0;i<720;i++) {
                float a=i*Mathf.PI*2/720;
                ring.Add(new Vector3(Mathf.Cos(a)*12,Mathf.Sin(a)*12,0),0.16f,
                    Color.Lerp(Aqua,Pearl,(Mathf.Sin(a*7)+1)*0.5f)*0.8f,a,0);
            }
            pulseMesh=Track(ring.Build("Whale resonance wave",40));
            pulse=PointCloud.Place("Resonance wave",pulseMesh,NewMaterial(Pearl,0,0),transform);
            pulseMaterial=pulse.GetComponent<Renderer>().sharedMaterial;
            pulse.SetActive(false);
        }

        static void AddWhaleFin(PointCloud p,System.Random random,Vector3 root,int side)
        {
            int ribs=105;
            for(int j=0;j<ribs;j++) {
                float u=j/(float)(ribs-1), x=side*(u*39f), z=-u*29f;
                float width=Mathf.Sin(u*Mathf.PI)*4.7f;
                for(int k=0;k<18;k++) {
                    float v=k/17f, y=-2.4f+v*4.8f;
                    p.Add(root+new Vector3(x+side*(v-0.5f)*width,y,z),0.13f,
                        Color.Lerp(new Color(0.24f,0.52f,0.92f),new Color(0.67f,0.86f,1f),v)*0.85f,
                        j*0.17f,v);
                }
            }
        }

        static void AddFluke(PointCloud p,System.Random random)
        {
            for(int side=-1;side<=1;side+=2) for(int j=0;j<4200;j++) {
                float u=(float)random.NextDouble(), v=(float)random.NextDouble();
                float x=side*(2f+u*27f), z=-79f+u*3f+v*(1-u)*10f;
                float y=2.5f+Mathf.Sin(u*Mathf.PI)*v*2.8f;
                p.Add(new Vector3(x,y,z),0.12f,Color.Lerp(new Color(0.25f,0.55f,0.95f),Pearl,v*0.72f),u,v);
            }
        }

        static Vector2 WhaleRadius(float z)
        {
            float tail=Mathf.Pow(Mathf.Clamp01((z+86f)/102f),0.9f);
            float head=Mathf.Sqrt(Mathf.Clamp01((77f-z)/27f));
            float width=18.5f*tail*head;
            return new Vector2(width,width*0.77f);
        }

        Mesh Track(Mesh mesh) { meshes.Add(mesh); ParticleCount += (int)(mesh.GetIndexCount(0)/6); return mesh; }
        Material NewMaterial(Color tint,float gain,float mode)
        {
            var m=new Material(marineTemplate) { name="Marine life / " + mode };
            m.SetColor("_Tint",tint); m.SetFloat("_Gain",gain); m.SetFloat("_MarineMode",mode);
            return m;
        }

        public void ResetLife()
        {
            litJellies.Clear(); litResonators.Clear(); fishResponses=0; pulseStarted=-100;
            jellyTargets.Clear(); fishTargets.Clear(); whaleTargets.Clear();
            if(pulse) pulse.SetActive(false);
            foreach(var c in jellies) { c.activated=false;c.activeUntil=0;c.nextReady=0;c.material.SetFloat("_Activation",0); }
            foreach(var c in schools) { c.scatterUntil=0;c.nextReady=0;c.visual.transform.localPosition=c.home;c.material.SetFloat("_Activation",0);c.material.SetFloat("_Scatter",0); }
            foreach(var c in resonators) { c.nextReady=0;c.material.SetFloat("_Activation",0); }
            RegisterTargets();
        }

        void RegisterTargets()
        {
            foreach(var c in jellies) jellyTargets.Add(Register(c,(target,song)=>ActivateJelly(c,target,song)));
            foreach(var c in schools) fishTargets.Add(Register(c,(target,song)=>ScatterSchool(c,target,song)));
            foreach(var c in resonators) whaleTargets.Add(Register(c,(target,song)=>LightResonator(c,target,song)));
        }

        LockTarget Register(Creature creature,Action<LockTarget,float> onHit)
        {
            creature.target=combat.RegisterEnvironment(creature.visual,onHit);
            return creature.target;
        }

        void ActivateJelly(Creature c,LockTarget target,float song)
        {
            c.activated=true;c.activeUntil=song+10f;c.nextReady=song+2.5f;
            int index=jellies.IndexOf(c);litJellies.Add(index);
            c.material.SetFloat("_Activation",1);
            if(world.Caverns) world.Caverns.Illuminate(target.position,0.9f);
            world.BurstAt(target.position,song,Aqua,0.9f);
        }

        void ScatterSchool(Creature c,LockTarget target,float song)
        {
            fishResponses++;c.scatterUntil=song+5f;c.nextReady=song+1.5f;
            Vector3 away=(target.position-CaveLayout.Rooms[CaveLayout.NearestRoom(target.position)].Center).normalized;
            if(away.sqrMagnitude<0.01f) away=Vector3.right;
            c.scatterDirection=away;
            c.material.SetFloat("_Activation",1);
            world.BurstAt(target.position,song,Pearl,0.7f);
        }

        void LightResonator(Creature c,LockTarget target,float song)
        {
            int index=resonators.IndexOf(c);
            litResonators.Add(index);c.nextReady=song+2.5f;
            c.material.SetFloat("_Activation",1);
            if(world.Caverns) world.Caverns.Illuminate(target.position,1.25f);
            world.BurstAt(target.position,song,Aqua,1.1f);
            if(litResonators.Count==WhaleOrganTotal) { pulseStarted=song;pulse.SetActive(true); }
        }

        public void Tick(float song,float dt,Vector3 player)
        {
            if(!world || !combat) return;
            Shader.SetGlobalFloat("_MarineSong",song);
            for(int i=0;i<jellies.Count;i++) {
                var c=jellies[i];
                Vector3 pos=c.home+Vector3.up*(Mathf.Sin(song*0.38f+c.phase)*2.4f)+
                    new Vector3(Mathf.Sin(song*0.19f+c.phase)*2f,0,Mathf.Cos(song*0.23f+c.phase)*2f);
                c.visual.transform.position=pos;
                c.visual.transform.rotation=Quaternion.Euler(Mathf.Sin(song*0.27f+c.phase)*4f,
                    Mathf.Sin(song*0.12f+c.phase)*9f,Mathf.Sin(song*0.21f+c.phase)*3f);
                c.material.SetFloat("_Activation",c.activated&&song<c.activeUntil?1:0);
                if(c.target!=null) { c.target.position=pos;c.target.visual.transform.position=pos;c.target.hp=song>=c.nextReady?1:0; }
            }
            for(int i=0;i<schools.Count;i++) {
                var c=schools[i];
                float scatter=Mathf.Clamp01((c.scatterUntil-song)/1.3f);
                Vector3 drift=new Vector3(Mathf.Sin(song*0.21f+c.phase)*6f,Mathf.Sin(song*0.31f+c.phase)*3f,
                    Mathf.Cos(song*0.18f+c.phase)*8f);
                Vector3 pos=c.home+drift+c.scatterDirection*(scatter*18f);
                Vector3 ignoredVelocity=Vector3.zero;
                pos=CaveLayout.Constrain(pos,ref ignoredVelocity,24f);
                c.visual.transform.position=pos;
                c.visual.transform.rotation=Quaternion.Euler(Mathf.Sin(song*0.2f+c.phase)*5f,
                    song*3f+c.phase*30f,Mathf.Sin(song*0.17f+c.phase)*4f);
                c.material.SetFloat("_Activation",scatter);
                c.material.SetFloat("_Scatter",scatter);
                if(c.target!=null) { c.target.position=pos;c.target.visual.transform.position=pos;c.target.hp=song>=c.nextReady?1:0; }
                if(song>=c.scatterUntil)c.material.SetFloat("_Activation",0);
            }
            TickWhale(song);
            for(int i=0;i<resonators.Count;i++) {
                var c=resonators[i];
                float z=Mathf.Lerp(66f,-61f,(i+0.5f)/WhaleOrganTotal);
                Vector2 radius=WhaleRadius(z)*1.08f;
                float rx=radius.x, ry=radius.y;
                float a=0.74f+(i%2)*1.8f;
                Vector3 local=new(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry,z);
                local.y+=WhaleTailBend(z,song);
                local.x+=WhaleTailOffset(z,song);
                float fin=Mathf.Clamp01((Mathf.Abs(local.x)-9f)/28f)*Mathf.Clamp01((z-1f)/15f);
                local.y+=Mathf.Sin(song*0.82f+c.phase*0.7f)*fin*0.9f;
                Vector3 pos=whale.transform.TransformPoint(local);
                c.visual.transform.position=pos;
                c.visual.transform.rotation=whale.transform.rotation;
                c.material.SetFloat("_Activation",litResonators.Contains(i)?1:0);
                if(c.target!=null) { c.target.position=pos;c.target.visual.transform.position=pos;c.target.hp=litResonators.Contains(i)||song<c.nextReady?0:1; }
            }
            if(pulse && pulse.activeSelf) {
                float age=song-pulseStarted;
                if(age>5f) pulse.SetActive(false);
                else {
                    pulse.transform.position=whalePosition;
                    pulse.transform.rotation=whale.transform.rotation;
                    pulse.transform.localScale=Vector3.one*(1+age*5f);
                    pulseMaterial.SetFloat("_Gain",Mathf.Max(0,2.2f*(1-age/5f)));
                }
            }
        }

        void TickWhale(float song)
        {
            float phase=song*0.035f;
            whalePosition=CaveLayout.Rooms[2].Center+new Vector3(Mathf.Sin(phase)*38f,
                Mathf.Sin(phase*0.71f)*17f,Mathf.Cos(phase)*48f);
            Vector3 tangent=new Vector3(Mathf.Cos(phase),Mathf.Sin(phase*0.71f)*0.08f,-Mathf.Sin(phase)).normalized;
            whale.transform.position=whalePosition;
            whale.transform.rotation=Quaternion.LookRotation(tangent,Vector3.up);
            whale.GetComponent<Renderer>().sharedMaterial.SetFloat("_Activation",litResonators.Count/(float)WhaleOrganTotal);
        }

        static float WhaleTailBend(float z,float song)
        {
            float tail=Mathf.Pow(Mathf.Clamp01((-z-8f)/74f),1.7f);
            return Mathf.Sin(song*0.78f+z*0.047f)*tail*5.5f;
        }

        static float WhaleTailOffset(float z,float song)
        {
            float tail=Mathf.Pow(Mathf.Clamp01((-z-8f)/74f),1.7f);
            return Mathf.Sin(song*0.61f+z*0.052f)*tail*2.2f;
        }

        void OnDestroy()
        {
            foreach(var mesh in meshes) if(mesh) Destroy(mesh);
            foreach(var c in jellies) if(c.material) Destroy(c.material);
            foreach(var c in schools) if(c.material) Destroy(c.material);
            foreach(var c in resonators) if(c.material) Destroy(c.material);
            if(whale) { var r=whale.GetComponent<Renderer>();if(r&&r.sharedMaterial)Destroy(r.sharedMaterial); }
            if(pulse) { var r=pulse.GetComponent<Renderer>();if(r&&r.sharedMaterial)Destroy(r.sharedMaterial); }
            if(marineTemplate) Destroy(marineTemplate);
        }
    }
}
