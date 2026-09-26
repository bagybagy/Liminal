using System;
using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public sealed class ParticleWorld : MonoBehaviour
    {
        public Material particleTemplate, ribbonMaterial;
        Material serpentMaterial, environmentMaterial;
        public Material NodeMaterial { get; private set; }
        public Mesh NodeMesh { get; private set; }
        public Mesh EnemyMesh { get; private set; }
        public int ParticleCount { get; private set; }
        readonly List<Mesh> meshes = new();
        readonly List<Material> materials = new();
        readonly Burst[] bursts = new Burst[28];
        int burstCursor;
        System.Random random = new(74192);
        float R(float min = 0, float max = 1) => min + (float)random.NextDouble() * (max-min);
        static readonly Color Ice = new(0.18f,0.92f,1.0f), Gold = new(1,0.61f,0.20f), Pearl = new(0.75f,1,0.91f);
        sealed class Burst { public Material material; public GameObject obj; public float until; }

        public void Initialize()
        {
            environmentMaterial = Material(0, 1.4f);
            serpentMaterial = Material(1, 2.3f);
            NodeMaterial = Material(0, 2.4f);
            BuildEnvironment(); BuildSerpent(); BuildSmallForms(); BuildBursts();
        }
        Material Material(int mode, float gain)
        {
            var m = new Material(particleTemplate);
            m.SetFloat("_Mode", mode); m.SetFloat("_Gain", gain);
            materials.Add(m); return m;
        }
        Mesh Mesh(PointCloud points, string name)
        {
            ParticleCount += points.Count;
            Mesh mesh = points.Build(name); meshes.Add(mesh); return mesh;
        }
        void BuildSerpent()
        {
            var p = new PointCloud();
            for (int ring = 0; ring < 510; ring++) {
                float u = ring / 510f;
                for (int j = 0; j < 92; j++) {
                    float a = j * Mathf.PI * 2 / 92 + Mathf.Sin(u*47)*0.045f;
                    float radial = 1 + R(-0.026f, 0.026f);
                    bool rib = ring % 12 < 2;
                    Color c = rib ? Gold * 0.55f : Color.Lerp(Ice, Pearl, (Mathf.Sin(a)+1)*0.5f) * R(0.36f,0.92f);
                    p.Add(new Vector3(u, a, radial), rib ? 0.074f : R(0.038f,0.068f), c, R());
                }
            }
            // Filament fins are part of the anatomy, not opaque fins with particles on top.
            for (int i = 0; i < 26500; i++) {
                float u = R(0.08f,0.96f), feather = R();
                float a = i % 3 == 0 ? Mathf.PI*0.5f : i % 3 == 1 ? 0.22f : Mathf.PI-0.22f;
                a += Mathf.Sin(u*39)*feather*0.24f;
                float spread = 1 + feather * (0.8f + Mathf.Pow(Mathf.Sin(u*24),2)*1.3f) * Mathf.Sin(u*Mathf.PI);
                Color c = Color.Lerp(Ice*0.62f, Gold*0.8f, feather*feather);
                p.Add(new Vector3(u, a, spread), R(0.033f,0.057f), c * (1-feather*0.4f), R(), feather);
            }
            for (int i = 0; i < 9500; i++) {
                float u = R(), a = R(0,Mathf.PI*2);
                p.Add(new Vector3(u,a,R(0.2f,0.85f)), R(0.025f,0.065f), Ice*R(0.1f,0.35f), R());
            }
            // Two luminous eye fields and trailing feelers distinguish the head from the tail.
            for (int i = 0; i < 2100; i++) {
                float u = R(0.03f,0.055f), a = (i % 2 == 0 ? 0.15f : 2.99f) + R(-0.17f,0.17f);
                p.Add(new Vector3(u,a,1.025f), R(0.035f,0.065f), Gold*1.9f, R());
            }
            PointCloud.Place("Leviathan / particulate anatomy", Mesh(p,"Leviathan"), serpentMaterial, transform);
        }
        void BuildEnvironment()
        {
            var stars = new PointCloud();
            for (int i = 0; i < 23000; i++) {
                var pos = new Vector3(R(-170,170), R(-75,95), R(8,230));
                float bright = Mathf.Pow(R(),5);
                stars.Add(pos, R(0.025f,0.10f), Color.Lerp(new Color(0.09f,0.27f,0.35f),Pearl,bright)*R(0.18f,0.65f), R(), 0.5f);
            }
            PointCloud.Place("Suspended matter", Mesh(stars,"Plankton"), environmentMaterial, transform);
            var bed = new PointCloud();
            for (int line = 0; line < 140; line++) {
                float x = (line-70)*1.0f;
                for (int j = 0; j < 440; j++) {
                    float z = j*0.46f-8;
                    float wx = x + Mathf.Sin(z*0.035f+x*0.014f)*8;
                    float y = -18 + Mathf.Sin(x*0.086f+z*0.025f)*2.8f + Mathf.Sin(z*0.07f)*1.3f;
                    float fade = Mathf.Lerp(0.08f,0.34f,Mathf.Pow(Mathf.Sin(line*0.038f+j*0.018f)*0.5f+0.5f,3));
                    bed.Add(new Vector3(wx,y,z), R(0.037f,0.065f), Color.Lerp(Ice,new Color(0.23f,0.38f,0.67f),j/440f)*fade,R(),0.6f);
                }
            }
            PointCloud.Place("Tidal contours", Mesh(bed,"Contours"), environmentMaterial, transform);
            var reef = new PointCloud();
            for (int i = 0; i < 34; i++) {
                Vector3 root = new((i%2==0?-1:1)*R(27,85), -19, R(28,185));
                Branch(reef, root, Vector3.up, R(5,13), 4, i%3==0?Gold*0.3f:Ice*0.27f);
            }
            PointCloud.Place("Glass reef", Mesh(reef,"Reef"), environmentMaterial, transform);
            var current = new PointCloud();
            for (int strand = 0; strand < 30; strand++) {
                for (int j = 0; j < 650; j++) {
                    float a = j/650f*Mathf.PI*1.6f + 0.4f, rr = 51+strand*0.17f;
                    Vector3 pos = new(Mathf.Cos(a)*rr, 18+Mathf.Sin(a)*rr*0.58f, 132+Mathf.Sin(a*2)*7+strand*0.4f);
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
            Shader.SetGlobalFloat("_Song",song);
            Shader.SetGlobalFloat("_Pulse",Score.Pulse(song));
            Shader.SetGlobalFloat("_Evolution",evolution);
            Shader.SetGlobalFloat("_Dissolve",dissolve);
            Shader.SetGlobalFloat("_Reduced",reduced?1:0);
            foreach(var b in bursts) if (b.obj.activeSelf && song>b.until) b.obj.SetActive(false);
        }
        public void ResetEffects() { foreach(var b in bursts) b.obj.SetActive(false); }
        void OnDestroy() { foreach(var mesh in meshes) Destroy(mesh); foreach(var mat in materials) Destroy(mat); }
    }
}
