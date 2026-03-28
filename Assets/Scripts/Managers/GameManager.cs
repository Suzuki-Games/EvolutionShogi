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
        SetupInitialBoard();
    }

    /// <summary>
    /// 非対称な初期配陣をセットアップします（7×7盤面）。
    /// 敵は強力な駒が並び、自陣は王・勇者(歩)・歩×2・金×2の弱いスタート。
    /// </summary>
    private void SetupInitialBoard()
    {
        Debug.Log("GameManager: 初期配置をセットアップします（7×7）。");

        // --- プレイヤー陣営の配置 (y=0: 後衛, y=1: 前衛) ---
        SpawnPiece(playerKingPrefab, PieceType.King, false, new Vector2Int(2, 0));   // 王将
        SpawnPiece(allyGoldPrefab, PieceType.Gold, false, new Vector2Int(0, 0));     // 金（左）
        SpawnPiece(allyGoldPrefab, PieceType.Gold, false, new Vector2Int(4, 0));     // 金（右）

        SpawnPiece(allyPawnPrefab, PieceType.Pawn, false, new Vector2Int(0, 1));     // 歩（左）
        HeroPiece hero = SpawnPiece(heroPrefab, PieceType.Pawn, false, new Vector2Int(2, 1)) as HeroPiece; // 勇者(歩)
        turnManager.RegisterHero(hero);
        if (uiManager != null) uiManager.TrackHero(hero);
        SpawnPiece(allyPawnPrefab, PieceType.Pawn, false, new Vector2Int(4, 1));     // 歩（右）

        // --- 敵陣営の配置（チート級の初期配置） ---
        // y=6: 強力な後衛
        SpawnPiece(enemyRookPrefab, PieceType.Rook, true, new Vector2Int(0, 6));     // 飛車
        SpawnPiece(enemyGoldPrefab, PieceType.Gold, true, new Vector2Int(1, 6));     // 金
        SpawnPiece(enemyKingPrefab, PieceType.King, true, new Vector2Int(2, 6));     // 王将
        SpawnPiece(enemyGoldPrefab, PieceType.Gold, true, new Vector2Int(3, 6));     // 金
        SpawnPiece(enemyBishopPrefab, PieceType.Bishop, true, new Vector2Int(4, 6)); // 角

        // y=5: 前衛（少数精鋭）
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(1, 5));     // 歩
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(2, 5));     // 銀の位置に歩（銀プレハブがないため）
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(3, 5));     // 歩

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
