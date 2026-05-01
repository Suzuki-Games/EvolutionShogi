using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゲーム内のUI状態（タイトル、ゲーム中、リザルト）を管理します。
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject resultPanel;

    [Header("Result UI Objects")]
    [SerializeField] private GameObject winTextObject;
    [SerializeField] private GameObject loseTextObject;

    [Header("Game HUD")]
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI formText;
    [SerializeField] private TextMeshProUGUI turnText;

    [Header("Hand (持ち駒) UI")]
    [SerializeField] private Transform handButtonContainer;
    [SerializeField] private Button handButtonPrefab;
    [SerializeField] private PlayerInputController playerInput;

    [Header("Evolution Effect")]
    [SerializeField] private TextMeshProUGUI evolutionAnnouncementText;

    [Header("Game Font (Optional)")]
    [Tooltip("ゲーム全体に適用するTMP Font Asset。" +
             "Assets/Fonts/YUGOTHB SDF または NotoSansJP-VariableFont_wght SDF をドラッグ。" +
             "未設定の場合はデフォルトフォントのまま。")]
    [SerializeField] private TMP_FontAsset gameFont;

    [Tooltip("代わりにTTFをドラッグするとDynamic SDFを実行時に自動生成。" +
             "Bangers.ttf や Anton.ttf 等の派手なゲームフォントをそのまま使える。" +
             "gameFont が未設定の場合のみ参照される。")]
    [SerializeField] private Font gameFontTTF;

    [Tooltip("日本語フォールバック。Bangers/Anton等のLatin専用フォント使用時に必要。" +
             "NotoSansJP-VariableFont_wght SDF をドラッグすると日本語だけそちらから借りる。")]
    [SerializeField] private TMP_FontAsset japaneseFallbackFont;

    private HeroPiece trackedHero;
    private int turnCount = 0;
    private List<Button> handButtons = new List<Button>();

    // 実行時に生成するタイトル画面の装飾要素
    private TextMeshProUGUI runtimeTitleText;
    private TextMeshProUGUI runtimeTaglineText;
    private Coroutine titleAnimationCoroutine;

    private void Start()
    {
        ShowTitleScreen();

        // HandContainerのLayoutGroupをGridLayoutGroupに切り替える（1回だけ）
        SetupHandContainerLayout();

        // TTFが指定されていればDynamic SDFを実行時生成。これによりFont Asset Creatorを使わずに
        // Bangers/Anton等の派手なゲームフォントをドラッグ1回で使えるようにしている。
        if (gameFont == null && gameFontTTF != null)
        {
            gameFont = TMP_FontAsset.CreateFontAsset(gameFontTTF);
            if (gameFont != null)
                Debug.Log($"[UIManager] TTFから動的SDFを生成しました: {gameFontTTF.name}");
            else
                Debug.LogWarning($"[UIManager] TTFからのSDF生成に失敗: {gameFontTTF.name}");
        }

        // 日本語フォールバック設定。Latin専用フォント（Bangers等）でもタイトル等の日本語が
        // 文字化けしないよう、Noto等の日本語SDFを fallback に登録する。
        if (gameFont != null && japaneseFallbackFont != null)
        {
            if (gameFont.fallbackFontAssetTable == null)
                gameFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!gameFont.fallbackFontAssetTable.Contains(japaneseFallbackFont))
            {
                gameFont.fallbackFontAssetTable.Add(japaneseFallbackFont);
                Debug.Log($"[UIManager] 日本語フォールバック追加: {japaneseFallbackFont.name}");
            }
        }

        // ゲーム全体のテキストにゲーム用フォントを一括適用
        ApplyGameFontToAllText();

        if (HandManager.Instance != null)
        {
            HandManager.Instance.OnHandChanged += UpdateHandUI;
        }
    }

    /// <summary>
    /// シーン内の全TextMeshProUGUIに gameFont を適用する。
    /// プレハブのアセット自体は除外し、シーンに配置されたインスタンスのみ対象とする。
    /// 動的に生成される持ち駒ボタンや進化フラッシュ等は親Canvas配下なのでこの関数で網羅できる。
    /// </summary>
    private void ApplyGameFontToAllText()
    {
        if (gameFont == null)
        {
            Debug.LogWarning("[UIManager] Game Font が未設定です。Inspectorで TMP_FontAsset をアサインしてください。");
            return;
        }

        TextMeshProUGUI[] all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        int applied = 0;
        foreach (var tmp in all)
        {
            // シーンに存在するオブジェクトのみ（プレハブアセット本体は除外）
            if (tmp != null && tmp.gameObject.scene.IsValid())
            {
                tmp.font = gameFont;
                tmp.SetMaterialDirty(); // マテリアルキャッシュを更新
                tmp.ForceMeshUpdate();  // テキストメッシュを再生成
                applied++;
            }
        }
        Debug.Log($"[UIManager] フォント適用完了: '{gameFont.name}' を {applied} 件のテキストに適用しました。");
    }

    /// <summary>
    /// HandContainerのLayoutGroupをGridLayoutGroupに初期設定する
    /// </summary>
    private void SetupHandContainerLayout()
    {
        if (handButtonContainer == null) return;

        // 既存のLayoutGroup（HorizontalLayoutGroup等）があれば即時削除
        foreach (var lg in handButtonContainer.GetComponents<LayoutGroup>())
        {
            DestroyImmediate(lg);
        }

        GridLayoutGroup grid = handButtonContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(55, 55);
        grid.spacing = new Vector2(4, 4);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1; // 縦1列に並べる
    }

    private void OnDestroy()
    {
        if (HandManager.Instance != null)
        {
            HandManager.Instance.OnHandChanged -= UpdateHandUI;
        }
    }

    public void ShowTitleScreen()
    {
        titlePanel.SetActive(true);
        gamePanel.SetActive(false);
        resultPanel.SetActive(false);
        SetHUDVisible(false);

        if (evolutionAnnouncementText != null) evolutionAnnouncementText.gameObject.SetActive(false);

        // タイトル装飾要素（大型タイトル、サブタイトル）を必要に応じて生成
        EnsureTitleElements();

        // タイトル画面アニメーション（スライドイン → ループ脈動）を開始
        if (titleAnimationCoroutine != null) StopCoroutine(titleAnimationCoroutine);
        titleAnimationCoroutine = StartCoroutine(TitleAnimationLoop());
    }

    /// <summary>
    /// タイトル画面の装飾要素（メインタイトル＋サブコピー）を実行時に生成する。
    /// 一旦すべての装飾（グラデ・アウトライン・AutoSize）を外した最シンプル構成。
    /// 重なって見えていた問題は装飾の組合せが原因と判断したため、ベースラインに戻す。
    /// </summary>
    private void EnsureTitleElements()
    {
        if (titlePanel == null) return;

        Canvas parentCanvas = titlePanel.GetComponentInParent<Canvas>();
        Transform runtimeParent = parentCanvas != null ? parentCanvas.transform : titlePanel.transform;

        // ── タイトル ──
        if (runtimeTitleText == null)
        {
            runtimeTitleText = CreateRuntimeTMP(
                name: "RuntimeTitleText",
                parent: runtimeParent,
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(0f, -90f),
                sizeDelta: new Vector2(-120f, 200f),
                fontSize: 72f,
                style: FontStyles.Bold);
            runtimeTitleText.transform.SetAsLastSibling();
        }
        runtimeTitleText.gameObject.SetActive(true);

        // 2行構成：EVOLUTION / SHOGI（fontSize 72 + 字間4でEVOLUTIONが1行に収まる）
        runtimeTitleText.text = "EVOLUTION\nSHOGI";
        runtimeTitleText.alignment = TextAlignmentOptions.Center;
        runtimeTitleText.enableAutoSizing = false;
        runtimeTitleText.fontSize = 72f;
        runtimeTitleText.color = Color.white; // グラデーションのベース（白で乗算しないように）
        runtimeTitleText.characterSpacing = 4f;
        // 金（上）→橙（下）の縦グラデーション。頂点カラー操作なのでDynamic SDFでも問題なく動く。
        runtimeTitleText.enableVertexGradient = true;
        runtimeTitleText.colorGradient = new VertexGradient(
            new Color(1f, 0.92f, 0.55f, 1f),  // top-left  : 明るい金
            new Color(1f, 0.92f, 0.55f, 1f),  // top-right : 明るい金
            new Color(1f, 0.55f, 0.1f, 1f),   // bottom-left  : 橙
            new Color(1f, 0.55f, 0.1f, 1f));  // bottom-right : 橙
        // Dynamic SDF（TTFから実行時生成したフォント）はアウトライン用のマテリアル設定が
        // 不完全なため、outlineWidthを0以上にすると無関係なグリフ（%や@等）が描画される
        // 既知の不具合がある。アウトラインは使わない。
        runtimeTitleText.outlineWidth = 0f;
        if (gameFont != null) runtimeTitleText.font = gameFont;

        // ── サブコピー ──
        // タイトル下に配置（top-anchor絶対座標）。
        // タイトルrect底（-90 - 200 = -290）から30px余白で -320。
        if (runtimeTaglineText == null)
        {
            runtimeTaglineText = CreateRuntimeTMP(
                name: "RuntimeTaglineText",
                parent: runtimeParent,
                anchorMin: new Vector2(0f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(0.5f, 1f),
                anchoredPos: new Vector2(0f, -320f),
                sizeDelta: new Vector2(-120f, 50f),
                fontSize: 26f,
                style: FontStyles.Normal);
            runtimeTaglineText.transform.SetAsLastSibling();
        }
        runtimeTaglineText.gameObject.SetActive(true);

        runtimeTaglineText.text = "PAWN TO HERO";
        runtimeTaglineText.alignment = TextAlignmentOptions.Center;
        runtimeTaglineText.enableAutoSizing = false;
        runtimeTaglineText.fontSize = 26f;
        runtimeTaglineText.color = new Color(1f, 1f, 1f, 0.75f);
        runtimeTaglineText.characterSpacing = 4f;
        runtimeTaglineText.enableVertexGradient = false;
        // Dynamic SDFの制約によりアウトラインは使えない（タイトルと同様）
        runtimeTaglineText.outlineWidth = 0f;
        if (gameFont != null) runtimeTaglineText.font = gameFont;

        Debug.Log($"[UIManager] タイトル要素を更新: title='{runtimeTitleText.text}', tagline='{runtimeTaglineText.text}'");
    }

    /// <summary>
    /// 共通：タイトル画面用の TextMeshProUGUI を実行時生成するヘルパ。
    /// 横方向ストレッチアンカーをサポートして Canvas 幅追従を可能にしている。
    /// </summary>
    private TextMeshProUGUI CreateRuntimeTMP(
        string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.raycastTarget = false; // クリックがStartボタンに届くように

        if (gameFont != null) tmp.font = gameFont;

        return tmp;
    }

    /// <summary>
    /// タイトル画面のアニメーション。
    /// ① 開幕：タイトル文字を上から ease-out で落下スライドイン
    /// ② 持続ループ：タイトル脈動（±4%スケール）＋ Startボタン呼吸（±6%スケール）
    /// すべてTransform操作なのでDynamic SDFのレンダリングに影響しない。
    /// </summary>
    private IEnumerator TitleAnimationLoop()
    {
        Button startButton = titlePanel != null ? titlePanel.GetComponentInChildren<Button>(true) : null;
        Vector3 buttonOriginalScale = startButton != null ? startButton.transform.localScale : Vector3.one;
        Vector3 titleOriginalScale = runtimeTitleText != null ? runtimeTitleText.transform.localScale : Vector3.one;

        // ① 落下スライドイン：250px上から ease-out で降りてくる
        if (runtimeTitleText != null)
        {
            RectTransform rt = runtimeTitleText.rectTransform;
            Vector2 endPos = rt.anchoredPosition;
            Vector2 startPos = endPos + new Vector2(0f, 250f);
            rt.anchoredPosition = startPos;

            float duration = 0.55f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - p) * (1f - p); // ease-out quad
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                yield return null;
            }
            rt.anchoredPosition = endPos;
        }

        // ② 持続ループ：タイトル脈動 + Startボタン呼吸
        while (titlePanel != null && titlePanel.activeSelf)
        {
            float t = Time.unscaledTime;

            if (runtimeTitleText != null)
            {
                float titlePulse = Mathf.Sin(t * 2.0f) * 0.04f + 1f;
                runtimeTitleText.transform.localScale = titleOriginalScale * titlePulse;
            }

            if (startButton != null)
            {
                float buttonPulse = Mathf.Sin(t * 2.8f) * 0.06f + 1f;
                startButton.transform.localScale = buttonOriginalScale * buttonPulse;
            }

            yield return null;
        }

        // クリーンアップ：ゲーム画面にスケール変動を持ち込まない
        if (startButton != null) startButton.transform.localScale = buttonOriginalScale;
        if (runtimeTitleText != null) runtimeTitleText.transform.localScale = titleOriginalScale;
    }

    public void OnClickGameStart()
    {
        titlePanel.SetActive(false);
        gamePanel.SetActive(true);
        resultPanel.SetActive(false);
        turnCount = 0;
        SetHUDVisible(true);

        // タイトル装飾はCanvas直下にあるため自動では消えない。明示的に非表示化。
        if (runtimeTitleText != null) runtimeTitleText.gameObject.SetActive(false);
        if (runtimeTaglineText != null) runtimeTaglineText.gameObject.SetActive(false);

        GameManager.Instance.StartGame();
    }

    private void SetHUDVisible(bool visible)
    {
        if (expText != null) expText.gameObject.SetActive(visible);
        if (formText != null) formText.gameObject.SetActive(visible);
        if (turnText != null) turnText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// GameManagerから勇者の参照を受け取り、HUD追跡を開始する
    /// </summary>
    public void TrackHero(HeroPiece hero)
    {
        trackedHero = hero;
        UpdateHeroHUD();
    }

    /// <summary>
    /// ターン開始時に呼ばれる（TurnManagerから）
    /// </summary>
    public void OnNewPlayerTurn()
    {
        turnCount++;
        UpdateHeroHUD();
    }

    /// <summary>
    /// EXP/進化表示だけを更新する（ターンカウントは増やさない）
    /// </summary>
    public void RefreshHeroHUD()
    {
        UpdateHeroHUD();
    }

    private void UpdateHeroHUD()
    {
        if (trackedHero == null) return;

        if (expText != null)
        {
            int currentExp = trackedHero.CurrentExp;
            int nextThreshold = GetNextExpThreshold(currentExp);
            int prevThreshold = GetPrevExpThreshold(currentExp);

            if (nextThreshold > 0)
            {
                // 現在の進化段階での進捗をビジュアルバーで表示
                int progress = currentExp - prevThreshold;
                int total = nextThreshold - prevThreshold;
                string bar = BuildProgressBar(progress, total, 10);
                expText.text = $"EXP {currentExp} / {nextThreshold}\n<size=80%>{bar}</size>";
            }
            else
            {
                // 最終形態：満タンバーで表示
                string bar = BuildProgressBar(1, 1, 10);
                expText.text = $"EXP {currentExp}  <color=#FFD93D>MAX</color>\n<size=80%>{bar}</size>";
            }
        }

        if (formText != null)
        {
            string formName = GetFormDisplayName(trackedHero.Type);
            string nextForm = GetNextFormName(trackedHero.Type);
            if (nextForm != null)
                formText.text = $"{formName}  >>  <color=#FFD93D>{nextForm}</color>";
            else
                formText.text = $"<color=#FFD93D>{formName}</color> (Final)";
        }

        if (turnText != null)
        {
            turnText.text = $"Turn {turnCount}";
        }
    }

    /// <summary>
    /// テキストベースの進捗バーを生成。RichText対応で進捗部分は黄色、未進捗部分はグレー。
    /// </summary>
    private string BuildProgressBar(int progress, int total, int barLength)
    {
        if (total <= 0) total = 1;
        progress = Mathf.Clamp(progress, 0, total);
        int filled = Mathf.RoundToInt(progress * barLength / (float)total);
        filled = Mathf.Clamp(filled, 0, barLength);
        string filledPart = new string('█', filled);
        string emptyPart = new string('░', barLength - filled);
        return $"<color=#FFD93D>{filledPart}</color><color=#555555>{emptyPart}</color>";
    }

    private int GetNextExpThreshold(int currentExp)
    {
        if (currentExp < 2) return 2;
        if (currentExp < 5) return 5;
        if (currentExp < 10) return 10;
        return -1; // MAX
    }

    private int GetPrevExpThreshold(int currentExp)
    {
        if (currentExp >= 10) return 10;
        if (currentExp >= 5) return 5;
        if (currentExp >= 2) return 2;
        return 0;
    }

    private string GetFormDisplayName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return "Pawn";
            case PieceType.Silver: return "Silver";
            case PieceType.Rook: return "Rook";
            case PieceType.Hero: return "Hero";
            default: return type.ToString();
        }
    }

    private string GetNextFormName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return "Silver";
            case PieceType.Silver: return "Rook";
            case PieceType.Rook: return "Hero";
            default: return null;
        }
    }

    /// <summary>
    /// 持ち駒UIを更新する（HandManagerのイベントから呼ばれる）
    /// </summary>
    private void UpdateHandUI()
    {
        // 既存ボタンをクリア
        foreach (var btn in handButtons)
        {
            if (btn != null) Destroy(btn.gameObject);
        }
        handButtons.Clear();

        if (handButtonContainer == null || handButtonPrefab == null || HandManager.Instance == null) return;

        // 持ち駒を種類ごとにカウント
        Dictionary<PieceType, int> handCount = new Dictionary<PieceType, int>();
        foreach (var type in HandManager.Instance.PlayerHand)
        {
            if (handCount.ContainsKey(type))
                handCount[type]++;
            else
                handCount[type] = 1;
        }

        // ボタンを生成
        foreach (var kvp in handCount)
        {
            PieceType pieceType = kvp.Key;
            int count = kvp.Value;

            Button btn = Instantiate(handButtonPrefab, handButtonContainer);
            btn.gameObject.SetActive(true);

            // 駒画像をResourcesから読み込んでボタンに表示
            string spriteName = GetSpriteNameForType(pieceType);
            string spritePath = "PieceImages/" + spriteName;
            Sprite pieceSprite = Resources.Load<Sprite>(spritePath);
            if (pieceSprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(spritePath);
                if (sprites != null && sprites.Length > 0) pieceSprite = sprites[0];
            }
            Image btnImage = btn.GetComponent<Image>();
            if (btnImage != null && pieceSprite != null)
            {
                btnImage.sprite = pieceSprite;
                btnImage.color = new Color(0.4f, 0.6f, 1f);
                btnImage.preserveAspect = true;
            }

            // テキストで個数を表示
            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = $"x{count}";
                btnText.fontSize = 18;
                btnText.alignment = TextAlignmentOptions.BottomRight;
                // 動的生成テキストにもゲームフォントを反映
                if (gameFont != null) btnText.font = gameFont;
            }

            btn.onClick.AddListener(() =>
            {
                if (playerInput != null)
                {
                    playerInput.EnterDropMode(pieceType);
                }
            });

            handButtons.Add(btn);
        }
    }

    private string GetSpriteNameForType(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return "Pawn_歩";
            case PieceType.Silver: return "Silver_銀";
            case PieceType.Gold: return "Gold_金";
            case PieceType.Rook: return "Rook_飛";
            case PieceType.Bishop: return "Bishop_角";
            case PieceType.Hero: return "Hero_勇";
            case PieceType.King: return "King_王";
            default: return "";
        }
    }

    /// <summary>
    /// 進化演出を表示する（HeroPiece.OnEvolvedから呼ばれる）。
    /// 画面フラッシュ + バウンス付きアナウンステキスト + 駒スケールアニメ（駒側で実行）の三段演出。
    /// </summary>
    public void ShowEvolutionEffect(PieceType oldType, PieceType newType)
    {
        StartCoroutine(EvolutionEffectCoroutine(oldType, newType));
    }

    private IEnumerator EvolutionEffectCoroutine(PieceType oldType, PieceType newType)
    {
        string oldName = GetFormDisplayName(oldType);
        string newName = GetFormDisplayName(newType);

        // ① 画面フラッシュ：黄色いオーバーレイを瞬間的に被せて消す
        SpawnScreenFlash();

        // ② アナウンステキスト：バウンス付きでスケールインさせる
        if (evolutionAnnouncementText != null)
        {
            evolutionAnnouncementText.text =
                $"<size=130%>EVOLUTION!</size>\n<color=#FFD93D>{oldName}</color>  >>  <color=#FFD93D>{newName}</color>";
            evolutionAnnouncementText.gameObject.SetActive(true);
            yield return StartCoroutine(BounceInAnnouncement(evolutionAnnouncementText.transform));
        }

        // 表示時間（読める間）
        yield return new WaitForSeconds(1.2f);

        // ③ クリーンアップ
        if (evolutionAnnouncementText != null)
        {
            evolutionAnnouncementText.transform.localScale = Vector3.one;
            evolutionAnnouncementText.gameObject.SetActive(false);
        }

        UpdateHeroHUD();
    }

    /// <summary>
    /// シーン上のCanvasを探してフルスクリーンの黄色フラッシュを生成し、フェードアウトで自己破棄する。
    /// 既存のUI階層に依存しないので、Editorでの追加設定なしで利用できる。
    /// </summary>
    private void SpawnScreenFlash()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject flashGO = new GameObject("EvolutionFlash");
        flashGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = flashGO.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = flashGO.AddComponent<Image>();
        img.color = new Color(1f, 0.95f, 0.5f, 0.75f);
        img.raycastTarget = false;

        // 他のUIより前面に出す
        flashGO.transform.SetAsLastSibling();

        StartCoroutine(FadeOutAndDestroy(flashGO, img, 0.45f));
    }

    private IEnumerator FadeOutAndDestroy(GameObject target, Image img, float duration)
    {
        float elapsed = 0f;
        Color startColor = img.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            img.color = c;
            yield return null;
        }
        Destroy(target);
    }

    /// <summary>
    /// アナウンステキストを 0.4 → 1.2 → 1.0 のバウンス曲線でスケールインさせる。
    /// 文字の出現に「衝撃」の手応えを与える定番演出。
    /// </summary>
    private IEnumerator BounceInAnnouncement(Transform target)
    {
        float duration = 0.35f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float scale;
            if (p < 0.6f)
            {
                // 0.4 → 1.2 のオーバーシュート
                scale = Mathf.Lerp(0.4f, 1.2f, p / 0.6f);
            }
            else
            {
                // 1.2 → 1.0 の落ち着き
                scale = Mathf.Lerp(1.2f, 1.0f, (p - 0.6f) / 0.4f);
            }
            target.localScale = Vector3.one * scale;
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    public void ShowGameClear()
    {
        gamePanel.SetActive(false);
        resultPanel.SetActive(true);

        // Winの文字だけを表示する
        if (winTextObject != null) winTextObject.SetActive(true);
        if (loseTextObject != null) loseTextObject.SetActive(false);
    }

    public void ShowGameOver()
    {
        gamePanel.SetActive(false);
        resultPanel.SetActive(true);

        // Loseの文字だけを表示する
        if (winTextObject != null) winTextObject.SetActive(false);
        if (loseTextObject != null) loseTextObject.SetActive(true);
    }

    public void OnClickRetry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
