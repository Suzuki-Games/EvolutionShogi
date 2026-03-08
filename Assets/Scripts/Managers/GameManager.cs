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

    [Header("Piece Prefabs (Assign in Inspector)")]
    [SerializeField] private HeroPiece heroPrefab;
    [SerializeField] private KingPiece playerKingPrefab;
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
    /// 非対称な初期配陣をセットアップします。
    /// 敵は強力な駒が並び、自陣は王と歩のみ。
    /// </summary>
    private void SetupInitialBoard()
    {
        Debug.Log("GameManager: 初期配置をセットアップします。");

        // --- プレイヤー陣営営の配置 ---
        // 自陣王将（下段中央: 2, 0）
        SpawnPiece(playerKingPrefab, PieceType.King, false, new Vector2Int(2, 0));
        
        // 主人公「歩」（王の前: 2, 1）
        HeroPiece hero = SpawnPiece(heroPrefab, PieceType.Pawn, false, new Vector2Int(2, 1)) as HeroPiece;
        turnManager.RegisterHero(hero);


        // --- 敵陣営の配置（チート級の初期配置） ---
        // 敵王将（上段中央: 2, 4）
        SpawnPiece(enemyKingPrefab, PieceType.King, true, new Vector2Int(2, 4));

        // 敵の強駒（y = 3, y = 4のラインに配置）
        SpawnPiece(enemyRookPrefab, PieceType.Rook, true, new Vector2Int(0, 4));   // 左奥：飛車
        SpawnPiece(enemyBishopPrefab, PieceType.Bishop, true, new Vector2Int(4, 4));// 右奥：角
        SpawnPiece(enemyGoldPrefab, PieceType.Gold, true, new Vector2Int(1, 3));   // 敵王の守り：金
        SpawnPiece(enemyGoldPrefab, PieceType.Gold, true, new Vector2Int(3, 3));   // 敵王の守り：金

        // 敵の歩（最前線 y = 2）
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(0, 2));
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(1, 2));
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(2, 2));
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(3, 2));
        SpawnPiece(enemyPawnPrefab, PieceType.Pawn, true, new Vector2Int(4, 2));

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
        
        if (uiManager != null)
        {
            uiManager.ShowGameOver();
        }
    }
}
