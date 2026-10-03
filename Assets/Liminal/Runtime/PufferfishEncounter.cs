using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Liminal
{
    public sealed class PufferfishEncounter : MonoBehaviour
    {
        public const int MaxFish = 2;
        public const int HitPointsPerFish = 16;
        public const int LocksPerFish = 8;
        public const int PressureBulletsPerBurst = 36;
        public const float SmallHermitReferenceRadius = 6.5f;
        public const float MaximumInflation = 3.76f;

        const float ShapeRadius = 1.52f;
        const float InitialScale = 1.6f;
        static float DeathDuration => ParticleTransitionSettings.Current.Duration()+.4f;
        public float inflationSmoothSeconds=.24f;
        const float BurstRetryWindow = 1f;
        const float BurstRetryInterval = 0.14f;
        const float SwimSpeed = 2.15f;
        const float PlayerClearance = 12f;
        const float PressureSpeed = 20f;
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int DeathProgressId = Shader.PropertyToID("_DeathProgress");
        static readonly int GainId = Shader.PropertyToID("_Gain");

        enum FishState { Empty, Alive, DeathCloud, DriftCloud }

        sealed class Fish
        {
            public FishState state;
            public GameObject body;
            public Renderer renderer;
            public LockTarget target;
            public Vector3 position, velocity, driftVelocity;
            public Quaternion rotation;
            public Color tint;
            public float age, phase, deathAge, burstDeadline, nextBurstAttempt, hitSong=-10,scaleVelocity;
            public int hits;
            public bool burstPending;
        }

        readonly Fish[] fish = { new Fish(), new Fish() };
        readonly List<LockTarget> liveTargets = new(MaxFish);
        ReadOnlyCollection<LockTarget> liveTargetView;
        Encounter combat;
        ParticleWorld world;
        Flight flight;
        Mesh fishMesh;
        Material fishMaterial;
        MaterialPropertyBlock properties;
        bool initialized;
        bool cleanupApplied;

        public IReadOnlyList<LockTarget> LiveTargets => liveTargetView ?? (IReadOnlyList<LockTarget>)Array.Empty<LockTarget>();
        public int Spawned { get; private set; }
        public int HitCount { get; private set; }
        public int Bursts { get; private set; }
        public int BurstBullets { get; private set; }
        public int ActiveFishCount => liveTargets.Count;
        public float InitialBodyRadius => BodyRadiusForHits(0);
        public bool StartsAtOrBelowHalfHermitSize => InitialBodyRadius <= SmallHermitReferenceRadius * 0.5f;

        public static float ScaleForHits(int hits)
        {
            return InitialScale * Mathf.Lerp(1,MaximumInflation,
                Mathf.SmoothStep(0,1,Mathf.Clamp01(hits/(float)HitPointsPerFish)));
        }

        public static float BodyRadiusForHits(int hits)
        {
            return ShapeRadius * ScaleForHits(hits);
        }

        public void Initialize(Encounter combat, ParticleWorld world, Flight flight)
        {
            if (initialized)
            {
                if (this.combat != combat || this.world != world || this.flight != flight)
                    throw new InvalidOperationException("PufferfishEncounter cannot be rebound after initialization.");
                ResetSchool();
                return;
            }

            if (!combat || !world || !flight || !world.NodeMaterial)
                throw new InvalidOperationException("PufferfishEncounter requires initialized combat, particles, and flight.");

            this.combat = combat;
            this.world = world;
            this.flight = flight;
            Shader shader = Resources.Load<Shader>("Pufferfish");
            if (!shader)
                throw new InvalidOperationException("Missing Resources/Pufferfish.shader.");

            fishMesh = BuildFishMesh();
            fishMaterial = new Material(shader) { name = "Pufferfish particle matter" };
            fishMaterial.SetFloat(GainId, 1.9f);
            properties = new MaterialPropertyBlock();
            liveTargetView = liveTargets.AsReadOnly();
            initialized = true;
            ResetSchool();
        }

        public void Tick(float song, float dt, bool roomActive)
        {
            if (!initialized || !combat || !world || !flight)
                return;

            dt = Mathf.Clamp(dt, 0f, 0.1f);
            bool active = roomActive && !combat.Ended && !combat.Peaceful && !combat.SerpentComplete;
            if (!active)
            {
                if (!cleanupApplied)
                {
                    combat.ClearPressureShots(this);
                    for (int i = 0; i < fish.Length; i++)
                        BeginDrift(fish[i]);
                    cleanupApplied = true;
                }
            }
            else
            {
                cleanupApplied = false;
            }

            for (int i = 0; i < fish.Length; i++)
            {
                Fish puffer = fish[i];
                if (puffer.state == FishState.Alive && active)
                    Swim(puffer, song, dt);
                else if (puffer.state == FishState.DeathCloud)
                    AdvanceDeathCloud(puffer, song, dt);
                else if (puffer.state == FishState.DriftCloud)
                    AdvanceDriftCloud(puffer, song, dt);
                if(puffer.body && puffer.state!=FishState.Empty) {
                    float scale=Mathf.SmoothDamp(puffer.body.transform.localScale.x,ScaleForHits(puffer.hits),
                        ref puffer.scaleVelocity,Mathf.Max(.05f,inflationSmoothSeconds),float.PositiveInfinity,dt);
                    puffer.body.transform.localScale=Vector3.one*scale;
                }
            }
        }

        public void ResetSchool()
        {
            if (combat)
                combat.ClearPressureShots(this);

            liveTargets.Clear();
            for (int i = 0; i < fish.Length; i++)
                ClearFish(fish[i]);

            Spawned = 0;
            HitCount = 0;
            Bursts = 0;
            BurstBullets = 0;
            cleanupApplied = false;
        }

        public bool TrySpawn(Vector3 position, float song)
        {
            if (!initialized || !combat || !world || !flight || combat.Ended || combat.Peaceful ||
                combat.SerpentComplete || (combat.ExplorationMode && combat.ActiveRoom != 1))
                return false;

            Fish puffer = null;
            for (int i = 0; i < fish.Length; i++)
            {
                if (fish[i].state != FishState.Empty)
                    continue;
                puffer = fish[i];
                break;
            }
            if (puffer == null)
                return false;

            puffer.state = FishState.Alive;
            puffer.position = position;
            puffer.velocity = Vector3.zero;
            puffer.age = 0f;
            puffer.phase = (Spawned % 2) * Mathf.PI;
            puffer.hits = 0;
            puffer.scaleVelocity=0;
            puffer.hitSong=song-10f;
            puffer.tint = Spawned % 2 == 0
                ? Color.white
                : new Color(0.9f, 1f, 0.88f, 1f);
            Vector3 facing = flight.Position - position;
            if (facing.sqrMagnitude < 0.001f)
                facing = flight.transform.forward;
            puffer.rotation = Facing(facing);

            puffer.body = new GameObject("Pufferfish particle body");
            puffer.body.transform.SetParent(transform, false);
            puffer.body.AddComponent<MeshFilter>().sharedMesh = fishMesh;
            puffer.renderer = puffer.body.AddComponent<MeshRenderer>();
            puffer.renderer.sharedMaterial = fishMaterial;
            puffer.body.transform.localScale = Vector3.one * InitialScale;
            SetPose(puffer, position, puffer.rotation);
            SetMaterialState(puffer, 0f, song);

            puffer.target = combat.RegisterEnvironment(puffer.body,
                (target, hitSong) => OnFishHit(puffer, target, hitSong));
            if (puffer.target == null)
            {
                Destroy(puffer.body);
                ClearFishState(puffer);
                return false;
            }

            puffer.target.hp = HitPointsPerFish;
            puffer.target.lockCapacity = LocksPerFish;
            puffer.target.position = position;
            liveTargets.Add(puffer.target);
            Spawned++;
            return true;
        }

        void Swim(Fish puffer, float song, float dt)
        {
            puffer.age += dt;
            Vector3 away = puffer.position - flight.Position;
            if (away.sqrMagnitude < 0.001f)
                away = -flight.transform.forward;
            else
                away.Normalize();

            Vector3 destination = flight.Position + away * PlayerClearance +
                Vector3.up * (2f + Mathf.Sin(puffer.age * 0.75f + puffer.phase) * 0.8f);
            Vector3 previous = puffer.position;
            Vector3 next = Vector3.MoveTowards(previous, destination, SwimSpeed * dt);
            puffer.velocity = dt > 0f ? (next - previous) / dt : Vector3.zero;

            Vector3 heading = destination - next;
            if (heading.sqrMagnitude < 0.01f)
                heading = puffer.velocity.sqrMagnitude > 0.01f ? puffer.velocity : puffer.rotation * Vector3.forward;
            Quaternion desiredRotation = Facing(heading);
            puffer.rotation = Quaternion.RotateTowards(puffer.rotation, desiredRotation, 40f * dt);
            SetPose(puffer, next, puffer.rotation);
            SetMaterialState(puffer,0f,song);
        }

        void OnFishHit(Fish puffer, LockTarget target, float song)
        {
            if (puffer.state != FishState.Alive || puffer.target != target)
                return;

            puffer.hits++;
            puffer.hitSong=song;
            HitCount++;
            world.BurstAt(puffer.position,song,new Color(.06f,.62f,1f),.55f,puffer.velocity);
            if (target.hp > 0)
                return;

            liveTargets.Remove(target);
            puffer.deathAge = 0f;
            puffer.state = FishState.DeathCloud;
            puffer.burstPending = false;
            if (!combat.Ended && !combat.Peaceful && !combat.SerpentComplete)
            {
                puffer.burstPending = true;
                float peak=ParticleTransitionSettings.Current.Timings.x+ParticleTransitionSettings.Current.Timings.y;
                puffer.nextBurstAttempt = song+peak;
                puffer.burstDeadline = puffer.nextBurstAttempt+BurstRetryWindow;
            }
            SetMaterialState(puffer, 0f, song);
        }

        void TryEmitDeathBurst(Fish puffer, float song)
        {
            if (!puffer.burstPending || song < puffer.nextBurstAttempt)
                return;

            int emitted = combat.RegisterRadialPressureBurst(puffer.position, song,
                new Color(0.12f, 0.82f, 0.9f), this, PressureBulletsPerBurst, PressureSpeed);
            if (emitted > 0)
            {
                puffer.burstPending = false;
                Bursts++;
                BurstBullets += emitted;
                return;
            }

            if (song >= puffer.burstDeadline)
            {
                puffer.burstPending = false;
                return;
            }
            puffer.nextBurstAttempt = Mathf.Min(song + BurstRetryInterval, puffer.burstDeadline);
        }

        void AdvanceDeathCloud(Fish puffer, float song, float dt)
        {
            puffer.deathAge += dt;
            if (puffer.burstPending)
                TryEmitDeathBurst(puffer, song);

            float progress = Mathf.Clamp01(puffer.deathAge / DeathDuration);
            Vector3 drift = Vector3.up * (Mathf.Sin(puffer.deathAge * 1.8f + puffer.phase) * 0.08f);
            SetPose(puffer, puffer.position + drift * dt, puffer.rotation);
            SetMaterialState(puffer, progress, song);
            if (progress >= 1f)
                ReleaseFish(puffer, false);
        }

        void BeginDrift(Fish puffer)
        {
            if (puffer.state == FishState.Empty || puffer.state == FishState.DriftCloud)
                return;

            puffer.burstPending = false;
            DetachTarget(puffer);
            puffer.deathAge = 0f;
            puffer.driftVelocity = Vector3.ClampMagnitude(puffer.velocity * 0.12f + Vector3.up * 0.32f, 0.8f);
            puffer.state = FishState.DriftCloud;
        }

        void AdvanceDriftCloud(Fish puffer, float song, float dt)
        {
            puffer.deathAge += dt;
            float progress = Mathf.Clamp01(puffer.deathAge / DeathDuration);
            Vector3 bob = Vector3.up * Mathf.Sin(puffer.deathAge * 1.3f + puffer.phase) * 0.05f;
            SetPose(puffer, puffer.position + (puffer.driftVelocity + bob) * dt, puffer.rotation);
            SetMaterialState(puffer, progress, song);
            if (progress >= 1f)
                ReleaseFish(puffer, true);
        }

        void ReleaseFish(Fish puffer, bool targetAlreadyDetached)
        {
            if (!targetAlreadyDetached)
                DetachTarget(puffer);
            if (puffer.body)
                Destroy(puffer.body);
            ClearFishState(puffer);
        }

        void DetachTarget(Fish puffer)
        {
            LockTarget target = puffer.target;
            if (target == null)
                return;

            liveTargets.Remove(target);
            puffer.target = null;
            if (puffer.body && target.visual == puffer.body)
                target.visual = null;
            if (combat)
                combat.UnregisterEnvironment(target);
        }

        void ClearFish(Fish puffer)
        {
            if (puffer.target != null)
            {
                LockTarget target = puffer.target;
                puffer.target = null;
                if (combat)
                    combat.UnregisterEnvironment(target);
            }
            if (puffer.body)
                Destroy(puffer.body);
            ClearFishState(puffer);
        }

        static void ClearFishState(Fish puffer)
        {
            puffer.state = FishState.Empty;
            puffer.body = null;
            puffer.renderer = null;
            puffer.target = null;
            puffer.position = Vector3.zero;
            puffer.velocity = Vector3.zero;
            puffer.driftVelocity = Vector3.zero;
            puffer.rotation = Quaternion.identity;
            puffer.age = puffer.phase = puffer.deathAge = 0f;
            puffer.burstDeadline = puffer.nextBurstAttempt = 0f;
            puffer.hits = 0;
            puffer.burstPending = false;
        }

        void SetPose(Fish puffer, Vector3 position, Quaternion rotation)
        {
            puffer.position = position;
            puffer.rotation = rotation;
            if (puffer.target != null)
                puffer.target.position = position;
            if (puffer.body)
                puffer.body.transform.SetPositionAndRotation(position, rotation);
        }

        void SetMaterialState(Fish puffer, float deathProgress, float song)
        {
            if (!puffer.renderer)
                return;
            properties.Clear();
            properties.SetColor(TintId, puffer.tint);
            properties.SetFloat(DeathProgressId, deathProgress);
            properties.SetFloat("_DeathAge",puffer.deathAge);
            properties.SetFloat("_HitAge",Mathf.Max(0,song-puffer.hitSong));
            properties.SetVector("_FlowVelocity",Quaternion.Inverse(puffer.rotation)*puffer.velocity);
            puffer.renderer.SetPropertyBlock(properties);
        }

        static Quaternion Facing(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f)
                return Quaternion.identity;
            direction.Normalize();
            Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.97f
                ? Vector3.forward
                : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }

        static Mesh BuildFishMesh()
        {
            var points = new PointCloud();
            const int surfaceCount = 520;
            const int innerCount = 250;
            for (int i = 0; i < surfaceCount + innerCount; i++)
            {
                Vector3 direction = i<surfaceCount?FibonacciDirection(i,surfaceCount):
                    FibonacciDirection(i-surfaceCount,innerCount);
                float radius = i < surfaceCount
                    ? 0.92f + 0.08f * Mathf.Sin(i * 1.73f)
                    : Mathf.Lerp(0.42f, 0.9f, (i - surfaceCount + 0.5f) / innerCount);
                Vector3 position = Vector3.Scale(direction * radius, new Vector3(0.78f, 0.74f, 0.84f));
                Color color = BodyColor(position, i);
                float size = i < surfaceCount ? 0.052f + (i % 5) * 0.006f : 0.043f + (i % 4) * 0.005f;
                points.Add(position, size, color, i / (float)(surfaceCount + innerCount), 0.035f);
            }

            AddSpines(points);
            AddPectoralFin(points, -1f);
            AddPectoralFin(points, 1f);
            AddTailFin(points);
            AddDorsalFin(points, 1f);
            AddDorsalFin(points, -1f);
            AddFace(points, -1f);
            AddFace(points, 1f);
            AddGills(points, -1f);
            AddGills(points, 1f);
            return points.Build("Pufferfish spined particle body", 8f);
        }

        static Vector3 FibonacciDirection(int index, int count)
        {
            float y = 1f - 2f * (index + 0.5f) / count;
            float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * 2.39996323f;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        static Color BodyColor(Vector3 position, int index)
        {
            float belly = Mathf.InverseLerp(0.15f, -0.55f, position.y);
            Color dorsal = new(0.08f, 0.48f, 0.66f);
            Color underside = new(0.9f, 0.68f, 0.36f);
            Color color = Color.Lerp(dorsal, underside, belly * 0.78f);
            if (index % 31 == 0)
                color = Color.Lerp(color, new Color(1f, 0.8f, 0.36f), 0.72f);
            else if (index % 17 == 0)
                color = Color.Lerp(color, new Color(0.38f, 0.94f, 0.79f), 0.55f);
            return color;
        }

        static void AddSpines(PointCloud points)
        {
            const int spineCount = 34;
            for (int i = 0; i < spineCount; i++)
            {
                Vector3 direction = FibonacciDirection(i + 7, spineCount + 14);
                if (direction.z > 0.76f && Mathf.Abs(direction.x) < 0.52f)
                    continue;
                Vector3 normal = new Vector3(direction.x / 0.78f, direction.y / 0.74f, direction.z / 0.84f).normalized;
                Vector3 start = Vector3.Scale(direction, new Vector3(0.78f, 0.74f, 0.84f)) * 0.9f;
                float length = 0.23f + Mathf.Max(0f, direction.y) * 0.13f + (i % 3) * 0.025f;
                for (int segment = 0; segment <= 5; segment++)
                {
                    float t = segment / 5f;
                    Vector3 position = start + normal * (length * t);
                    Color color = Color.Lerp(new Color(0.18f, 0.8f, 0.76f),
                        new Color(1f, 0.65f, 0.25f), Mathf.SmoothStep(0.3f, 1f, t));
                    points.Add(position, Mathf.Lerp(0.047f, 0.03f, t), color, i / (float)spineCount);
                }
            }
        }

        static void AddPectoralFin(PointCloud points, float side)
        {
            for (int ray = 0; ray <= 8; ray++)
            {
                float spread = ray / 8f * 2f - 1f;
                Vector3 root = new Vector3(side * 0.62f, -0.04f + Mathf.Abs(spread) * 0.06f, 0.08f);
                Vector3 tip = new Vector3(side * (0.94f + (1f - Mathf.Abs(spread)) * 0.24f),
                    -0.1f + spread * 0.22f, -0.44f - (1f - Mathf.Abs(spread)) * 0.13f);
                for (int segment = 0; segment <= 10; segment++)
                {
                    float t = segment / 10f;
                    Vector3 position = Vector3.Lerp(root, tip, t);
                    Color color = Color.Lerp(new Color(0.11f, 0.67f, 0.75f),
                        new Color(0.96f, 0.67f, 0.31f), t * 0.48f);
                    points.Add(position, 0.04f + Mathf.Sin(t * Mathf.PI) * 0.018f, color,
                        ray / 9f, 0.045f);
                }
            }
        }

        static void AddTailFin(PointCloud points)
        {
            for (int ray = 0; ray < 11; ray++)
            {
                float angle = ray / 10f * Mathf.PI - Mathf.PI * 0.5f;
                for (int segment = 0; segment <= 10; segment++)
                {
                    float t = segment / 10f;
                    Vector3 position = new Vector3(Mathf.Sin(angle) * t * 0.43f,
                        Mathf.Cos(angle) * t * 0.34f, -0.76f - t * 0.61f);
                    Color color = ray % 2 == 0
                        ? new Color(0.12f, 0.69f, 0.78f)
                        : new Color(0.94f, 0.62f, 0.29f);
                    points.Add(position, 0.039f + (1f - t) * 0.018f, color, ray / 11f, 0.07f);
                }
            }
        }

        static void AddDorsalFin(PointCloud points, float verticalSide)
        {
            for (int ray = 0; ray <= 5; ray++)
            {
                float x = (ray / 5f - 0.5f) * 0.5f;
                for (int segment = 0; segment <= 6; segment++)
                {
                    float t = segment / 6f;
                    Vector3 position = new Vector3(x * (1f - t * 0.45f),
                        verticalSide * (0.56f + t * 0.3f), -0.2f - t * 0.16f);
                    points.Add(position, 0.043f - t * 0.01f,
                        Color.Lerp(new Color(0.12f, 0.68f, 0.75f), new Color(1f, 0.68f, 0.29f), t * 0.5f),
                        ray / 6f, 0.04f);
                }
            }
        }

        static void AddFace(PointCloud points, float side)
        {
            Vector3 eye = new Vector3(side * 0.27f, 0.18f, 0.77f);
            points.Add(eye, 0.11f, new Color(0.94f, 0.81f, 0.48f));
            points.Add(eye + new Vector3(0f, 0f, 0.035f), 0.072f, new Color(0.025f, 0.09f, 0.15f));
            points.Add(eye + new Vector3(-side * 0.018f, 0.024f, 0.065f), 0.026f, Color.white);
            for (int i = 0; i <= 4; i++)
            {
                float x = (i / 4f - 0.5f) * 0.22f;
                Vector3 mouth = new Vector3(x, -0.28f - Mathf.Sin(i / 4f * Mathf.PI) * 0.035f, 0.81f);
                points.Add(mouth, 0.044f, new Color(1f, 0.65f, 0.39f), i / 4f);
            }
        }

        static void AddGills(PointCloud points, float side)
        {
            for (int rib = 0; rib < 3; rib++)
            {
                for (int segment = 0; segment <= 7; segment++)
                {
                    float t = segment / 7f;
                    float arc = (t - 0.5f) * 0.42f;
                    Vector3 position = new Vector3(side * (0.7f + rib * 0.035f),
                        0.05f + Mathf.Sin(t * Mathf.PI) * 0.16f, 0.27f - rib * 0.08f + arc);
                    points.Add(position, 0.032f, new Color(0.43f, 0.95f, 0.83f), t, 0.025f);
                }
            }
        }

        void OnDestroy()
        {
            if (combat)
                combat.ClearPressureShots(this);
            liveTargets.Clear();
            for (int i = 0; i < fish.Length; i++)
                ClearFish(fish[i]);
            if (fishMesh)
                Destroy(fishMesh);
            if (fishMaterial)
                Destroy(fishMaterial);
        }
    }
}
