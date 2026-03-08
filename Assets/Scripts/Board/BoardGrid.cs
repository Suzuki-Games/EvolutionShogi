using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 盤面のデータ構造を管理するクラス。
/// テトリスなどと同じく、2次元配列を用いてグリッド座標の更新や当たり判定を処理します。
/// </summary>
public class BoardGrid : MonoBehaviour
{
    // 盤面サイズ：ミニ将棋の5x5
    public const int Width = 5;
    public const int Height = 5;

    // 5x5の盤面を管理する2次元配列 (nullなら駒なし)
    private Piece[,] grid = new Piece[Width, Height];

    /// <summary>
    /// 与えられた座標が盤面内（0〜4）に収まっているか判定します。
    /// （テトリスにおける枠外判定のロジック再利用）
    /// </summary>
    public static bool IsInsideBoard(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < Height;
    }

    /// <summary>
    /// 全く新しい駒を盤面に配置します（初期配置・リスポーン用）。
    /// </summary>
    public void PlacePiece(Piece piece, Vector2Int pos)
    {
        if (!IsInsideBoard(pos))
        {
            Debug.LogWarning($"配置失敗: 座標 {pos} は盤面外です。");
            return;
        }

        grid[pos.x, pos.y] = piece;
        piece.Position = pos;
    }

    /// <summary>
    /// 駒を移動させ、グリッド座標を更新します。
    /// プレイヤーの駒（主人公）が移動・駒取りを行った際の経験値加算もここで処理します。
    /// </summary>
    public void MovePiece(Piece piece, Vector2Int targetPos)
    {
        if (!IsInsideBoard(targetPos)) return;

        HeroPiece hero = piece as HeroPiece;
        int expToGain = 0;
        bool isHeroMoving = (hero != null && !hero.IsEnemy);

        // 移動先に敵駒があれば、その駒の OnTaken() を呼ぶ
        Piece targetPiece = GetPieceAt(targetPos);
        if (targetPiece != null && targetPiece.IsEnemy != piece.IsEnemy)
        {
            if (isHeroMoving)
            {
                // 敵の駒を取った場合、その駒に設定されたExpValueを獲得
                expToGain = targetPiece.ExpValue;
            }
            targetPiece.OnTaken();
        }
        else if (isHeroMoving && targetPiece == null)
        {
            // 空きマスへの前進行動は基本EXP1とする
            expToGain = 1;
        }

        // 経験値の付与実行（HeroPiece側で奥へ進むほどのボーナス計算を行う）
        if (isHeroMoving && expToGain > 0)
        {
            hero.AddExp(expToGain, targetPos);
        }

        // 元の位置を空（null）にする
        grid[piece.Position.x, piece.Position.y] = null;

        // 新しい位置に配置する
        PlacePiece(piece, targetPos);
    }

    /// <summary>
    /// 指定された座標に存在する駒を取得します（空の場合はnull）。
    /// </summary>
    public Piece GetPieceAt(Vector2Int pos)
    {
        if (IsInsideBoard(pos))
        {
            return grid[pos.x, pos.y];
        }
        return null;
    }

    /// <summary>
    /// 指定された座標の駒を取り除きます。
    /// </summary>
    public void RemovePieceAt(Vector2Int pos)
    {
        if (IsInsideBoard(pos))
        {
            grid[pos.x, pos.y] = null;
        }
    }

    /// <summary>
    /// 当たり判定（移動可能マス計算用）に盤面配列の参照を返します。
    /// </summary>
    public Piece[,] GetGrid()
    {
        return grid;
    }
}
