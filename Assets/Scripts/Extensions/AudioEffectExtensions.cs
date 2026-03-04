namespace Projectiles
{
	public static class AudioEffectExtensions
	{
		public static bool PlaySound(this AudioEffect[] effects, AudioSetup setup, EForceBehaviour force = EForceBehaviour.None)
		{
			if (effects == null)
				return false;

			if (setup == null)
				return false;

			if (setup.Clips.SafeCount() == 0)
				return false;

			AudioEffect bestPlayingEffect = null;
			float bestTime = 0.5f;

			for (int i = 0; i < effects.Length; i++)
			{
				var audioEffect = effects[i];
				if (audioEffect == null)
					continue;

				if (audioEffect.IsPlaying == false)
				{
					audioEffect.Play(setup);
					return true;
				}

				bool chooseAudioEffect = false;
				var audioSource = audioEffect.AudioSource;
				if (audioSource == null)
					continue;

				switch (force)
				{
					case EForceBehaviour.ForceDifferentSetup:
						chooseAudioEffect = audioSource.time > bestTime && audioEffect.CurrentSetup != setup;
						break;
					case EForceBehaviour.ForceSameSetup:
						chooseAudioEffect = audioSource.time > bestTime && audioEffect.CurrentSetup == setup;
						break;
					case EForceBehaviour.ForceAny:
						chooseAudioEffect = audioSource.time > bestTime;
						break;
				}

				if (chooseAudioEffect == true)
				{
					bestPlayingEffect = audioEffect;
					bestTime = audioSource.time;
				}
			}

			if (force == EForceBehaviour.None)
				return false; // No free audio effect

			if (bestPlayingEffect != null)
			{
				bestPlayingEffect.Play(setup, force);
				return true;
			}

			return false;
		}
	}
}
