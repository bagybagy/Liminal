using System.Collections.Generic;
using UnityEngine;

namespace Liminal
{
    public static class CaveLayout
    {
        public readonly struct Chamber
        {
            public readonly string Name;
            public readonly Vector3 Center, Radius;
            public readonly Color Color, Accent;
            public Chamber(string name, Vector3 center, Vector3 radius, Color color, Color accent)
            { Name=name; Center=center; Radius=radius; Color=color; Accent=accent; }
        }

        public static readonly Chamber[] Rooms = {
            new("LANTERN GROTTO",new Vector3(0,210,-450),new Vector3(150,85,165),
                new Color(.12f,.83f,.76f),new Color(.83f,.98f,.88f)),
            new("SERPENT SANCTUM",new Vector3(0,10,35),new Vector3(215,110,225),
                new Color(.20f,.55f,.66f),new Color(1,.62f,.22f)),
            new("HORIZON WHALE",new Vector3(90,-360,1120),new Vector3(700,300,720),
                new Color(.24f,.44f,.86f),new Color(.80f,.88f,1)),
            new("TIDAL SHELLS",new Vector3(-1300,-600,850),new Vector3(380,160,410),
                new Color(.12f,.70f,.52f),new Color(1,.78f,.30f)),
            new("SCARLET ENGINE",new Vector3(1300,-600,850),new Vector3(520,280,560),
                new Color(.65f,.12f,.19f),new Color(1,.53f,.24f))
        };

        // The first four passage ids keep their original room pairings.
        static readonly int[] PassageSources = { 0, 1, 2, 3, 1, 1, 2 };
        static readonly int[] PassageDestinations = { 1, 2, 3, 4, 3, 4, 4 };
        static readonly int[][] RoomPassages = {
            new[] { 0 },
            new[] { 0, 1, 4, 5 },
            new[] { 1, 2, 6 },
            new[] { 2, 3, 4 },
            new[] { 3, 5, 6 }
        };
        static readonly int[] DefaultForwardPassage = { 0, 1, -1, 2, 6 };
        static readonly bool[] DefaultPassageDirection = { true, true, true, false, false };

        public const float PassageRadius=34f;
        public static int PassageCount => Passages.Length;
        public static float HorizonSurfaceY => Rooms[2].Center.y+65f;
        public static Bounds WorldBounds {
            get {
                var bounds=new Bounds(Rooms[0].Center,Rooms[0].Radius*2.16f);
                for(int i=1;i<Rooms.Length;i++) bounds.Encapsulate(new Bounds(Rooms[i].Center,Rooms[i].Radius*2.16f));
                return bounds;
            }
        }

        public static readonly Vector3[][] Passages = {
            new[] {new Vector3(0,180,-360),new Vector3(60,130,-285),new Vector3(55,65,-195),new Vector3(0,35,-115)},
            new[] {new Vector3(110,-25,175),new Vector3(190,-95,320),new Vector3(170,-190,430),new Vector3(90,-315,570)},
            new[] {
                new Vector3(90,-420,1160),new Vector3(-260,-420,1170),new Vector3(-610,-440,1100),
                new Vector3(-850,-500,1000),new Vector3(-1080,-560,930),new Vector3(-1300,-600,850)
            },
            new[] {
                new Vector3(-1300,-600,850),new Vector3(-1220,-700,820),new Vector3(-900,-760,790),
                new Vector3(-450,-760,780),new Vector3(0,-760,790),new Vector3(450,-760,780),
                new Vector3(900,-760,790),new Vector3(1220,-700,820),new Vector3(1300,-600,850)
            },
            new[] {
                new Vector3(-120,-20,140),new Vector3(-300,-90,225),new Vector3(-520,-240,300),
                new Vector3(-760,-410,430),new Vector3(-990,-510,590),new Vector3(-1180,-580,760),
                new Vector3(-1300,-600,850)
            },
            new[] {
                new Vector3(120,0,100),new Vector3(300,-90,200),new Vector3(520,-240,300),
                new Vector3(760,-410,430),new Vector3(990,-510,590),new Vector3(1180,-580,760),
                new Vector3(1300,-600,850)
            },
            new[] {
                new Vector3(500,-360,1180),new Vector3(700,-370,1160),new Vector3(960,-400,1080),
                new Vector3(1210,-500,960),new Vector3(1300,-600,850)
            }
        };

