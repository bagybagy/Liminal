using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class ParticleWorld : MonoBehaviour
    {
        public Material particleTemplate, ribbonMaterial, cavernSurface, marineLight;
        public Material advectedParticles, membrane;
        public ComputeShader particleSimulation;
        Material environmentMaterial;
        public LeviathanVfx Serpent { get; private set; }
        public Material NodeMaterial { get; private set; }
        public Mesh NodeMesh { get; private set; }
        public Mesh EnemyMesh { get; private set; }
        int particleCount;
        public int ParticleCount => particleCount + (Caverns ? Caverns.ParticleCount : 0) + LeviathanVfx.SimulatedParticles;
        public CaveEnvironment Caverns { get; private set; }
        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly Burst[] bursts = new Burst[28];
        int burstCursor;
        System.Random random = new(74192);
        float R(float min = 0, float max = 1) => min + (float)random.NextDouble() * (max-min);
        static readonly Color Ice = new(0.18f,0.92f,1.0f), Gold = new(1,0.61f,0.20f), Pearl = new(0.75f,1,0.91f);
        sealed class Burst { public Material material; public GameObject obj; public float until; }

        public void Initialize(bool caverns = false)
        {
            environmentMaterial = Material(0, 1.4f);
            NodeMaterial = Material(0, 2.4f);
            if (caverns) {
                var obj = new GameObject("Cavern environment");
                obj.transform.SetParent(transform, false);
                Caverns = obj.AddComponent<CaveEnvironment>();
                Caverns.Initialize(this);
            } else BuildEnvironment();
            BuildSerpent(); BuildSmallForms(); BuildBursts();
        }
        Material Material(int mode, float gain)
        {
            var m = new Material(particleTemplate);
            m.SetFloat("_Mode", mode); m.SetFloat("_Gain", gain);
            materials.Add(m); return m;
        }
        Mesh Mesh(PointCloud points, string name)
        {
            particleCount += points.Count;
            Mesh mesh = points.Build(name); meshes.Add(mesh); return mesh;
        }
        void BuildSerpent()
        {
            var obj = new GameObject("Leviathan / GPU flow");
            obj.transform.SetParent(transform, false);
            Serpent = obj.AddComponent<LeviathanVfx>();
            Serpent.Initialize(particleSimulation, advectedParticles, membrane);
        }
        void BuildEnvironment()
        {
            var stars = new PointCloud();
            for (int i = 0; i < 23000; i++) {
                var pos = new Vector3(R(-290,290), R(-95,180), R(-250,300));
                float bright = Mathf.Pow(R(),5);
                stars.Add(pos, R(0.025f,0.10f), Color.Lerp(new Color(0.09f,0.27f,0.35f),Pearl,bright)*R(0.18f,0.65f), R(), 0.5f);
            }
            PointCloud.Place("Suspended matter", Mesh(stars,"Plankton"), environmentMaterial, transform);
            var bed = new PointCloud();
            for (int line = 0; line < 140; line++) {
                float x = (line-70)*3.8f;
                for (int j = 0; j < 440; j++) {
                    float z = j*1.22f-240;
                    float wx = x + Mathf.Sin(z*0.035f+x*0.014f)*8;
                    float y = -68 + Mathf.Sin(x*0.032f+z*0.013f)*8 + Mathf.Sin(z*0.035f)*4;
                    float fade = Mathf.Lerp(0.08f,0.34f,Mathf.Pow(Mathf.Sin(line*0.038f+j*0.018f)*0.5f+0.5f,3));
                    bed.Add(new Vector3(wx,y,z), R(0.075f,0.12f), Color.Lerp(Ice,new Color(0.23f,0.38f,0.67f),j/440f)*fade,R(),0.6f);
                }
            }
            PointCloud.Place("Tidal contours", Mesh(bed,"Contours"), environmentMaterial, transform);
            var reef = new PointCloud();
            for (int i = 0; i < 34; i++) {
                Vector3 root = new((i%2==0?-1:1)*R(38,180), -65, R(-145,195));
                Branch(reef, root, Vector3.up, R(12,28), 4, i%3==0?Gold*0.3f:Ice*0.27f);
            }
            PointCloud.Place("Glass reef", Mesh(reef,"Reef"), environmentMaterial, transform);
            var current = new PointCloud();
            for (int strand = 0; strand < 30; strand++) {
                for (int j = 0; j < 650; j++) {
                    float a = j/650f*Mathf.PI*1.6f + 0.4f, rr = 205+strand*0.23f;
                    Vector3 pos = new(Mathf.Cos(a)*rr, 42+Mathf.Sin(a)*rr*0.35f, 245+Mathf.Sin(a*2)*35+strand*0.4f);
                    current.Add(pos,R(0.044f,0.075f),Color.Lerp(Ice,Gold,strand/30f)*R(0.06f,0.23f),R(),0.8f);
                }
            }
            PointCloud.Place("Distant current", Mesh(current,"Current"), environmentMaterial, transform);
        }
        void Branch(PointCloud p, Vector3 start, Vector3 direction, float length, int depth, Color color)
        {
            Vector3 bend = new(R(-0.3f,0.3f),0,R(-0.3f,0.3f));
            int count = Mathf.CeilToInt(length*25);
            for (int i = 0; i < count; i++) {
                float f = i/(float)count;
                Vector3 c = start + direction*length*f + bend*length*f*f;
                float angle = i*2.399f;
                c += new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*depth*0.045f;
                p.Add(c,R(0.037f,0.07f),color*R(0.5f,1),R(),0.4f);
            }
            if (depth <= 0) return;
            Vector3 end = start+(direction+bend)*length;
            for (int i = 0; i < 3; i++) Branch(p,end,(direction+new Vector3(R(-1,1),R(0,0.3f),R(-1,1))).normalized,length*R(0.45f,0.66f),depth-1,color*0.9f);
        }
        void BuildSmallForms()
        {
            var p = new PointCloud();
            for (int i = 0; i < 500; i++) {
                float a = R(0,Mathf.PI*2), z = R(-1,1), r = Mathf.Sqrt(1-z*z);
                p.Add(new Vector3(r*Mathf.Cos(a),z,r*Mathf.Sin(a))*0.28f,0.045f,Pearl,R());
            }
            NodeMesh = Mesh(p,"Light organ");
            var e = new PointCloud();
            for (int i = 0; i < 2400; i++) {
                float x = R(-1,1), z = R(-1,1);
                float width = Mathf.Sin((z+1)*Mathf.PI*0.5f)*2.7f;
                Vector3 pos = new(x*width,Mathf.Sin(Mathf.Abs(x)*2.8f)*0.6f,z*1.4f);
                e.Add(pos,R(0.022f,0.046f),Color.Lerp(Ice,Gold,Mathf.Abs(x)*0.85f)*R(0.6f,1.1f),R(),Mathf.Abs(x)*0.26f);
            }
            EnemyMesh = Mesh(e,"Choir ray");
        }
        void BuildBursts()
        {
            var p = new PointCloud();
            for (int i = 0; i < 850; i++) {
                Vector3 v = new(R(-1,1),R(-1,1),R(-1,1));
                p.Add(v.normalized*R(0.2f,1),R(0.032f,0.086f),Color.Lerp(Ice,Gold,R()),R());
            }
            var mesh = Mesh(p,"Radiant dispersion");
            for (int i=0;i<bursts.Length;i++) {
                var m = Material(2,2);
                var obj = PointCloud.Place("Dispersion "+i,mesh,m,transform);
                obj.SetActive(false);
                bursts[i]=new Burst { material=m,obj=obj };
            }
        }
        public void BurstAt(Vector3 position, float song, Color color, float scale=1)
        {
            var b = bursts[burstCursor++%bursts.Length];
            b.material.SetVector("_Burst",new Vector4(position.x,position.y,position.z,song));
            b.material.SetColor("_Tint",color*scale); b.obj.SetActive(true); b.until=song+2.5f;
        }
        public void Tick(float song, float evolution, float dissolve, bool reduced)
        {
            Serpent.Tick(song, evolution, dissolve);
            Shader.SetGlobalFloat("_Song",song);
            Shader.SetGlobalFloat("_Pulse",Score.Pulse(song));
            Shader.SetGlobalFloat("_Evolution",evolution);
            Shader.SetGlobalFloat("_Dissolve",dissolve);
            Shader.SetGlobalFloat("_Reduced",reduced?1:0);
            foreach(var b in bursts) if (b.obj.activeSelf && song>b.until) b.obj.SetActive(false);
        }
        public void ResetEffects() { foreach(var b in bursts) b.obj.SetActive(false); if(Serpent) Serpent.ResetSimulation(); }
        void OnDestroy() { foreach(var mesh in meshes) Destroy(mesh); foreach(var mat in materials) Destroy(mat); }
    }
}
