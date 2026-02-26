using Fusion;
using UnityEngine;

namespace Projectiles
{
    /// Fires N shots per trigger pull, then waits burst cooldown.
    public class BurstWeaponTrigger : WeaponComponent
    {
        [SerializeField] private int _shotsPerBurst = 3;
        [SerializeField] private int _burstCadence = 180;       // bursts per minute
        [SerializeField] private int _intraBurstCadence = 900;  // shots per minute inside burst
        [SerializeField] private EInputButton _fireButton = EInputButton.Fire;
        [SerializeField] private bool _fireOnKeyDownOnly = true;

        [Networked] private TickTimer _shotCooldown { get; set; }
        [Networked] private TickTimer _burstCooldown { get; set; }
        [Networked] private int _pendingShots { get; set; }

        private int _shotTicks;
        private int _burstTicks;

        public override bool IsBusy {
            get {
                if (_pendingShots > 0)
                    return _shotCooldown.ExpiredOrNotRunning(Runner) == false;

                return _burstCooldown.ExpiredOrNotRunning(Runner) == false;
            }
        }

        public override bool CanFire()
        {
            if (Weapon.IsBusy())
                return false;

            // Continue burst even if button no longer held.
            if (_pendingShots > 0)
                return _shotCooldown.ExpiredOrNotRunning(Runner);

            if (_burstCooldown.ExpiredOrNotRunning(Runner) == false)
                return false;

            return _fireOnKeyDownOnly
                ? PressedButtons.IsSet(_fireButton)
                : Buttons.IsSet(_fireButton);
        }

        public override void Fire()
        {
            if (_pendingShots <= 0)
                _pendingShots = Mathf.Max(1, _shotsPerBurst);

            _pendingShots--;

            if (_pendingShots > 0)
                _shotCooldown = TickTimer.CreateFromTicks(Runner, _shotTicks);
            else
                _burstCooldown = TickTimer.CreateFromTicks(Runner, _burstTicks);
        }

        public override void Spawned()
        {
            base.Spawned();

            _shotsPerBurst = Mathf.Max(1, _shotsPerBurst);

            float intraShotTime = 60f / Mathf.Max(1, _intraBurstCadence);
            float burstTime = 60f / Mathf.Max(1, _burstCadence);

            _shotTicks = Mathf.Max(1, Mathf.CeilToInt(intraShotTime / Runner.DeltaTime));
            _burstTicks = Mathf.Max(1, Mathf.CeilToInt(burstTime / Runner.DeltaTime));
        }
    }
}