        public static Vector3 Spawn => Rooms[0].Center+new Vector3(0,-4,-70);
        public static Quaternion SpawnRotation => Quaternion.LookRotation(new Vector3(0,-.05f,1));

        public static int FromRoom(int passage) => PassageSources[passage];
        public static int ToRoom(int passage) => PassageDestinations[passage];
        public static int Destination(int passage,bool forward) => forward ? ToRoom(passage) : FromRoom(passage);
        public static IReadOnlyList<int> IncidentPassages(int room) => RoomPassages[room];

        public static float RoomDistance(int room,Vector3 position)
        {
            var c=Rooms[room];
            Vector3 p=position-c.Center;
            return new Vector3(p.x/c.Radius.x,p.y/c.Radius.y,p.z/c.Radius.z).magnitude;
        }

        public static int NearestRoom(Vector3 position)
        {
            int room=0; float best=float.MaxValue;
            for(int i=0;i<Rooms.Length;i++) {
                float d=RoomDistance(i,position);
                if(d<best) {best=d;room=i;}
            }
            return room;
        }

        public static Vector3 ClosestOnSegment(Vector3 point,Vector3 a,Vector3 b)
        {
            Vector3 delta=b-a;
            return a+delta*Mathf.Clamp01(Vector3.Dot(point-a,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
        }

        public static bool InPassage(Vector3 point,float padding=0)
        {
            float radius=PassageRadius-padding;
            foreach(var route in Passages)
                for(int i=1;i<route.Length;i++)
                    if((point-ClosestOnSegment(point,route[i-1],route[i])).sqrMagnitude<radius*radius) return true;
            return false;
        }

        public static bool Contains(Vector3 point,float padding=0)
        {
            foreach(var room in Rooms) {
                Vector3 p=point-room.Center,r=room.Radius-Vector3.one*padding;
                if(new Vector3(p.x/r.x,p.y/r.y,p.z/r.z).sqrMagnitude<=1) return true;
            }
            return InPassage(point,padding);
        }

        public static bool LineOfSight(Vector3 from,Vector3 to)
        {
            int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/8f);
            for(int i=1;i<steps;i++) if(!Contains(Vector3.Lerp(from,to,i/(float)steps))) return false;
            return true;
        }

        // The same volumes define rendered openings and swimmer boundaries.
        public static Vector3 Constrain(Vector3 desired,ref Vector3 velocity,float padding=3f)
        {
            padding=Mathf.Clamp(padding,0,PassageRadius-1);
            if(Contains(desired,padding)) return desired;
            Vector3 nearest=desired; float best=float.MaxValue;
            foreach(var room in Rooms) {
                Vector3 r=room.Radius-Vector3.one*padding,d=desired-room.Center;
                float scale=new Vector3(d.x/r.x,d.y/r.y,d.z/r.z).magnitude;
                Vector3 point=room.Center+d/Mathf.Max(1,scale);
                float distance=(desired-point).sqrMagnitude;
                if(distance<best) {best=distance;nearest=point;}
            }
            foreach(var route in Passages) for(int i=1;i<route.Length;i++) {
                Vector3 c=ClosestOnSegment(desired,route[i-1],route[i]);
                Vector3 point=c+Vector3.ClampMagnitude(desired-c,PassageRadius-padding);
                float distance=(desired-point).sqrMagnitude;
                if(distance<best) {best=distance;nearest=point;}
            }
            Vector3 outward=(desired-nearest).normalized;
            float speed=Vector3.Dot(velocity,outward);
            if(speed>0) velocity-=outward*speed;
            return nearest;
        }

        public static Vector3 ForwardWaypoint(Vector3 position,int room)
        {
            int passage=DefaultForwardPassage[room];
            if(passage<0) return Rooms[room].Center;
            bool forward=DefaultPassageDirection[room];
            GetPortal(passage,forward,out Vector3 portal,out _);
            if(RoomDistance(room,position)<=1f) return portal;

            Vector3[] route=Passages[passage];
            FindClosestRoutePoint(position,route,out int segment,out _,out _);
            return forward ? route[Mathf.Min(route.Length-1,segment+1)] : route[Mathf.Max(0,segment-1)];
        }

        public static void GetPortal(int passage,bool forward,out Vector3 position,out Vector3 direction)
        {
            Vector3[] route=Passages[passage];
            int room=forward?FromRoom(passage):ToRoom(passage);
            int segments=route.Length-1;
            position=forward?route[0]:route[route.Length-1];
            direction=forward?(route[1]-route[0]).normalized:(route[route.Length-2]-route[route.Length-1]).normalized;
            for(int step=1;step<=segments;step++) {
                int a=forward?step-1:route.Length-step;
                int b=forward?step:route.Length-step-1;
                if(RoomDistance(room,route[a])>1f || RoomDistance(room,route[b])<=1f) continue;
                float low=0f,high=1f;
                for(int iteration=0;iteration<20;iteration++) {
                    float middle=(low+high)*.5f;
                    if(RoomDistance(room,Vector3.Lerp(route[a],route[b],middle))<=1f) low=middle;
                    else high=middle;
                }
                position=Vector3.Lerp(route[a],route[b],(low+high)*.5f);
                direction=(route[b]-route[a]).normalized;
                return;
            }
        }

        public static bool NextPassage(Vector3 position,out Vector3 waypoint,out int destination)
        {
            int room=NearestRoom(position);
            waypoint=position;destination=room;
            if(RoomDistance(room,position)<=1f) {
                int passage=DefaultForwardPassage[room];
                if(passage<0) return false;
                bool defaultDirection=DefaultPassageDirection[room];
                GetPortal(passage,defaultDirection,out waypoint,out _);
                destination=Destination(passage,defaultDirection);
                return true;
            }

            float best=float.MaxValue;
            int nearestPassage=-1,nearestSegment=0;
            float nearestT=0f,nearestProgress=0f,nearestLength=0f;
            for(int passage=0;passage<PassageCount;passage++) {
                Vector3[] route=Passages[passage];
                FindClosestRoutePoint(position,route,out int segment,out float t,out float distance);
                if(distance>=best) continue;
                best=distance;
                nearestPassage=passage;
                nearestSegment=segment;
                nearestT=t;
                nearestProgress=RouteDistance(route,segment,t);
                nearestLength=RouteLength(route);
            }
            if(nearestPassage<0) return false;

            Vector3[] nearestRoute=Passages[nearestPassage];
            bool forward=nearestProgress<=nearestLength*.5f;
            destination=Destination(nearestPassage,forward);
            if(forward) {
                int waypointIndex=nearestSegment;
                if(nearestT>.85f && waypointIndex<nearestRoute.Length-1) waypointIndex++;
                waypoint=nearestRoute[Mathf.Min(waypointIndex,nearestRoute.Length-1)];
            }
            else {
                int waypointIndex=nearestSegment-1;
                if(nearestT<.15f && waypointIndex>0) waypointIndex--;
                waypoint=nearestRoute[Mathf.Max(0,waypointIndex)];
            }
            return true;
        }

        static void FindClosestRoutePoint(Vector3 point,Vector3[] route,out int segment,out float t,out float distanceSquared)
        {
            segment=1;t=0f;distanceSquared=float.MaxValue;
            for(int i=1;i<route.Length;i++) {
                Vector3 closest=ClosestOnSegment(point,route[i-1],route[i]);
                float distance=(point-closest).sqrMagnitude;
                if(distance>=distanceSquared) continue;
                segment=i;
                Vector3 delta=route[i]-route[i-1];
                t=Mathf.Clamp01(Vector3.Dot(closest-route[i-1],delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                distanceSquared=distance;
            }
        }

        static float RouteDistance(Vector3[] route,int segment,float t)
        {
            float distance=0f;
            for(int i=1;i<segment;i++) distance+=Vector3.Distance(route[i-1],route[i]);
            return distance+Vector3.Distance(route[segment-1],route[segment])*t;
        }

        static float RouteLength(Vector3[] route)
        {
            float distance=0f;
            for(int i=1;i<route.Length;i++) distance+=Vector3.Distance(route[i-1],route[i]);
            return distance;
        }
    }
}
