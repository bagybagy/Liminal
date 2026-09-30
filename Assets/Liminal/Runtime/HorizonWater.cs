using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liminal
{
    public sealed class HorizonWater : MonoBehaviour
    {
        const int RingCount = 72;
        const int SideCount = 192;
        const int EventCapacity = 24;
        const int SprayBeadsPerEvent = 320;
        const int SprayVerticesPerEvent = SprayBeadsPerEvent * 6;
        const float WhaleHalfLength = 142f;
        const float EventLifetime = 14f;

        readonly Vector4[] eventData = new Vector4[EventCapacity * 2];
        GraphicsBuffer eventBuffer;
        Material surfaceMaterial, sprayMaterial;
        Mesh surfaceMesh;
        MarineLife marineLife;
        Vector3 center;
        Vector3 previousWhalePosition;
        Vector2 radii;
        int writeIndex, activeEvents;
        float previousSong = -1f, lastBirthSong = -100f, previousCenterSide;
        float whaleVisibility;
        Vector3 lastBirthPosition;
        float previousBowSide, previousSternSide, previousLeftFinSide, previousRightFinSide;
        bool haveBirthPosition, havePreviousPose, whaleWasVisible;

        public float SurfaceHeight { get; private set; }
        public int WaveEventCount => activeEvents;
        public int SurfaceCrossings { get; private set; }
        public int MajorWaveCount { get; private set; }
        public Vector3 LastMajorOrigin { get; private set; }
        public float LastMajorTime { get; private set; } = -1f;
        public bool Ready => surfaceMesh != null && eventBuffer != null && surfaceMaterial != null && sprayMaterial != null;

        public void Initialize(Material surfaceTemplate, Material sprayTemplate)
        {
            if (!surfaceTemplate || !sprayTemplate)
                throw new InvalidOperationException("Horizon Water requires surface and spray material templates.");
            var room = CaveLayout.Rooms[2];
            center = room.Center;
            SurfaceHeight = CaveLayout.HorizonSurfaceY;
            radii = new Vector2(Mathf.Min(room.Radius.x * 0.96f, 672f), Mathf.Min(room.Radius.z * 0.96f, 690f));
            surfaceMaterial = new Material(surfaceTemplate) { name = "Horizon water / runtime" };
            sprayMaterial = new Material(sprayTemplate) { name = "Horizon spray / runtime" };
            eventBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, EventCapacity * 2, 16);
            surfaceMaterial.SetBuffer("_WaterEvents", eventBuffer);
            sprayMaterial.SetBuffer("_WaterEvents", eventBuffer);
            sprayMaterial.SetInt("_SprayBeadsPerEvent", SprayBeadsPerEvent);
            surfaceMaterial.SetInt("_WaterEventCount", 0);
            sprayMaterial.SetInt("_WaterEventCount", 0);
            surfaceMaterial.SetFloat("_WhaleVisibility", 0f);
            sprayMaterial.SetFloat("_WhaleVisibility", 0f);
            surfaceMaterial.SetVector("_WaterCenter", new Vector4(center.x, SurfaceHeight, center.z, 0));
            surfaceMaterial.SetVector("_WaterRadii", new Vector4(radii.x, radii.y, 0, 0));
            surfaceMesh = CreateSurface();
            ResetWater();
            RenderPipelineManager.beginCameraRendering += Render;
        }

        public void Tick(float song, float dt, Vector3 whalePosition, Quaternion whaleRotation, Vector3 whaleVelocity, bool released)
        {
            if (!Ready) return;
            if (previousSong >= 0f && song < previousSong - 0.001f) ResetWater();
            previousSong = song;

            MarineLife life = GetMarineLife();
            bool whaleVisible = life != null && life.WhaleVisible;
            whaleVisibility = whaleVisible ? Mathf.Clamp01(life.WhaleVisibility) : 0f;
            surfaceMaterial.SetFloat("_WhaleVisibility", whaleVisibility);
            sprayMaterial.SetFloat("_WhaleVisibility", whaleVisibility);
            if (!whaleVisible)
            {
                if (whaleWasVisible || activeEvents > 0) ClearWaveEvents();
                whaleWasVisible = false;
                havePreviousPose = false;
                UploadEvents(song);
                return;
            }
            if (!whaleWasVisible)
            {
                ClearWaveEvents();
                whaleWasVisible = true;
            }

            Vector3 forward = whaleRotation * Vector3.forward;
            Vector3 bow = whalePosition + forward * WhaleHalfLength;
            Vector3 stern = whalePosition - forward * WhaleHalfLength;
            Vector3 leftFin = whalePosition + whaleRotation * (MarineLife.DeformWhaleLocal(new Vector3(-55f, -5f, 4f), song) * 1.8f);
            Vector3 rightFin = whalePosition + whaleRotation * (MarineLife.DeformWhaleLocal(new Vector3(55f, -5f, 4f), song) * 1.8f);
            float bowSide = bow.y - SurfaceHeight;
            float sternSide = stern.y - SurfaceHeight;
            float leftFinSide = leftFin.y - SurfaceHeight;
            float rightFinSide = rightFin.y - SurfaceHeight;
            float centerSide = whalePosition.y - SurfaceHeight;

            if (!released && havePreviousPose)
            {
                DetectCrossing(previousBowSide, bowSide, bow, whaleVelocity, song);
                DetectCrossing(previousSternSide, sternSide, stern, whaleVelocity, song);
                DetectCrossing(previousLeftFinSide, leftFinSide, leftFin, whaleVelocity, song);
                DetectCrossing(previousRightFinSide, rightFinSide, rightFin, whaleVelocity, song);
                Vector2 horizontalVelocity = new Vector2(whaleVelocity.x, whaleVelocity.z);
                if (life.WhaleEntranceComplete && Crossed(previousCenterSide, centerSide)
                    && horizontalVelocity.magnitude >= 12f
                    && (LastMajorTime < 0f || song - LastMajorTime >= 8f))
                {
                    float sideTravel = Mathf.Abs(previousCenterSide) + Mathf.Abs(centerSide);
                    float crossingT = sideTravel > 0.0001f ? Mathf.Abs(previousCenterSide) / sideTravel : 0.5f;
                    Vector3 crossing = Vector3.Lerp(previousWhalePosition, whalePosition, crossingT);
                    crossing.y = SurfaceHeight;
                    MajorImpact(crossing, whaleVelocity, song);
                }
            }
            previousBowSide = bowSide;
            previousSternSide = sternSide;
            previousLeftFinSide = leftFinSide;
            previousRightFinSide = rightFinSide;
            previousCenterSide = centerSide;
            previousWhalePosition = whalePosition;
            havePreviousPose = true;

            Vector3 samplePosition = whalePosition;
            float nearDistance = Mathf.Abs(centerSide);
            ConsiderSurfacePoint(bow, bowSide, ref samplePosition, ref nearDistance);
            ConsiderSurfacePoint(stern, sternSide, ref samplePosition, ref nearDistance);
            ConsiderSurfacePoint(leftFin, leftFinSide, ref samplePosition, ref nearDistance);
            ConsiderSurfacePoint(rightFin, rightFinSide, ref samplePosition, ref nearDistance);
            Vector3 sampleVelocity = whaleVelocity;
            bool nearSurface = nearDistance < 66f;
            if (!released && nearSurface)
            {
                float spacing = Vector3.Distance(samplePosition, lastBirthPosition);
                if (!haveBirthPosition || (spacing >= 28f && song - lastBirthSong >= 0.18f))
                {
                    float proximity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - nearDistance / 66f));
                    AddEvent(samplePosition, sampleVelocity, song, 0.72f * proximity);
                }
            }
            UploadEvents(song);
        }

        static bool Crossed(float before, float after) => (before < 0f && after >= 0f) || (before > 0f && after <= 0f);

        void DetectCrossing(float before, float after, Vector3 position, Vector3 velocity, float song)
        {
            if (!Crossed(before, after)) return;
            SurfaceCrossings = Mathf.Min(SurfaceCrossings + 1, 999999);
            AddEvent(position, velocity, song, 1.65f);
        }

        public void MajorImpact(Vector3 position, Vector3 velocity, float song, float strength = 4f)
        {
            if (!Ready) return;
            MarineLife life = GetMarineLife();
            if (life == null || !life.WhaleVisible) return;
            if (float.IsNaN(strength) || float.IsInfinity(strength)) strength = 4f;
            position.y = SurfaceHeight;
            AddEvent(position, velocity, song, Mathf.Clamp(strength, 3f, 8f));
        }

        MarineLife GetMarineLife()
        {
            if (marineLife == null) marineLife = GetComponent<MarineLife>();
            return marineLife;
        }

        static void ConsiderSurfacePoint(Vector3 position, float side, ref Vector3 nearest, ref float distance)
        {
            float candidate = Mathf.Abs(side);
            if (candidate >= distance) return;
            nearest = position;
            distance = candidate;
        }

        void AddEvent(Vector3 position, Vector3 velocity, float song, float strength)
        {
            int slot = writeIndex;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 direction = horizontal.sqrMagnitude > 0.01f ? horizontal.normalized : Vector3.forward;
            bool major = strength >= 3f;
            float eventSpeed = major
                ? Mathf.Clamp(58f + horizontal.magnitude * 0.24f + (strength - 3f) * 2f, 58f, 80f)
                : Mathf.Clamp(horizontal.magnitude, 0f, 28f);
            int first = slot * 2;
            eventData[first] = new Vector4(position.x, SurfaceHeight, position.z, song);
            eventData[first + 1] = new Vector4(direction.x, direction.z, eventSpeed, strength);
            writeIndex = (writeIndex + 1) % EventCapacity;
            activeEvents = Mathf.Min(activeEvents + 1, EventCapacity);
            if (major)
            {
                MajorWaveCount = Mathf.Min(MajorWaveCount + 1, 999999);
                LastMajorOrigin = position;
                LastMajorTime = song;
            }
            lastBirthPosition = position;
            lastBirthSong = song;
            haveBirthPosition = true;
        }

        void UploadEvents(float song)
        {
            int live = 0;
            for (int i = 0; i < EventCapacity; i++)
                if (eventData[i * 2 + 1].w > 0f && song - eventData[i * 2].w <= EventLifetime) live++;
            activeEvents = live;
            eventBuffer.SetData(eventData);
            surfaceMaterial.SetFloat("_Song", song);
            sprayMaterial.SetFloat("_Song", song);
            surfaceMaterial.SetInt("_WaterEventCount", EventCapacity);
            sprayMaterial.SetInt("_WaterEventCount", EventCapacity);
        }

        public void ResetWater()
        {
            ClearWaveEvents();
            SurfaceCrossings = MajorWaveCount = 0;
            LastMajorOrigin = Vector3.zero;
            LastMajorTime = -1f;
            previousSong = -1f;
            whaleVisibility = 0f;
            whaleWasVisible = false;
            if (surfaceMaterial) surfaceMaterial.SetFloat("_WhaleVisibility", 0f);
            if (sprayMaterial) sprayMaterial.SetFloat("_WhaleVisibility", 0f);
        }

        void ClearWaveEvents()
        {
            Array.Clear(eventData, 0, eventData.Length);
            if (eventBuffer != null) eventBuffer.SetData(eventData);
            writeIndex = activeEvents = 0;
            lastBirthSong = -100f;
            haveBirthPosition = havePreviousPose = false;
            if (surfaceMaterial) surfaceMaterial.SetInt("_WaterEventCount", 0);
            if (sprayMaterial) sprayMaterial.SetInt("_WaterEventCount", 0);
        }

        public float SampleHeight(float x, float z, float song)
        {
            float baseWave = Mathf.Sin(x * 0.014f + z * 0.007f + song * 0.48f) * 0.7f
                + Mathf.Sin(-x * 0.008f + z * 0.018f - song * 0.34f) * 0.44f
                + Mathf.Sin(x * 0.036f - z * 0.025f + song * 0.82f) * 0.12f;
            float events = 0f;
            for (int i = 0; i < EventCapacity; i++)
            {
                Vector4 origin = eventData[i * 2];
                Vector4 motion = eventData[i * 2 + 1];
                float age = song - origin.w;
                if (motion.w <= 0f || motion.w >= 3f || age < 0f || age > EventLifetime) continue;
                float drift = motion.z * age * 0.06f;
                float dx = x - origin.x - motion.x * drift;
                float dz = z - origin.z - motion.y * drift;
                float radius = Mathf.Sqrt(dx * dx + dz * dz);
                float rippleRadius = 3f + age * (4f + motion.z * 0.1f);
                float ripple = Mathf.Sin(radius * 0.34f - age * 2.4f)
                    * Mathf.Exp(-Mathf.Abs(radius - rippleRadius) * 0.18f) * Mathf.Exp(-age * 0.62f);
                float side = dx * motion.y - dz * motion.x;
                float backward = Mathf.Max(0f, -(dx * motion.x + dz * motion.y));
                float wakeWidth = 2.8f + backward * 0.018f;
                float wakeGate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 15f, backward)) * Mathf.Exp(-backward * 0.012f) * Mathf.Exp(-age * 0.24f);
                float shoulder = Mathf.Exp(-Mathf.Pow((Mathf.Abs(side) - backward * 0.22f) / wakeWidth, 2f)) * wakeGate;
                float channel = Mathf.Exp(-Mathf.Pow(side / (wakeWidth * 0.42f), 2f)) * wakeGate;
                float impact = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1.3f, motion.w));
                events += (ripple * (0.12f + impact * 0.15f) + shoulder * 0.42f - channel * 0.14f) * motion.w;
            }
            return SurfaceHeight + baseWave + Mathf.Clamp(events, -0.75f, 0.85f) * whaleVisibility
                + MajorWaveHeight(x, z, song);
        }

        float MajorWaveHeight(float x, float z, float song)
        {
            // Keep this expression in lockstep with MajorWaveHeight in HorizonWater.shader.
            float sum = 0f;
            for (int i = 0; i < EventCapacity; i++)
            {
                Vector4 origin = eventData[i * 2];
                Vector4 motion = eventData[i * 2 + 1];
                float age = song - origin.w;
                if (motion.w < 3f || age < 0f || age > EventLifetime) continue;
                float dx = x - origin.x;
                float dz = z - origin.z;
                float radius = Mathf.Sqrt(dx * dx + dz * dz);
                float ringRadius = 15f + motion.z * age;
                float width = 6.5f + age * 0.8f;
                float front = radius - ringRadius;
                float leading = Mathf.Exp(-(front * front) / (width * width));
                float trailingFront = radius - (ringRadius - 28f);
                float trailingWidth = width * 1.2f;
                float trailing = Mathf.Exp(-(trailingFront * trailingFront) / (trailingWidth * trailingWidth));
                float breakup = MajorWaveBreakup(dx, dz, radius, motion);
                float fade = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.8f, 9f, age))) *
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.12f, age));
                float amplitude = Mathf.Lerp(6f, 12f, Mathf.Clamp01((motion.w - 3f) / 5f));
                sum += amplitude * (leading + trailing * breakup * 0.25f) * fade;
            }
            return sum * whaleVisibility;
        }

        static float MajorWaveBreakup(float dx, float dz, float radius, Vector4 motion)
        {
            float inverseRadius = 1f / Mathf.Max(radius, 0.001f);
            float radialX = dx * inverseRadius;
            float radialZ = dz * inverseRadius;
            float along = radialX * motion.x + radialZ * motion.y;
            float across = radialX * motion.y - radialZ * motion.x;
            float cos2 = along * along - across * across;
            float sin2 = 2f * along * across;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.35f, 0.55f, cos2 * cos2 - sin2 * sin2));
        }

        public float SurfaceEnergyAt(Vector3 position, float song)
        {
            float energy = 0f;
            for (int i = 0; i < EventCapacity; i++)
            {
                Vector4 origin = eventData[i * 2];
                if (eventData[i * 2 + 1].w <= 0f) continue;
                float age = song - origin.w;
                if (age < 0f || age > EventLifetime) continue;
                Vector4 motion = eventData[i * 2 + 1];
                float drift = motion.z * age * 0.06f;
                float dx = position.x - origin.x - motion.x * drift;
                float dz = position.z - origin.z - motion.y * drift;
                float radius = Mathf.Sqrt(dx * dx + dz * dz);
                float ringRadius = 3f + age * (4f + motion.z * 0.1f);
                float ring = Mathf.Exp(-Mathf.Abs(radius - ringRadius) * 0.18f) * Mathf.Exp(-age * 0.62f);
                float side = dx * motion.y - dz * motion.x;
                float backward = Mathf.Max(0f, -(dx * motion.x + dz * motion.y));
                float wakeWidth = 2.8f + backward * 0.018f;
                float wake = Mathf.Exp(-Mathf.Pow((Mathf.Abs(side) - backward * 0.22f) / wakeWidth, 2f))
                    * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4f, 15f, backward)) * Mathf.Exp(-backward * 0.012f) * Mathf.Exp(-age * 0.24f);
                energy += (ring * 0.25f + wake * 0.42f) * motion.w;
            }
            return energy;
        }

        Mesh CreateSurface()
        {
            int vertexCount = 1 + RingCount * SideCount;
            var vertices = new Vector3[vertexCount];
            var triangles = new int[(SideCount + (RingCount - 1) * SideCount * 2) * 3];
            vertices[0] = Vector3.zero;
            for (int ring = 1; ring <= RingCount; ring++)
            {
                float r = ring / (float)RingCount;
                for (int side = 0; side < SideCount; side++)
                {
                    float angle = side * (Mathf.PI * 2f / SideCount);
                    vertices[1 + (ring - 1) * SideCount + side] = new Vector3(Mathf.Cos(angle) * radii.x * r, 0f, Mathf.Sin(angle) * radii.y * r);
                }
            }
            int index = 0;
            for (int side = 0; side < SideCount; side++)
            {
                int next = (side + 1) % SideCount;
                triangles[index++] = 0;
                triangles[index++] = 1 + side;
                triangles[index++] = 1 + next;
            }
            for (int ring = 1; ring < RingCount; ring++)
                for (int side = 0; side < SideCount; side++)
                {
                    int next = (side + 1) % SideCount;
                    int innerA = 1 + (ring - 1) * SideCount + side;
                    int innerB = 1 + (ring - 1) * SideCount + next;
                    int outerA = innerA + SideCount, outerB = innerB + SideCount;
                    triangles[index++] = innerA; triangles[index++] = outerA; triangles[index++] = innerB;
                    triangles[index++] = innerB; triangles[index++] = outerA; triangles[index++] = outerB;
                }
            var mesh = new Mesh { name = "Horizon water / bounded tessellated ellipse", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radii.x * 2f, 60f, radii.y * 2f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        void Render(ScriptableRenderContext context, Camera camera)
        {
            if (!Ready || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
            var bounds = CaveLayout.WorldBounds;
            var surfaceParams = new RenderParams(surfaceMaterial) { camera = camera, worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMesh(surfaceParams, surfaceMesh, 0, Matrix4x4.Translate(new Vector3(center.x, SurfaceHeight, center.z)));
            var sprayParams = new RenderParams(sprayMaterial) { camera = camera, worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderPrimitives(sprayParams, MeshTopology.Triangles, EventCapacity * SprayVerticesPerEvent);
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            eventBuffer?.Dispose(); eventBuffer = null;
            if (surfaceMaterial) Destroy(surfaceMaterial);
            if (sprayMaterial) Destroy(sprayMaterial);
            if (surfaceMesh) Destroy(surfaceMesh);
        }
    }
}
