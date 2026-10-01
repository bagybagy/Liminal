using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    [DisallowMultipleComponent]
    public sealed class CorridorShoals : MonoBehaviour
    {
        const int SchoolsPerPassage = 3;
        const int FishPerSchool = 7;
        const float ScatterSeconds = 3.6f;
        const float ProximityRange = 31f;
        const float RearmSeconds = 6.5f;
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly float[] RouteFractions = { 0.32f, 0.5f, 0.68f };
        static readonly Color[] ShoalTints = {
            new(0.22f, 0.96f, 0.82f), new(0.28f, 0.70f, 1f),
            new(0.49f, 0.94f, 0.70f), new(1f, 0.60f, 0.27f)
        };

        sealed class Fish
        {
            public GameObject visual;
            public LockTarget target;
            public Shoal shoal;
            public Vector3 position, velocity, homeOffset, scatterOrigin, scatterVelocity;
            public float phase, scatterAge, rearmAt;
            public bool scattering;
        }

        sealed class Shoal
        {
            public Vector3 home, center, forward, side, up;
            public int passage;
            public float phase;
            public bool near;
            public readonly List<Fish> fish = new();
        }

        readonly List<Shoal> shoals = new();
        readonly List<Fish> fish = new();
        Vector3[] positions = System.Array.Empty<Vector3>();
        ParticleWorld world;
        Encounter combat;
        Mesh fishMesh;
        MaterialPropertyBlock tintProperties;
        bool initialized, targetsMayNeedRegistration;

        public int ShoalCount => shoals.Count;
        public int FishCount => fish.Count;
        public IReadOnlyList<Vector3> Positions => positions;

        public void Initialize(ParticleWorld owner, Encounter encounter)
        {
            if (!owner) return;
            if (initialized)
            {
                if (world != owner || (combat && encounter && combat != encounter))
                    throw new System.InvalidOperationException("Corridor shoals cannot be rebound after initialization.");
                if (!combat && encounter)
                {
                    combat = encounter;
                    RegisterTargets();
                }
                return;
            }
            if (!owner.NodeMaterial)
                throw new System.InvalidOperationException("Corridor shoals require initialized particle materials.");

            world = owner;
            combat = encounter;
            tintProperties = new MaterialPropertyBlock();
            fishMesh = BuildFishMesh();
            CreateSchools();
            positions = new Vector3[shoals.Count];
            for (int i = 0; i < shoals.Count; i++) positions[i] = shoals[i].home;
            initialized = true;
            RegisterTargets();
        }

        public void Tick(float song, float dt, Vector3 player, bool hasPlayer)
        {
            if (!initialized) return;
            if (targetsMayNeedRegistration)
            {
                RegisterTargets();
                targetsMayNeedRegistration = false;
            }
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            for (int schoolIndex = 0; schoolIndex < shoals.Count; schoolIndex++)
            {
                Shoal school = shoals[schoolIndex];
                float phase = song * 0.24f + school.phase;
                school.center = school.home + school.forward * (Mathf.Sin(phase) * 4.5f) +
                    school.side * (Mathf.Sin(song * 0.31f + school.phase) * 3.2f) +
                    school.up * (Mathf.Sin(song * 0.43f + school.phase) * 1.6f);
                positions[schoolIndex] = school.center;

                bool near = hasPlayer && (player - school.center).sqrMagnitude < ProximityRange * ProximityRange;
                if (near && !school.near) Scatter(school, player, song, false);
                school.near = near;

                foreach (Fish swimmer in school.fish)
                {
                    Vector3 bob = school.side * (Mathf.Sin(song * 0.83f + swimmer.phase) * 1.15f) +
                        school.up * (Mathf.Sin(song * 1.17f + swimmer.phase) * 0.72f);
                    Vector3 formation = school.center + swimmer.homeOffset + bob;
                    Vector3 desired = formation;
                    if (swimmer.scattering)
                    {
                        swimmer.scatterAge += dt;
                        if (swimmer.scatterAge < 0.78f)
                        {
                            swimmer.velocity = Vector3.Lerp(swimmer.scatterVelocity, Vector3.zero,
                                swimmer.scatterAge / 0.78f) + school.up * Mathf.Sin(swimmer.scatterAge * 5f + swimmer.phase);
                            desired = swimmer.position + swimmer.velocity * dt;
                        }
                        else
                        {
                            float regroup = Mathf.Clamp01((swimmer.scatterAge - 0.78f) / (ScatterSeconds - 0.78f));
                            Vector3 curl = Vector3.Cross(swimmer.homeOffset.normalized, school.up) *
                                Mathf.Sin(regroup * Mathf.PI) * 8f;
                            desired = Vector3.Lerp(swimmer.scatterOrigin + swimmer.scatterVelocity * 0.46f,
                                formation, regroup) + curl;
                            swimmer.velocity = Vector3.Lerp(swimmer.velocity,
                                (desired - swimmer.position) * 2.1f, dt * 2.8f);
                            desired = swimmer.position + swimmer.velocity * dt;
                        }
                        if (swimmer.scatterAge >= ScatterSeconds) swimmer.scattering = false;
                    }
                    else
                    {
                        swimmer.velocity = Vector3.Lerp(swimmer.velocity,
                            (desired - swimmer.position) * 1.35f, dt * 2.2f);
                        desired = swimmer.position + swimmer.velocity * dt;
                    }

                    Vector3 velocity = swimmer.velocity;
                    swimmer.position = CaveLayout.Constrain(desired, ref velocity, 8f);
                    swimmer.velocity = velocity;
                    Vector3 heading = velocity.sqrMagnitude > 1f ? velocity.normalized : school.forward;
                    swimmer.visual.transform.SetPositionAndRotation(swimmer.position,
                        Quaternion.LookRotation(heading, school.up));
                    if (swimmer.target != null)
                    {
                        swimmer.target.position = swimmer.position;
                        if (!swimmer.scattering && song >= swimmer.rearmAt &&
                            swimmer.target.hp < 1 && swimmer.target.reserved == 0)
                            swimmer.target.hp = 1;
                    }
                }
            }
        }

        public void ResetShoals()
        {
            if (!initialized) return;
            foreach (Shoal school in shoals)
            {
                school.center = school.home;
                school.near = false;
                foreach (Fish swimmer in school.fish)
                {
                    swimmer.position = school.home + swimmer.homeOffset;
                    swimmer.velocity = school.forward * 5f;
                    swimmer.scatterOrigin = swimmer.position;
                    swimmer.scatterVelocity = Vector3.zero;
                    swimmer.scatterAge = 0f;
                    swimmer.scattering = false;
                    swimmer.rearmAt = 0f;
                    swimmer.visual.transform.SetPositionAndRotation(swimmer.position,
                        Quaternion.LookRotation(school.forward, school.up));
                    if (swimmer.target != null)
                    {
                        swimmer.target.position = swimmer.position;
                        swimmer.target.hp = swimmer.target.reserved > 0 ? swimmer.target.reserved : 1;
                    }
                }
            }
            for (int i = 0; i < shoals.Count; i++) positions[i] = shoals[i].home;
            targetsMayNeedRegistration = true;
            RegisterTargets();
        }

        void CreateSchools()
        {
            for (int passage = 0; passage < CaveLayout.Passages.Length; passage++)
            for (int shoalIndex = 0; shoalIndex < SchoolsPerPassage; shoalIndex++)
            {
                RoutePose(CaveLayout.Passages[passage], RouteFractions[shoalIndex], out Vector3 home, out Vector3 forward);
                Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                Vector3 up = Vector3.Cross(forward, side).normalized;
                var school = new Shoal {
                    home = home,
                    center = home,
                    forward = forward,
                    side = side,
                    up = up,
                    passage = passage,
                    phase = Hash(passage * 13 + shoalIndex, 29) * Mathf.PI * 2f
                };
                shoals.Add(school);

                for (int index = 0; index < FishPerSchool; index++)
                {
                    int row = (index + 1) / 2;
                    float sign = index == 0 ? 0f : (index % 2 == 1 ? -1f : 1f);
                    float variation = Hash(passage * 317 + shoalIndex * 41 + index, 83);
                    Vector3 offset = -forward * (row * 3.7f) + side * (sign * row * 3.2f) +
                        up * ((variation - 0.5f) * 2.2f);
                    float scale = 1.25f + variation * 0.42f;
                    Vector3 constraintVelocity = school.forward * 5f;
                    Vector3 start = CaveLayout.Constrain(home + offset, ref constraintVelocity, 8f);
                    var visual = PointCloud.Place("Corridor fish", fishMesh, world.NodeMaterial, transform);
                    visual.transform.SetPositionAndRotation(start, Quaternion.LookRotation(school.forward, school.up));
                    visual.transform.localScale = Vector3.one * scale;
                    Renderer renderer = visual.GetComponent<Renderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    tintProperties.Clear();
                    Color tint = ShoalTints[passage];
                    if (index == 0) tint = Color.Lerp(tint, new Color(1f, 0.83f, 0.48f), 0.34f);
                    else if (index % 3 == 0) tint = Color.Lerp(tint, new Color(0.75f, 0.95f, 1f), 0.26f);
                    tintProperties.SetColor(TintId, tint);
                    renderer.SetPropertyBlock(tintProperties);

                    var swimmer = new Fish {
                        visual = visual,
                        shoal = school,
                        position = start,
                        velocity = school.forward * (4f + variation * 2f),
                        homeOffset = offset,
                        phase = variation * Mathf.PI * 2f
                    };
                    school.fish.Add(swimmer);
                    fish.Add(swimmer);
                }
            }
        }

        void RegisterTargets()
        {
            if (!combat) return;
            foreach (Fish swimmer in fish)
            {
                if (swimmer.target != null && combat.Targets.Contains(swimmer.target)) continue;
                swimmer.target = null;
                swimmer.target = combat.RegisterEnvironment(swimmer.visual,
                    (target, song) => OnFishHit(swimmer, target, song));
                if (swimmer.target != null) swimmer.target.acquireRange = Encounter.LockRange;
            }
        }

        void OnFishHit(Fish struck, LockTarget target, float song)
        {
            Scatter(struck.shoal, target.position, song, true);
            Color tint = ShoalTints[struck.shoal.passage];
            if (world.Caverns) world.Caverns.Illuminate(target.position, 0.72f, tint);
            world.BurstAt(target.position, song, tint, 0.72f);
        }

        void Scatter(Shoal school, Vector3 origin, float song, bool hit)
        {
            foreach (Fish swimmer in school.fish)
            {
                Vector3 away = swimmer.position - origin;
                if (away.sqrMagnitude < 0.01f)
                    away = Quaternion.AngleAxis(swimmer.phase * 57.3f, school.up) * school.forward;
                swimmer.scatterOrigin = swimmer.position;
                swimmer.scatterVelocity = away.normalized * (18f + 10f * Hash((int)(swimmer.phase * 1000f), 7)) +
                    school.up * (1.5f + 2.5f * Hash((int)(swimmer.phase * 500f), 31));
                swimmer.scatterAge = 0f;
                swimmer.scattering = true;
                swimmer.rearmAt = Mathf.Max(swimmer.rearmAt, song + (hit ? RearmSeconds : 3.8f));
                // Scattering removes new locks without invalidating impacts already scheduled on the music grid.
                if (swimmer.target != null) swimmer.target.hp = swimmer.target.reserved;
            }
        }

        static void RoutePose(Vector3[] route, float fraction, out Vector3 position, out Vector3 tangent)
        {
            float total = 0f;
            for (int i = 1; i < route.Length; i++) total += Vector3.Distance(route[i - 1], route[i]);
            float remaining = Mathf.Clamp01(fraction) * total;
            for (int i = 1; i < route.Length; i++)
            {
                Vector3 segment = route[i] - route[i - 1];
                float length = segment.magnitude;
                if (remaining <= length || i == route.Length - 1)
                {
                    tangent = segment.normalized;
                    position = route[i - 1] + tangent * Mathf.Min(remaining, length);
                    return;
                }
                remaining -= length;
            }
            position = route[route.Length - 1];
            tangent = (route[route.Length - 1] - route[route.Length - 2]).normalized;
        }

        static Mesh BuildFishMesh()
        {
            var cloud = new PointCloud();
            const int bodyRows = 20, bodyRings = 10;
            for (int row = 0; row < bodyRows; row++)
            {
                float along = row / (float)(bodyRows - 1);
                float belly = Mathf.Pow(Mathf.Sin(along * Mathf.PI), 0.78f);
                float z = Mathf.Lerp(-1.35f, 1.48f, along);
                float width = 0.045f + belly * 0.50f;
                float height = 0.045f + belly * 0.27f;
                for (int ring = 0; ring < bodyRings; ring++)
                {
                    float angle = ring * Mathf.PI * 2f / bodyRings;
                    Vector3 at = new(Mathf.Cos(angle) * width, Mathf.Sin(angle) * height, z);
                    Color tint = Mathf.Sin(angle) > 0.55f
                        ? new Color(0.82f, 1f, 0.96f)
                        : new Color(0.64f, 0.91f, 0.91f);
                    cloud.Add(at, 0.055f + 0.016f * belly, tint, along, 0.018f + belly * 0.025f);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int sample = 0; sample <= 18; sample++)
                {
                    float t = sample / 18f;
                    float fin = Mathf.Sin(t * Mathf.PI);
                    Vector3 pectoral = new(side * (0.34f + 0.77f * t), -0.045f + fin * 0.055f,
                        0.48f - t * 0.88f);
                    cloud.Add(pectoral, 0.064f, new Color(0.86f, 1f, 0.91f), t, 0.09f);
                    Vector3 tail = new(side * (0.045f + 0.43f * t), 0.025f * fin, -1.12f - 0.52f * t);
                    cloud.Add(tail, 0.065f, new Color(0.72f, 0.95f, 0.90f), t, 0.14f);
                }
            }
            for (int sample = 0; sample <= 12; sample++)
            {
                float t = sample / 12f;
                cloud.Add(new Vector3(0f, 0.17f + Mathf.Sin(t * Mathf.PI) * 0.25f, -0.28f + t * 0.70f),
                    0.062f, new Color(0.92f, 1f, 0.96f), t, 0.045f);
            }
            cloud.Add(new Vector3(-0.22f, 0.12f, 1.18f), 0.055f, Color.white, 0.2f);
            cloud.Add(new Vector3(0.22f, 0.12f, 1.18f), 0.055f, Color.white, 0.8f);

            Mesh mesh = cloud.Build("Corridor shoal fish", 12f);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(8f, 5f, 8f));
            return mesh;
        }

        static float Hash(int value, int salt) => Mathf.Repeat(Mathf.Sin(value * 127.1f + salt * 311.7f) * 43758.5453f, 1f);

        void OnDestroy()
        {
            foreach (Fish swimmer in fish)
            {
                if (swimmer.target != null && combat) combat.UnregisterEnvironment(swimmer.target);
                else if (swimmer.visual) Destroy(swimmer.visual);
            }
            if (fishMesh) Destroy(fishMesh);
        }
    }
}
