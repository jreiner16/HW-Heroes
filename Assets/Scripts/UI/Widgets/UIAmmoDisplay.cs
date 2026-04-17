using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// Compact ammo counter widget. Shows magazine/reserve ammo and reload progress.
	/// Only visible when the current weapon has a magazine system.
	/// </summary>
	public class UIAmmoDisplay : UIBehaviour
	{
		// PRIVATE MEMBERS

		private TextMeshProUGUI _ammoText;
		private Image _reloadFill;
		private GameObject _root;

		private int _lastMagazine = -1;
		private int _lastReserve = -1;
		private bool _lastReloading;

		private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.7f);
		private static readonly Color ReloadColor = new Color(1f, 0.8f, 0.2f, 0.8f);

		// MONOBEHAVIOUR

		protected void Awake()
		{
			BuildUI();
		}

		// PUBLIC METHODS

		public void UpdateWeapon(Weapon weapon)
		{
			if (weapon == null)
			{
				if (_root != null) _root.SetActive(false);
				return;
			}

			var magazine = weapon.GetComponentInChildren<WeaponMagazine>();
			if (magazine == null || (!magazine.HasMagazine && magazine.HasUnlimitedAmmo))
			{
				if (_root != null) _root.SetActive(false);
				return;
			}

			if (_root != null) _root.SetActive(true);

			int mag = magazine.MagazineAmmo;
			int reserve = magazine.WeaponAmmo;
			bool reloading = magazine.IsReloading;

			if (_ammoText != null && (mag != _lastMagazine || reserve != _lastReserve || reloading != _lastReloading))
			{
				if (reloading)
				{
					_ammoText.text = "RELOADING";
					_ammoText.color = ReloadColor;
				}
				else
				{
					string reserveStr = magazine.HasUnlimitedAmmo ? "\u221E" : reserve.ToString();
					_ammoText.text = magazine.HasMagazine
						? $"{mag} / {reserveStr}"
						: reserveStr;
					_ammoText.color = mag <= 5 && magazine.HasMagazine
						? new Color(0.9f, 0.3f, 0.2f, 1f)
						: new Color(0.9f, 0.9f, 0.95f, 1f);
				}

				_lastMagazine = mag;
				_lastReserve = reserve;
				_lastReloading = reloading;
			}

			if (_reloadFill != null)
			{
				if (reloading)
				{
					_reloadFill.gameObject.SetActive(true);
					_reloadFill.fillAmount = magazine.ReloadProgress;
				}
				else
				{
					_reloadFill.gameObject.SetActive(false);
				}
			}
		}

		// PRIVATE METHODS

		private void BuildUI()
		{
			_root = new GameObject("AmmoRoot");
			_root.transform.SetParent(transform, false);
			var rootRect = _root.AddComponent<RectTransform>();
			rootRect.anchorMin = Vector2.zero;
			rootRect.anchorMax = Vector2.one;
			rootRect.offsetMin = Vector2.zero;
			rootRect.offsetMax = Vector2.zero;

			UIUtility.AddBackgroundPanel(rootRect, BackgroundColor);

			// Ammo text
			var textGo = new GameObject("AmmoText");
			textGo.transform.SetParent(_root.transform, false);
			var textRect = textGo.AddComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(4f, 2f);
			textRect.offsetMax = new Vector2(-4f, -2f);
			_ammoText = textGo.AddComponent<TextMeshProUGUI>();
			_ammoText.fontSize = 18f;
			_ammoText.alignment = TextAlignmentOptions.Center;
			_ammoText.color = new Color(0.9f, 0.9f, 0.95f, 1f);
			_ammoText.raycastTarget = false;

			// Reload progress bar (thin bar at bottom)
			var reloadGo = new GameObject("ReloadBar");
			reloadGo.transform.SetParent(_root.transform, false);
			var reloadRect = reloadGo.AddComponent<RectTransform>();
			reloadRect.anchorMin = new Vector2(0f, 0f);
			reloadRect.anchorMax = new Vector2(1f, 0f);
			reloadRect.pivot = new Vector2(0f, 0f);
			reloadRect.anchoredPosition = Vector2.zero;
			reloadRect.sizeDelta = new Vector2(0f, 3f);
			_reloadFill = reloadGo.AddComponent<Image>();
			_reloadFill.color = ReloadColor;
			_reloadFill.type = Image.Type.Filled;
			_reloadFill.fillMethod = Image.FillMethod.Horizontal;
			_reloadFill.fillOrigin = (int)Image.OriginHorizontal.Left;
			_reloadFill.raycastTarget = false;
			reloadGo.SetActive(false);

			_root.SetActive(false);
		}
	}
}
