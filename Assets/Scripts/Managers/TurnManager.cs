using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// 現在のターンの状態
/// </summary>
public enum GameState
{
    PlayerTurn,
    EnemyTurn,
    GameOver
}

/// <summary>
/// ターン進行とゲームサイクルのコアを管理します。
/// </summary>
public class TurnManager : MonoBehaviour
{
    // 現在のターンステート
    public GameState CurrentState { get; private set; }

    // 主人公と敵の管理リスト
    private HeroPiece heroPiece;
    private List<EnemyPiece> enemyPieces = new List<EnemyPiece>();

    [SerializeField] private BoardGrid boardGrid;
    [SerializeField] private BoardView boardView;
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private UIManager uiManager;

    public event Action OnPlayerTurnStarted;
    public event Action OnEnemyTurnStarted;

    private void Start()
    {
        // GameManager.StartGame() から SetState が呼ばれるまで待機
    }

    /// <summary>
    /// ターンの状態を変更し、関連するイベントを発火します。
    /// </summary>
    public void SetState(GameState newState)
    {
        CurrentState = newState;

        switch (newState)
        {
            case GameState.PlayerTurn:
                HandlePlayerTurnStart();
                break;
            case GameState.EnemyTurn:
                HandleEnemyTurnStart();
                break;
            case GameState.GameOver:
                Debug.Log("ゲーム終了");
                break;
        }
    }

    /// <summary>
    /// プレイヤーターン開始時の処理（リスポーン状況の確認など）
    /// </summary>
    private void HandlePlayerTurnStart()
    {
        Debug.Log("--- プレイヤーのターン ---");

        if (heroPiece != null && heroPiece.IsDead)
        {
            heroPiece.DecrementRespawnTurn(boardGrid, boardView);
        }

        if (uiManager != null) uiManager.OnNewPlayerTurn();
        OnPlayerTurnStarted?.Invoke();
        
        // （本来ならここでUIでの入力を待ちます）
    }

    /// <summary>
    /// 敵ターン開始時の処理
    /// </summary>
    private void HandleEnemyTurnStart()
    {
        Debug.Log("--- 敵のターン ---");
        OnEnemyTurnStarted?.Invoke();

        if (enemyAI != null && enemyPieces.Count > 0)
        {
            // EnemyAIに現在の敵駒リストを渡し、行動完了時のコールバックとしてプレイヤーのターンへ戻す処理を渡す
            StartCoroutine(enemyAI.ExecuteTurn(enemyPieces, () => 
            {
                SetState(GameState.PlayerTurn);
            }));
        }
        else
        {
            // 敵がいない場合などはすぐにプレイヤーのターンに戻す
            StartCoroutine(EnemyTurnRoutine());
        }
    }

    /// <summary>
    /// 敵がいない場合などのフェイルセーフ用コルーチン
    /// </summary>
    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        SetState(GameState.PlayerTurn);
    }

    /// <summary>
    /// プレイヤーが駒を移動させた後に呼ばれ、ターンを終了する
    /// </summary>
    public void EndPlayerTurn()
    {
        if (CurrentState == GameState.PlayerTurn)
        {
            SetState(GameState.EnemyTurn);
        }
    }

    // --- 管理用のユーティリティメソッド ---

    public void RegisterHero(HeroPiece hero)
    {
        heroPiece = hero;
    }

    public void RegisterEnemy(EnemyPiece enemy)
    {
        if (!enemyPieces.Contains(enemy))
        {
            enemyPieces.Add(enemy);
        }
    }

    public void UnregisterEnemy(EnemyPiece enemy)
    {
        if (enemyPieces.Contains(enemy))
        {
            enemyPieces.Remove(enemy);
        }
    }
}
