using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 味方の一般駒（金、歩など）。
/// HeroPieceやKingPieceではない味方駒に使用します。
/// </summary>
public class AllyPiece : Piece
{
    public override void Initialize(PieceType type, bool isEnemy, Vector2Int startPos)
    {
        base.Initialize(type, false, startPos);

        switch (type)
        {
            case PieceType.Gold:
            case PieceType.Silver:
                ExpValue = 2;
                break;
            case PieceType.Pawn:
                ExpValue = 1;
                break;
            default:
                ExpValue = 1;
                break;
        }
    }

    public override List<Vector2Int> GetAvailableMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        // 味方は上（y方向+1）に進行する
        switch (Type)
        {
            case PieceType.Pawn:
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));
                break;

            case PieceType.Gold:
                // 金将：前、斜め前、横、後（斜め後ろ以外）
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));   // 前(上)
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, 1));   // 右前
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 1));  // 左前
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, 0));   // 右
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 0));  // 左
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, -1));  // 後(下)
                break;

            case PieceType.Silver:
                // 銀将：前3方向＋斜め後方
                AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, 1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(-1, -1));
                AddMoveIfValid(board, moves, Position + new Vector2Int(1, -1));
                break;
        }

        return moves;
    }

    private void AddMoveIfValid(Piece[,] board, List<Vector2Int> moves, Vector2Int nextPos)
    {
        if (BoardGrid.IsInsideBoard(nextPos))
        {
            Piece target = board[nextPos.x, nextPos.y];
            if (target == null || target.IsEnemy != this.IsEnemy)
            {
                moves.Add(nextPos);
            }
        }
    }
}
