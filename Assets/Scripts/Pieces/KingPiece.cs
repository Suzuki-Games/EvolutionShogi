using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王将クラス。
/// 取られた際にゲームの勝敗を決定します。
/// </summary>
public class KingPiece : Piece
{
    private void Awake()
    {
        Type = PieceType.King;
    }

    /// <summary>
    /// 初期化（基底の実装を呼び出し種類のみ確定）
    /// </summary>
    public override void Initialize(PieceType type, bool isEnemy, Vector2Int startPos)
    {
        base.Initialize(PieceType.King, isEnemy, startPos);
        ExpValue = 10; // 味方の王が取られることはゲームオーバーだが一応設定
    }

    /// <summary>
    /// 王将が取られた際の特別ルール（ゲームの勝敗決定）
    /// </summary>
    public override void OnTaken()
    {
        base.OnTaken();
        
        // TurnManagerやGameManagerに勝敗の通知を送る
        if (IsEnemy)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameClear();
        }
        else
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver();
        }
    }

    /// <summary>
    /// 王将の移動範囲（全方向1マス）
    /// </summary>
    public override List<Vector2Int> GetAvailableMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                Vector2Int nextPos = Position + new Vector2Int(x, y);

                if (BoardGrid.IsInsideBoard(nextPos))
                {
                    Piece target = board[nextPos.x, nextPos.y];
                    // 空きマス、または敵の駒であれば移動可能
                    if (target == null || target.IsEnemy != this.IsEnemy)
                    {
                        moves.Add(nextPos);
                    }
                }
            }
        }

        return moves;
    }
}
