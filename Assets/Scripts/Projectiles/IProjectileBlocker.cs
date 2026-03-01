using Fusion;

namespace Projectiles
{
    public interface IProjectileBlocker
    {
        // Return true if this projectile should be stopped by this object.
        bool ShouldBlockProjectile(NetworkRunner runner, PlayerRef projectileOwner);
    }
}