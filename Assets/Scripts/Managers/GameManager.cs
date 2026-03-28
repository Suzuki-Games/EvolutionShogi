using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // リトライ処理などのため

/// <summary>
/// ゲーム全体の進行（初期配置、勝敗判定、シーン遷移）を管理します。
/// LogicTesterで行っていた「駒の配置」や「依存性の注入」もここで本番用に実装します。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Systems")]
    [SerializeField] private BoardGrid boardGrid;
    [SerializeField] private BoardView boardView;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private UIManager uiManager;

    [Header("Player Piece Prefabs")]
    [SerializeField] private HeroPiece heroPrefab;
    [SerializeField] private KingPiece playerKingPrefab;
    [SerializeField] private AllyPiece allyGoldPrefab;
    [SerializeField] private AllyPiece allyPawnPrefab;

    [Header("Enemy Piece Prefabs")]
    [SerializeField] private KingPiece enemyKingPrefab;
    [SerializeField] private EnemyPiece enemyPawnPrefab;
    [SerializeField] private EnemyPiece enemyGoldPrefab;
    [SerializeField] private EnemyPiece enemyRookPrefab;
    [SerializeField] private EnemyPiece enemyBishopPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // マネージャー間の依存関係を設定
        if (enemyAI != null)
        {
            enemyAI.boardGrid = boardGrid;
            enemyAI.boardView = boardView;
        }

        // UI側から `StartGame()` が呼ばれるまで待機するため、ここで SetupInitialBoard は呼ばない
    }

    /// <summary>
    /// タイトル画面で「Start」が押されたときに UIManager から呼ばれます
    /// </summary>
    public void StartGame()
    {
        // 持ち駒をリセット
        if (HandManager.Instance != null) HandManager.Instance.ClearAll();

        // カメラを盤面中央に配置し、7×7盤面が収まるサイズに自動調整
        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0, 0, -10);
            float boardWorldSize = Mathf.Max(BoardGrid.Width, BoardGrid.Height) * 1.1f;
            Camera.main.orthographicSize = boardWorldSize / 2f + 0.5f;
        }

        SetupInitialBoard();
    }

    /// <summary>
    /// 非対称な初期配陣をセットアップします（7×7盤面）。
    /// 敵は強力な駒が並び、自陣は王・勇者(歩)・歩×2・金×2の弱いスタート。
    /// </summary>
    private void SetupInitialBoard()
    {
        Debug.Log("GameManager: 初期配置をセットアップします（7×7）。");

        // --- プレイヤー陣営の配置 ---
        // y=0: 後衛（王＋金×2で守り固め）
        SpawnPiece(allyGoldPrefab, PieceType.Gold, false, new Vector2Int(1, 0));     // 金（左）
        SpawnPiece(playerKingPrefab, PieceType.King, false, new Vector2Int(3, 0));   // 王将（中央）
        SpawnPiece(allyGoldPrefab, PieceType.Gold, false, new Vector2Int(5, 0));     // 金（右）

        // y=1: 前衛（勇者＋歩×2）
        SpawnPiece(allyPawnPrefab, PieceType.Pawn, false, new Vector2Int(1, 1));     // 歩（左）
        HeroPiece hero = SpawnPiece(heroPrefab, PieceType.Pawn, false, new Vector2Int(3, 1)) as HeroPiece; // 勇者(歩)
        turnManager.RegisterHero(hero);
        if (uiManager != null)
        {
            uiManager.TrackHero(hero);
            hero.OnEvolved += (oldType, newType) =>
            {
                uiManager.ShowEvolutionEffect(oldType, newType);
            };
        }
        SpawnPiece(allyPawnPrefab, PieceType.Pawn, false, new Vector2Int(5, 1));     // 歩（右）

        // --- 敵陣営の配置（3層の防衛ライン） ---
        // y=6: 本陣（王将を飛車・角・金で囲む）
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(0, 6));     // 歩（左端）
        SpawnPiece(enemyGoldPrefab, PieceType.Gold, true, new Vector2Int(1, 6));     // 金
        SpawnPiece(enemyRookPrefab, PieceType.Rook, true, new Vector2Int(2, 6));     // 飛車
        SpawnPiece(enemyKingPrefab, PieceType.King, true, new Vector2Int(3, 6));     // 王将（中央）
        SpawnPiece(enemyBishopPrefab, PieceType.Bishop, true, new Vector2Int(4, 6)); // 角
        SpawnPiece(enemyGoldPrefab, PieceType.Gold, true, new Vector2Int(5, 6));     // 金
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(6, 6));     // 歩（右端）

        // y=5: 第2防衛ライン（歩の壁 — 全幅）
        for (int x = 0; x < BoardGrid.Width; x++)
        {
            SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(x, 5));
        }

        // y=4: 前線部隊（歩×3 — 中央を前に押し出す）
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(2, 4));
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(3, 4));
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(4, 4));

        // セットアップ完了後、プレイヤーのターンを開始する
        turnManager.SetState(GameState.PlayerTurn);
    }

    /// <summary>
    /// プレハブから駒を生成し、盤面の指定座標に配置します。
    /// </summary>
    private Piece SpawnPiece(Piece prefab, PieceType type, bool isEnemy, Vector2Int pos)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"GameManager: {type} のプレハブがアサインされていません！");
            return null;
        }

        Piece newPiece = Instantiate(prefab);
        newPiece.Initialize(type, isEnemy, pos);
        
        // 盤面データへの登録
        boardGrid.PlacePiece(newPiece, pos);

        // 画面上の表示位置を合わせる
        if (boardView != null)
        {
            newPiece.transform.position = boardView.GetWorldPositionFromGrid(pos);
            newPiece.transform.SetParent(boardView.transform);
        }

        // 敵であればTurnManagerに登録
        if (isEnemy && newPiece is EnemyPiece enemy)
        {
            turnManager.RegisterEnemy(enemy);
        }

        return newPiece;
    }

    /// <summary>
    /// 王将が取られた時に KingPiece.cs から呼ばれるゲームクリア処理
    /// </summary>
    public void OnGameClear()
    {
        Debug.Log("<color=yellow>GameManager: 敵の王将を討ち取りました！ GAME CLEAR!!</color>");
        turnManager.SetState(GameState.GameOver);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayGameClear();

        if (uiManager != null)
        {
            uiManager.ShowGameClear();
        }
    }

    /// <summary>
    /// 自分の王将が取られた時に KingPiece.cs から呼ばれるゲームオーバー処理
    /// </summary>
    public void OnGameOver()
    {
        Debug.Log("<color=red>GameManager: 味方の王将が討ち取られました... GAME OVER</color>");
        turnManager.SetState(GameState.GameOver);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayGameOver();

        if (uiManager != null)
        {
            uiManager.ShowGameOver();
        }
    }
}
