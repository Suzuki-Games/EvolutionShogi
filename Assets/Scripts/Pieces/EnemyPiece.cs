using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵のダミー駒（王将、飛車、角など）の基本実装
/// </summary>
public class EnemyPiece : Piece
{
    private void Awake()
    {
        // 敵駒であることを保証する
        IsEnemy = true;
    }

    /// <summary>
    /// 初期化時に種類に応じた経験値などを設定
    /// </summary>
    public override void Initialize(PieceType type, bool isEnemy, Vector2Int startPos)
    {
        base.Initialize(type, true, startPos); // 強制的にEnemy

        switch (type)
        {
            case PieceType.Pawn:
                ExpValue = 1;
                break;
            case PieceType.Gold:
            case PieceType.Silver:
                ExpValue = 2;
                break;
            case PieceType.Bishop:
            case PieceType.Rook:
                ExpValue = 5; // 強力な駒を倒すと大量EXP
                break;
            case PieceType.King:
                ExpValue = 10;
                break;
            default:
                ExpValue = 1;
                break;
        }
    }

    /// <summary>
    /// 全敵駒共通の移動アルゴリズム（仮）
    /// 将来的にはAI専用の `GetAvailableMoves` へと拡張予定。
    /// </summary>
    public override List<Vector2Int> GetAvailableMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        
        // 敵は下（y方向-1）に進行する
        Vector2Int forward = new Vector2Int(0, -1);
        
        switch (Type)
        {
            case PieceType.Pawn:
                AddMoveIfValid(board, moves, Position + forward);
                break;
                
            case PieceType.Gold:
                // 金将：前、斜め前、横、後（斜め後ろ以外）
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, -1)); // 前(下)
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, -1)); // 右前
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, -1));// 左前
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, 0));  // 右
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 0)); // 左
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));  // 後(上)
                break;

            case PieceType.Rook:
                // 飛車：縦横（簡易的に1マスだけとしているが、本来はループで端まで）
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, -1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, 0));
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 0));
                break;
                
            case PieceType.Bishop:
                // 角行：斜め（簡易的に1マスで実装中）
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, 1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, -1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, -1));
                break;

            case PieceType.King:
                // 王将：全方向1マス
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        AddMoveIfValid(board, moves, Position + new Vector2Int(x, y));
                    }
                }
                break;
        }

        return moves;
    }

    private void AddMoveIfValid(Piece[,] board, List<Vector2Int> moves, Vector2Int nextPos)
    {
        if (BoardGrid.IsInsideBoard(nextPos))
        {
            Piece target = board[nextPos.x, nextPos.y];
            // 空きマス、またはプレイヤーの駒（Enemyではない）なら移動可能
            if (target == null || target.IsEnemy == false)
            {
                moves.Add(nextPos);
            }
        }
    }
}
