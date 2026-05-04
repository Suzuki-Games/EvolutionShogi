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

    // 進化選択モーダル：初回表示時に遅延生成し以降は使い回す
    private GameObject evolutionChoicePanel;
    private Transform evolutionChoiceCardContainer;
    private HeroPiece evolutionChoiceHero;

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

        // 日本語フォールバックが Inspector 未設定なら Resources から自動ロードを試みる。
        // 進化選択モーダルの動的UIで日本語が文字化けする問題（gameFontがLatin専用Bangers等の場合）に対する保険。
        // アセットは Assets/Resources/Fonts/ 配下に置いてあるので Resources.Load で参照できる。
        if (japaneseFallbackFont == null)
        {
            japaneseFallbackFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansJP-VariableFont_wght SDF");
            if (japaneseFallbackFont != null)
                Debug.Log($"[UIManager] 日本語フォントを自動ロード: {japaneseFallbackFont.name}");
            else
                Debug.LogWarning("[UIManager] 日本語フォールバックフォントを発見できませんでした。" +
                                 "Inspector で japaneseFallbackFont を設定するか、Resources/Fonts/ に NotoSansJP SDF を置いてください。");
        }

        // Latin専用フォント（Bangers等）でも日本語が文字化けしないよう、Noto等の日本語SDFを fallback に登録する。
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
            case PieceType.Gold: return "Gold";
            case PieceType.Rook: return "Rook";
            case PieceType.Bishop: return "Bishop";
            case PieceType.Hero: return "Hero";
            default: return type.ToString();
        }
    }

    /// <summary>
    /// 現在の形態から見た「次に選べる進化先」のラベルを返す。
    /// 分岐（Pawn → Silver/Gold など）はスラッシュ区切りで併記する。
    /// 進化ツリーに新しい分岐を足したい場合はここに追記する。
    /// </summary>
    private string GetNextFormName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn:   return "Silver / Gold";
            case PieceType.Silver: return "Rook / Bishop";
            case PieceType.Gold:   return "Rook / Bishop";
            case PieceType.Rook:   return "Hero";
            case PieceType.Bishop: return "Hero";
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

    // ──────────────────────────────────────────────────────────────────
    //  進化選択モーダル
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 進化先の表示用データ。駒の種類ごとに「名前」と「動きの説明文」を持つ。
    /// 新しい進化先を追加したい場合は <see cref="GetChoiceDescriptor"/> に1ケース追加するだけで済む。
    /// </summary>
    private readonly struct EvolutionChoiceDescriptor
    {
        public readonly PieceType Type;
        public readonly string DisplayName;
        public readonly string MoveDescription;
        public readonly string FlavorText;

        public EvolutionChoiceDescriptor(PieceType type, string displayName, string moveDescription, string flavorText)
        {
            Type = type;
            DisplayName = displayName;
            MoveDescription = moveDescription;
            FlavorText = flavorText;
        }
    }

    /// <summary>
    /// 進化先1つに対応する表示データを返す。プレゼンテーション層の文言はここに集約する。
    /// </summary>
    private EvolutionChoiceDescriptor GetChoiceDescriptor(PieceType type)
    {
        switch (type)
        {
            // フレーバー文の "\n" は意図的な改行点。日本語はスペース境界が無く
            // TMPが熟語を分断するため(「盤面」が「盤/面」で割れる等)、文の意味で割る。
            case PieceType.Silver:
                return new EvolutionChoiceDescriptor(
                    type, "銀 (Silver)",
                    "前3マス + 斜め後ろ2マス",
                    "前進と斜め後退で攻防を両立。\n退路を残しつつ攻める。");
            case PieceType.Gold:
                return new EvolutionChoiceDescriptor(
                    type, "金 (Gold)",
                    "前3マス + 横2マス + 真後ろ",
                    "粘り強い守備寄り。\n横移動と真後ろで盤面を支配する。");
            case PieceType.Rook:
                return new EvolutionChoiceDescriptor(
                    type, "飛車 (Rook)",
                    "縦・横にスライド",
                    "直線の支配者。\n縦横どこまでも貫く長距離の脅威。");
            case PieceType.Bishop:
                return new EvolutionChoiceDescriptor(
                    type, "角 (Bishop)",
                    "斜めにスライド",
                    "対角線を切る奇襲型。\n縦横に詰まった盤面で活きる。");
            case PieceType.Hero:
                return new EvolutionChoiceDescriptor(
                    type, "勇者 (Hero)",
                    "全方位スライド",
                    "最終形態。\nあらゆる方向にどこまでも進める。");
            default:
                return new EvolutionChoiceDescriptor(type, type.ToString(), "", "");
        }
    }

    /// <summary>
    /// 進化先2択モーダルを表示する。HeroPiece.OnEvolutionChoiceRequired から呼ばれる想定。
    /// プレイヤーがカードをクリックすると <see cref="HeroPiece.ApplyEvolutionChoice"/> を呼んでモーダルを閉じる。
    /// </summary>
    public void ShowEvolutionChoiceModal(HeroPiece hero, List<PieceType> choices)
    {
        if (hero == null || choices == null || choices.Count == 0) return;

        evolutionChoiceHero = hero;
        EnsureEvolutionChoicePanel();
        PopulateEvolutionChoiceCards(choices);
        evolutionChoicePanel.SetActive(true);
        evolutionChoicePanel.transform.SetAsLastSibling(); // 他のUIより前面へ
    }

    /// <summary>
    /// モーダルパネルを必要なら遅延生成する（初回のみ階層構築のコストを払う）。
    /// 階層構造：
    ///   EvolutionChoicePanel（半透明黒の全画面 Image。raycastで盤面クリックも遮断）
    ///     ├─ Title（"EVOLUTION!"）
    ///     ├─ Subtitle（"進化先を選択してください"）
    ///     └─ CardContainer（横並び。子カードが <see cref="PopulateEvolutionChoiceCards"/> で生成される）
    /// </summary>
    private void EnsureEvolutionChoicePanel()
    {
        if (evolutionChoicePanel != null) return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[UIManager] Canvas が見つからず進化選択モーダルを生成できません。");
            return;
        }

        // ── 全画面ディマー ──
        evolutionChoicePanel = new GameObject("EvolutionChoicePanel");
        evolutionChoicePanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRT = evolutionChoicePanel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image dim = evolutionChoicePanel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.7f);
        dim.raycastTarget = true; // 盤面側クリックを遮断するために必須

        // ── タイトル "EVOLUTION!" ──
        CreateModalText(
            name: "ModalTitle",
            parent: evolutionChoicePanel.transform,
            anchoredPos: new Vector2(0f, 220f),
            sizeDelta: new Vector2(800f, 100f),
            text: "EVOLUTION!",
            fontSize: 64f,
            color: new Color(1f, 0.85f, 0.3f, 1f),
            style: FontStyles.Bold);

        // ── サブタイトル ──
        CreateModalText(
            name: "ModalSubtitle",
            parent: evolutionChoicePanel.transform,
            anchoredPos: new Vector2(0f, 150f),
            sizeDelta: new Vector2(800f, 40f),
            text: "進化先を選択してください",
            fontSize: 22f,
            color: new Color(1f, 1f, 1f, 0.85f),
            style: FontStyles.Normal);

        // ── カードコンテナ（中央に横並び） ──
        // 子カードの座標は PopulateEvolutionChoiceCards で配置するため、
        // ここでは選択肢2枚が収まる十分な大きさのRectTransformだけ用意する。
        GameObject containerGO = new GameObject("CardContainer");
        containerGO.transform.SetParent(evolutionChoicePanel.transform, false);
        RectTransform containerRT = containerGO.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.sizeDelta = new Vector2(700f, 320f);
        containerRT.anchoredPosition = new Vector2(0f, -40f);

        evolutionChoiceCardContainer = containerGO.transform;
    }

    /// <summary>
    /// 既存カードを破棄して、現在の選択肢に合わせてカードを再生成する。
    /// カード幅と間隔から自動配置するため、2択でも3択でも対応できる。
    /// </summary>
    private void PopulateEvolutionChoiceCards(List<PieceType> choices)
    {
        if (evolutionChoiceCardContainer == null) return;

        // 既存カードを全削除（同じパネルを再利用するため）
        for (int i = evolutionChoiceCardContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(evolutionChoiceCardContainer.GetChild(i).gameObject);
        }

        const float cardWidth = 280f;
        const float cardHeight = 320f;
        const float spacing = 40f;

        // 中央揃え：N枚のカードを左から並べたとき、コンテナ中心からのオフセットを計算
        float totalWidth = choices.Count * cardWidth + (choices.Count - 1) * spacing;
        float startX = -totalWidth / 2f + cardWidth / 2f;

        for (int i = 0; i < choices.Count; i++)
        {
            EvolutionChoiceDescriptor descriptor = GetChoiceDescriptor(choices[i]);
            float x = startX + i * (cardWidth + spacing);
            CreateChoiceCard(
                parent: evolutionChoiceCardContainer,
                descriptor: descriptor,
                anchoredPos: new Vector2(x, 0f),
                size: new Vector2(cardWidth, cardHeight));
        }
    }

    /// <summary>
    /// 進化選択カードを1枚生成する。クリックすると <see cref="OnEvolutionChoiceClicked"/> を呼ぶ。
    /// 構造：背景パネル + 駒画像 + 駒名 + 動き説明 + フレーバー
    /// </summary>
    private void CreateChoiceCard(Transform parent, EvolutionChoiceDescriptor descriptor, Vector2 anchoredPos, Vector2 size)
    {
        // ── カード本体（ボタン） ──
        GameObject cardGO = new GameObject($"Card_{descriptor.Type}");
        cardGO.transform.SetParent(parent, false);

        RectTransform cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.pivot = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = size;
        cardRT.anchoredPosition = anchoredPos;

        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        cardBg.raycastTarget = true;

        Button cardButton = cardGO.AddComponent<Button>();
        // カード全体をクリック可能にする。ホバー色は ColorBlock で軽く明るく。
        var colors = cardButton.colors;
        colors.normalColor = new Color(0.18f, 0.18f, 0.24f, 1f);
        colors.highlightedColor = new Color(0.30f, 0.28f, 0.18f, 1f); // 金寄りに反応
        colors.pressedColor = new Color(0.5f, 0.42f, 0.18f, 1f);
        cardButton.colors = colors;
        cardButton.targetGraphic = cardBg;

        // 値キャプチャ：デリゲート内で descriptor.Type を直接参照すると
        // ループ変数に依存しなくなる（このメソッドのスコープなので問題ないが明示的に）
        PieceType chosenType = descriptor.Type;
        cardButton.onClick.AddListener(() => OnEvolutionChoiceClicked(chosenType));

        // ── 駒画像 ──
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(cardGO.transform, false);
        RectTransform iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.5f, 1f);
        iconRT.anchorMax = new Vector2(0.5f, 1f);
        iconRT.pivot = new Vector2(0.5f, 1f);
        iconRT.sizeDelta = new Vector2(110f, 110f);
        iconRT.anchoredPosition = new Vector2(0f, -20f);

        Image iconImage = iconGO.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconImage.preserveAspect = true;
        iconImage.color = new Color(1f, 0.85f, 0.3f); // 進化後の勇者カラー
        iconImage.sprite = LoadPieceSprite(descriptor.Type);

        // ── 駒名 ──
        CreateModalText(
            name: "Name",
            parent: cardGO.transform,
            anchoredPos: new Vector2(0f, -150f),
            sizeDelta: new Vector2(size.x - 20f, 36f),
            text: descriptor.DisplayName,
            fontSize: 24f,
            color: new Color(1f, 0.92f, 0.55f),
            style: FontStyles.Bold,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f));

        // ── 動きの説明 ──
        CreateModalText(
            name: "MoveDesc",
            parent: cardGO.transform,
            anchoredPos: new Vector2(0f, -190f),
            sizeDelta: new Vector2(size.x - 20f, 32f),
            text: descriptor.MoveDescription,
            fontSize: 16f,
            color: new Color(0.95f, 0.95f, 1f),
            style: FontStyles.Normal,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f));

        // ── フレーバー説明 ──
        CreateModalText(
            name: "Flavor",
            parent: cardGO.transform,
            anchoredPos: new Vector2(0f, -230f),
            sizeDelta: new Vector2(size.x - 20f, 70f),
            text: descriptor.FlavorText,
            fontSize: 14f,
            color: new Color(0.85f, 0.85f, 0.9f),
            style: FontStyles.Italic,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f));
    }

    /// <summary>
    /// プレイヤーが進化先カードをクリックした時のコールバック。
    /// 選択を Hero に伝えてモーダルを閉じる。多段進化が連鎖した場合は
    /// HeroPiece 側が次の OnEvolutionChoiceRequired を発火し、再度モーダルが開く。
    /// </summary>
    private void OnEvolutionChoiceClicked(PieceType chosen)
    {
        HeroPiece hero = evolutionChoiceHero;
        HideEvolutionChoiceModal();

        if (hero != null)
        {
            hero.ApplyEvolutionChoice(chosen);
        }
    }

    private void HideEvolutionChoiceModal()
    {
        if (evolutionChoicePanel != null)
        {
            evolutionChoicePanel.SetActive(false);
        }
    }

    /// <summary>
    /// モーダル内テキストを生成する共通ヘルパ。anchor を引数化してカード内/全画面どちらでも使えるようにしている。
    /// </summary>
    private TextMeshProUGUI CreateModalText(
        string name, Transform parent,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, Color color, FontStyles style,
        Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        rt.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        // フォントを先に確定させてから text を入れる。順序が逆だとTMPの初期レンダが
        // デフォルトフォント(LiberationSans=Latin専用)で走り、その後フォントを差し替えても
        // 文字化けキャッシュが残ることがあるため。
        TMP_FontAsset fontToUse = PickFontForText(text);
        if (fontToUse != null) tmp.font = fontToUse;

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        // 日本語はスペースが無いためTMPの自動wrapが効かないことがある。
        // RectTransform の幅で折り返したいので Normal を明示する。
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.raycastTarget = false; // ボタンクリックを通すために透過

        // 念のためメッシュ再生成。フォント差し替え後に古いグリフが残るケースの保険。
        tmp.ForceMeshUpdate();

        return tmp;
    }

    /// <summary>
    /// テキスト内容に応じた適切なフォントを返す。
    /// 非ASCII（U+0080以上、つまり日本語・中国語等）が含まれていれば japaneseFallbackFont を優先。
    /// gameFont の fallbackFontAssetTable に登録した日本語フォントは Dynamic SDF 等で
    /// 環境差により動かないことがあるため、ここで「直接」日本語フォントを指定する保険を入れている。
    /// </summary>
    private TMP_FontAsset PickFontForText(string text)
    {
        if (string.IsNullOrEmpty(text)) return gameFont;

        bool hasNonAscii = false;
        foreach (char c in text)
        {
            if (c > 0x7F)
            {
                hasNonAscii = true;
                break;
            }
        }

        if (hasNonAscii && japaneseFallbackFont != null)
        {
            return japaneseFallbackFont;
        }
        return gameFont;
    }

    /// <summary>
    /// 駒種から Resources/PieceImages 配下のスプライトを読み込む。
    /// LoadAll フォールバックは Multiple モードのスプライトに対応するため。
    /// </summary>
    private Sprite LoadPieceSprite(PieceType type)
    {
        string spritePath = "PieceImages/" + GetSpriteNameForType(type);
        Sprite sprite = Resources.Load<Sprite>(spritePath);
        if (sprite == null)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(spritePath);
            if (sprites != null && sprites.Length > 0) sprite = sprites[0];
        }
        return sprite;
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
