using System;

namespace Liminal
{
    [Flags]
    public enum BossId { None=0, Serpent=1, Hermit=2, Submarine=4, Whale=8 }

    public sealed class RunProgress
    {
        public const BossId OptionalBosses=BossId.Serpent|BossId.Hermit|BossId.Submarine;
        public BossId Defeated { get; private set; }
        public BossId EndingMask { get; private set; }
        public bool EndingStarted { get; private set; }
        public int OptionalCount => Count(Defeated & OptionalBosses);
        public bool Has(BossId boss) => (Defeated & boss)==boss;
        public bool Record(BossId boss)
        {
            if(EndingStarted || boss==BossId.None || Has(boss)) return false;
            Defeated |= boss;
            if(boss==BossId.Whale) {EndingMask=Defeated & OptionalBosses;EndingStarted=true;}
            return true;
        }
        public void Reset() {Defeated=EndingMask=BossId.None;EndingStarted=false;}
        public static int Count(BossId mask)
        {
            int value=(int)mask,count=0;
            while(value!=0) {count+=value&1;value>>=1;}
            return count;
        }
    }
}
