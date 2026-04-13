using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// Displays team capture progress at the top of the screen.
	/// Shows "First to X" and each team's score with a clear progress bar.
	/// On game over: "You Win!" or "You Lose!" plus Exit Room button.
	/// Creates its own UI at runtime if not configured in prefab.
	/// </summary>
	public class UIScoreHeader : UIBehaviour
	{
		// PRIVATE MEMBERS

		[Header("Labels (optional - auto-created if null)")]
		[SerializeField]
		private TextMeshProUGUI _objectiveLabel;
		[SerializeField]
		private TextMeshProUGUI _team1ScoreText;
		[SerializeField]
		private TextMeshProUGUI _team2ScoreText;

		[Header("Progress Bars")]
		[SerializeField]
		private Image _team1Fill;
		[SerializeField]
		private Image _team2Fill;

		[Header("Colors")]
		[SerializeField]
		private Color _team1Color = new Color(0.2f, 0.5f, 1f);
		[SerializeField]
		private Color _team2Color = new Color(1f, 0.3f, 0.2f);

		[Header("Win/Lose Panel")]
		[SerializeField]
		private GameObject _winPanel;
		[SerializeField]
		private TextMeshProUGUI _winText;
		[SerializeField]
		private Button _exitRoomButton;

		private SceneContext _context;
		private int _lastTeam1;
		private int _lastTeam2;
		private int _lastToWin;
		private bool _lastGameOver;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_context = GameUI.Context;
			_lastTeam1 = -1;
			_lastTeam2 = -1;
			_lastToWin = -1;
			_lastGameOver = false;

			UIUtility.AddBackgroundPanel(RectTransform, new Color(0.3f, 0.3f, 0.4f, 0.6f), 1f);
			UIUtility.AddBackgroundPanel(RectTransform, new Color(0.08f, 0.08f, 0.12f, 0.7f));

			EnsureUI();
			if (_winPanel != null)
				_winPanel.SetActive(false);
			if (_exitRoomButton != null)
				_exitRoomButton.onClick.AddListener(OnExitRoomClicked);
		}

		private void EnsureUI()
		{
			EnsureScoreElements();
			EnsureGameOverPanel();
		}

		private void EnsureScoreElements()
		{
			if (_objectiveLabel != null && _team1ScoreText != null && _team2ScoreText != null)
				return;

			var rect = RectTransform;
			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -10f);
			rect.sizeDelta = new Vector2(520f, 100f);

			var defaultFont = TMPro.TMP_Settings.defaultFontAsset;
			var whiteSprite = CreateWhiteSprite();

			// Objective: "First to 100"
			if (_objectiveLabel == null)
			{
				var objGo = new GameObject("Objective");
				objGo.transform.SetParent(transform, false);
				var objRect = objGo.AddComponent<RectTransform>();
				objRect.anchorMin = new Vector2(0.5f, 1f);
				objRect.anchorMax = new Vector2(0.5f, 1f);
				objRect.pivot = new Vector2(0.5f, 1f);
				objRect.anchoredPosition = new Vector2(0f, 0f);
				objRect.sizeDelta = new Vector2(300f, 28f);
				_objectiveLabel = objGo.AddComponent<TextMeshProUGUI>();
				if (defaultFont != null) _objectiveLabel.font = defaultFont;
				_objectiveLabel.fontSize = 22;
				_objectiveLabel.alignment = TextAlignmentOptions.Center;
				_objectiveLabel.color = new Color(1f, 1f, 1f, 0.95f);
				_objectiveLabel.raycastTarget = false;
			}

			float rowY = -40f;
			float barW = 180f;
			float barH = 24f;
			float gap = 12f;
			float scoreW = 56f;

			// Team1: [45] [bar] - score left of bar
			if (_team1ScoreText == null)
			{
				var t1Go = new GameObject("Team1Score");
				t1Go.transform.SetParent(transform, false);
				var t1Rect = t1Go.AddComponent<RectTransform>();
				t1Rect.anchorMin = new Vector2(0.5f, 1f);
				t1Rect.anchorMax = new Vector2(0.5f, 1f);
				t1Rect.pivot = new Vector2(1f, 1f);
				t1Rect.anchoredPosition = new Vector2(-gap - barW * 0.5f - scoreW * 0.5f, rowY);
				t1Rect.sizeDelta = new Vector2(scoreW, 28f);
				_team1ScoreText = t1Go.AddComponent<TextMeshProUGUI>();
				if (defaultFont != null) _team1ScoreText.font = defaultFont;
				_team1ScoreText.fontSize = 24;
				_team1ScoreText.alignment = TextAlignmentOptions.Right;
				_team1ScoreText.color = _team1Color;
				_team1ScoreText.raycastTarget = false;
			}

			if (_team1Fill == null)
			{
				var barGo = new GameObject("Team1Bar");
				barGo.transform.SetParent(transform, false);
				var barRect = barGo.AddComponent<RectTransform>();
				barRect.anchorMin = new Vector2(0.5f, 1f);
				barRect.anchorMax = new Vector2(0.5f, 1f);
				barRect.pivot = new Vector2(0.5f, 1f);
				barRect.anchoredPosition = new Vector2(-gap - barW * 0.5f, rowY);
				barRect.sizeDelta = new Vector2(barW, barH);

				var bg = barGo.AddComponent<Image>();
				bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
				if (whiteSprite != null) bg.sprite = whiteSprite;

				var fillGo = new GameObject("Fill");
				fillGo.transform.SetParent(barGo.transform, false);
				var fillRect = fillGo.AddComponent<RectTransform>();
				fillRect.anchorMin = Vector2.zero;
				fillRect.anchorMax = Vector2.one;
				fillRect.offsetMin = Vector2.zero;
				fillRect.offsetMax = Vector2.zero;
				_team1Fill = fillGo.AddComponent<Image>();
				_team1Fill.color = _team1Color;
				if (whiteSprite != null) _team1Fill.sprite = whiteSprite;
				_team1Fill.type = Image.Type.Filled;
				_team1Fill.fillMethod = Image.FillMethod.Horizontal;
				_team1Fill.fillOrigin = (int)Image.OriginHorizontal.Left;
			}

			// Team2: [bar] [45] - bar left of score
			if (_team2ScoreText == null)
			{
				var t2Go = new GameObject("Team2Score");
				t2Go.transform.SetParent(transform, false);
				var t2Rect = t2Go.AddComponent<RectTransform>();
				t2Rect.anchorMin = new Vector2(0.5f, 1f);
				t2Rect.anchorMax = new Vector2(0.5f, 1f);
				t2Rect.pivot = new Vector2(0f, 1f);
				t2Rect.anchoredPosition = new Vector2(gap + barW * 0.5f + scoreW * 0.5f, rowY);
				t2Rect.sizeDelta = new Vector2(scoreW, 28f);
				_team2ScoreText = t2Go.AddComponent<TextMeshProUGUI>();
				if (defaultFont != null) _team2ScoreText.font = defaultFont;
				_team2ScoreText.fontSize = 24;
				_team2ScoreText.alignment = TextAlignmentOptions.Left;
				_team2ScoreText.color = _team2Color;
				_team2ScoreText.raycastTarget = false;
			}

			if (_team2Fill == null)
			{
				var barGo = new GameObject("Team2Bar");
				barGo.transform.SetParent(transform, false);
				var barRect = barGo.AddComponent<RectTransform>();
				barRect.anchorMin = new Vector2(0.5f, 1f);
				barRect.anchorMax = new Vector2(0.5f, 1f);
				barRect.pivot = new Vector2(0.5f, 1f);
				barRect.anchoredPosition = new Vector2(gap + barW * 0.5f, rowY);
				barRect.sizeDelta = new Vector2(barW, barH);

				var bg = barGo.AddComponent<Image>();
				bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
				if (whiteSprite != null) bg.sprite = whiteSprite;

				var fillGo = new GameObject("Fill");
				fillGo.transform.SetParent(barGo.transform, false);
				var fillRect = fillGo.AddComponent<RectTransform>();
				fillRect.anchorMin = Vector2.zero;
				fillRect.anchorMax = Vector2.one;
				fillRect.offsetMin = Vector2.zero;
				fillRect.offsetMax = Vector2.zero;
				_team2Fill = fillGo.AddComponent<Image>();
				_team2Fill.color = _team2Color;
				if (whiteSprite != null) _team2Fill.sprite = whiteSprite;
				_team2Fill.type = Image.Type.Filled;
				_team2Fill.fillMethod = Image.FillMethod.Horizontal;
				_team2Fill.fillOrigin = (int)Image.OriginHorizontal.Right;
			}
		}

		private static Sprite CreateWhiteSprite()
		{
			var tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, Color.white);
			tex.Apply();
			return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
		}

		private void EnsureGameOverPanel()
		{
			// Win/Lose panel (full-screen overlay with result + Exit Room button)
			if (_winPanel == null)
			{
				var font = TMPro.TMP_Settings.defaultFontAsset;
				_winPanel = new GameObject("GameOverPanel");
				// Parent to Canvas (full-screen) so panel covers entire screen, not just score header
				var canvas = GetComponentInParent<Canvas>();
				var fullScreenParent = canvas != null ? canvas.transform : (transform.parent != null ? transform.parent : transform);
				_winPanel.transform.SetParent(fullScreenParent, false);
				_winPanel.transform.SetAsLastSibling();
				var panelRect = _winPanel.AddComponent<RectTransform>();
				panelRect.anchorMin = Vector2.zero;
				panelRect.anchorMax = Vector2.one;
				panelRect.offsetMin = Vector2.zero;
				panelRect.offsetMax = Vector2.zero;

				var panelBg = _winPanel.AddComponent<Image>();
				panelBg.color = new Color(0f, 0f, 0f, 0.75f);
				panelBg.raycastTarget = true;

				// Result text - centered vertically and horizontally
				var textGo = new GameObject("ResultText");
				textGo.transform.SetParent(_winPanel.transform, false);
				var textRect = textGo.AddComponent<RectTransform>();
				textRect.anchorMin = new Vector2(0.5f, 0.5f);
				textRect.anchorMax = new Vector2(0.5f, 0.5f);
				textRect.pivot = new Vector2(0.5f, 0.5f);
				textRect.anchoredPosition = Vector2.zero;
				textRect.sizeDelta = new Vector2(500f, 80f);
				_winText = textGo.AddComponent<TextMeshProUGUI>();
				if (font != null) _winText.font = font;
				_winText.fontSize = 42;
				_winText.alignment = TextAlignmentOptions.Center;
				_winText.fontStyle = FontStyles.Bold;
				_winText.raycastTarget = false;

				// Exit Room button - 100px below center
				var btnGo = new GameObject("ExitRoomButton");
				btnGo.transform.SetParent(_winPanel.transform, false);
				var btnRect = btnGo.AddComponent<RectTransform>();
				btnRect.anchorMin = new Vector2(0.5f, 0.5f);
				btnRect.anchorMax = new Vector2(0.5f, 0.5f);
				btnRect.pivot = new Vector2(0.5f, 0.5f);
				btnRect.anchoredPosition = new Vector2(0f, -100f);
				btnRect.sizeDelta = new Vector2(200f, 44f);
				_exitRoomButton = btnGo.AddComponent<Button>();
				var btnImage = btnGo.AddComponent<Image>();
				btnImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);

				var btnTextGo = new GameObject("Text");
				btnTextGo.transform.SetParent(btnGo.transform, false);
				var btnTextRect = btnTextGo.AddComponent<RectTransform>();
				btnTextRect.anchorMin = Vector2.zero;
				btnTextRect.anchorMax = Vector2.one;
				btnTextRect.offsetMin = Vector2.zero;
				btnTextRect.offsetMax = Vector2.zero;
				var btnText = btnTextGo.AddComponent<TextMeshProUGUI>();
				if (font != null) btnText.font = font;
				btnText.text = "Exit Room";
				btnText.fontSize = 22;
				btnText.alignment = TextAlignmentOptions.Center;
				btnText.color = Color.white;
			}
		}

		private void AddFinalScoreText(Gameplay gameplay)
		{
			// Avoid duplicates
			var existing = _winPanel.transform.Find("FinalScore");
			if (existing != null)
				return;

			var font = TMPro.TMP_Settings.defaultFontAsset;

			var scoreGo = new GameObject("FinalScore");
			scoreGo.transform.SetParent(_winPanel.transform, false);
			var scoreRect = scoreGo.AddComponent<RectTransform>();
			scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
			scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
			scoreRect.pivot = new Vector2(0.5f, 0.5f);
			scoreRect.anchoredPosition = new Vector2(0f, -50f);
			scoreRect.sizeDelta = new Vector2(400f, 40f);

			var scoreText = scoreGo.AddComponent<TextMeshProUGUI>();
			if (font != null) scoreText.font = font;
			scoreText.text = $"{gameplay.Team1Score} - {gameplay.Team2Score}";
			scoreText.fontSize = 32;
			scoreText.alignment = TextAlignmentOptions.Center;
			scoreText.color = new Color(0.8f, 0.8f, 0.9f, 0.9f);
			scoreText.raycastTarget = false;
		}

		private void OnExitRoomClicked()
		{
			var runner = _context?.Runner ?? FindObjectOfType<NetworkRunner>();
			if (runner != null && runner.IsRunning)
			{
				runner.Shutdown();
				return;
			}
			var bootstrap = FindObjectOfType<FusionBootstrap>();
			if (bootstrap != null)
			{
				bootstrap.ShutdownAll();
			}
		}

		protected void Update()
		{
			if (_context?.Runner == null || _context.Runner.IsRunning == false)
				return;

			var gameplay = ResolveGameplay();
			if (gameplay == null)
				return;

			int team1 = gameplay.Team1Score;
			int team2 = gameplay.Team2Score;
			int toWin = gameplay.PointsToWin;
			bool gameOver = gameplay.WinningTeam != ETeam.None;

			// Objective label: "First to 100"
			if (_objectiveLabel != null && toWin != _lastToWin)
			{
				_objectiveLabel.text = $"First to {toWin}";
				_lastToWin = toWin;
			}

			// Team scores
			if (_team1ScoreText != null)
			{
				_team1ScoreText.text = team1.ToString();
				_lastTeam1 = team1;
			}

			if (_team2ScoreText != null)
			{
				_team2ScoreText.text = team2.ToString();
				_lastTeam2 = team2;
			}

			// Progress bars (0–1 fill) - always update so we catch replicated values
			if (_team1Fill != null)
			{
				float fill = toWin > 0 ? Mathf.Clamp01((float)team1 / toWin) : 0f;
				_team1Fill.fillAmount = fill;
				if (_team1Fill.color != _team1Color)
					_team1Fill.color = _team1Color;
			}

			if (_team2Fill != null)
			{
				float fill = toWin > 0 ? Mathf.Clamp01((float)team2 / toWin) : 0f;
				_team2Fill.fillAmount = fill;
				if (_team2Fill.color != _team2Color)
					_team2Fill.color = _team2Color;
			}

			// Win state
			if (gameOver != _lastGameOver && _winPanel != null)
			{
				_lastGameOver = gameOver;
				_winPanel.SetActive(gameOver);

				if (gameOver && _winText != null)
				{
					ETeam localTeam = GetLocalPlayerTeam();
					bool localPlayerWon = localTeam == gameplay.WinningTeam;

					_winText.text = localPlayerWon ? "YOU WIN!" : "YOU LOSE";
					_winText.color = localPlayerWon
						? (gameplay.WinningTeam == ETeam.Team1 ? _team1Color : _team2Color)
						: new Color(0.9f, 0.3f, 0.3f, 1f);

					// Animate entrance
					_winText.transform.localScale = Vector3.one * 0.5f;
					_winText.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);

					var panelCg = _winPanel.GetComponent<CanvasGroup>();
					if (panelCg == null)
						panelCg = _winPanel.AddComponent<CanvasGroup>();
					panelCg.alpha = 0f;
					panelCg.DOFade(1f, 0.5f);

					// Add final score text
					AddFinalScoreText(gameplay);
				}
			}
		}

		private Gameplay ResolveGameplay()
		{
			if (_context == null)
				return null;

			var g = _context.Gameplay;
			if (g != null && g.Object != null)
				return g;

			if (_context.Runner != null)
			{
				var list = new System.Collections.Generic.List<Gameplay>(4);
				_context.Runner.GetAllBehaviours(list);
				foreach (var gp in list)
				{
					if (gp != null && gp.Object != null)
						return gp;
				}
			}
			return null;
		}

		private ETeam GetLocalPlayerTeam()
		{
			if (_context?.Runner == null || _context.Runner.IsRunning == false)
				return ETeam.None;

			var playerObj = _context.Runner.GetPlayerObject(_context.Runner.LocalPlayer);
			if (playerObj == null)
				return ETeam.None;

			var player = playerObj.GetComponent<Player>();
			return player != null ? player.Team : ETeam.None;
		}
	}
}
