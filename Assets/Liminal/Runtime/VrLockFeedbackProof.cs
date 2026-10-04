using System;

namespace Liminal
{
    public static class VrLockFeedbackProof
    {
        public static string Verify(Experience game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            VrWorldHud hud = game.GetComponent<VrWorldHud>();
            if (hud == null)
                throw new InvalidOperationException("VR world HUD is missing.");
            if (!hud.ValidateLockFeedbackForProof(out string failure))
                throw new InvalidOperationException(failure);

            return "locks=" + hud.VisibleLocks + "/" + hud.LockRingCapacity +
                ", renderers=" + hud.ActiveLockRingRendererCount +
                ", material=" + hud.LockRingMaterialCreated +
                ", vrHudActive=" + hud.VrHudActive +
                ", gameplayHudHidden=" + hud.GameplayHudHidden;
        }
    }
}
