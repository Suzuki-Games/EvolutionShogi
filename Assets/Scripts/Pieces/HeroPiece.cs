using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 主人公クラス。
/// 進化（EXP）やリスポーンの概念など、主人公特有のロジックを持ちます。
/// 現在の状態（Type）により移動可能なマスが変化します。
/// </summary>
public class HeroPiece : Piece
{
    [Header("Hero Specific Status")]
    public int CurrentExp = 0;
    
    // リスポーンまでの残りターン数
    public int RespawnTurnsLeft = 0;
    
    // 死亡状態かどうかのフラグ
    public bool IsDead = false;

    private const int EXP_TO_SILVER = 2;
    private const int EXP_TO_ROOK = 5;
    private const int EXP_TO_HERO = 10;

    /// <summary>
    /// 進化発生時に外部から演出処理をフックするためのイベント。
    /// (PieceType oldType, PieceType newType, Vector2Int position, int shockwaveKills)
    /// </summary>
    public event Action<PieceType, PieceType, Vector2Int, int> OnEvolved;

    /// <summary>
    /// 前進時や駒を取った時に得られる経験値を敵陣に近いほどボーナス付与して処理します。
    /// </summary>
    /// <param name="baseAmount">基本となる経験値量（前進時は1、駒取得時は敵のExpValueなどを想定）</param>
    /// <param name="targetPos">アクションを起こした（移動した）先の座標</param>
    public void AddExp(int baseAmount, Vector2Int targetPos)
    {
        if (IsDead) return;

        // 敵陣（盤面の奥）に近いほどボーナスを付与するロジック
        // プレイヤー側を下から上へ(y: 0 -> 6)の進行と仮定
        // y が 5 または 6 のエリアを敵陣（奥）とする
        int bonusExp = 0;
        if (targetPos.y >= 5)
        {
            bonusExp = (targetPos.y - 4); // y=5なら+1, y=6なら+2のボーナス
        }

        int totalExp = baseAmount + bonusExp;
        
        Debug.Log($"[HeroPiece] 経験値獲得: 基本={baseAmount}, ボーナス={bonusExp}, 合計={totalExp}");

        CurrentExp += totalExp;
        CheckEvolution();
    }

    /// <summary>
    /// 経験値に基づいた動的な進化ロジック
    /// </summary>
    private void CheckEvolution()
    {
        PieceType oldType = Type;

        if (CurrentExp >= EXP_TO_HERO)
        {
            Type = PieceType.Hero;
        }
        else if (CurrentExp >= EXP_TO_ROOK)
        {
            Type = PieceType.Rook;
        }
        else if (CurrentExp >= EXP_TO_SILVER)
        {
            Type = PieceType.Silver;
        }

        if (oldType != Type)
        {
            Debug.Log($"進化した！ {oldType} -> {Type}");
            if (AudioManager.Instance != null) AudioManager.Instance.PlayEvolve();

            // 進化衝撃波を発動
            int kills = ExecuteShockwave(oldType, Type);

            // 外部に進化イベントを通知（演出用）
            OnEvolved?.Invoke(oldType, Type, Position, kills);
        }

        UpdateVisuals();
    }

    /// <summary>
    /// 進化衝撃波：進化段階に応じて周囲の敵を除去する
    /// </summary>
    private int ExecuteShockwave(PieceType oldType, PieceType newType)
    {
        BoardGrid boardGrid = FindAnyObjectByType<BoardGrid>();
        if (boardGrid == null) return 0;

        List<Vector2Int> affectedPositions = new List<Vector2Int>();

        if (newType == PieceType.Silver)
        {
            // 歩→銀: 周囲1マスの敵を除去
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    affectedPositions.Add(Position + new Vector2Int(dx, dy));
                }
            }
        }
        else if (newType == PieceType.Rook)
        {
            // 銀→飛: 十字方向2マスの敵を除去
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            foreach (var dir in dirs)
            {
                for (int dist = 1; dist <= 2; dist++)
                {
                    affectedPositions.Add(Position + dir * dist);
                }
            }
        }
        else if (newType == PieceType.Hero)
        {
            // 飛→勇者: 全方向2マスの敵を除去
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    affectedPositions.Add(Position + new Vector2Int(dx, dy));
                }
            }
        }

        // 衝撃波の範囲をタイルにオレンジ色で表示
        BoardView boardView = FindAnyObjectByType<BoardView>();
        if (boardView != null)
        {
            float duration = (newType == PieceType.Hero) ? 1.5f : 1.0f;
            boardView.ShowShockwaveEffect(affectedPositions, duration);
        }

        // 対象マスの敵駒を除去
        int killCount = 0;
        foreach (var pos in affectedPositions)
        {
            if (!BoardGrid.IsInsideBoard(pos)) continue;
            Piece target = boardGrid.GetPieceAt(pos);
            if (target != null && target.IsEnemy)
            {
                Debug.Log($"[Shockwave] {target.Type} at {pos} eliminated!");

                // 持ち駒に追加
                if (HandManager.Instance != null)
                {
                    HandManager.Instance.AddToHand(target.Type, false);
                }

                boardGrid.RemovePieceAt(pos);
                target.OnTaken();
                killCount++;
            }
        }

        if (killCount > 0)
        {
            Debug.Log($"[衝撃波] {killCount}体の敵を吹き飛ばした！");
        }

        return killCount;
    }

    /// <summary>
    /// 主人公が取られた時の特別ルール（リスポーン待機状態へ）
    /// </summary>
    public override void OnTaken()
    {
        IsDead = true;
        RespawnTurnsLeft = 3; // 3ターン後に再出撃
        gameObject.SetActive(false); // 盤面からは一旦消す
    }

    /// <summary>
    /// ターン経過時に呼び出し、リスポーンカウントを減らします。
    /// </summary>
    public void DecrementRespawnTurn(BoardGrid boardGrid, BoardView boardView = null)
    {
        if (!IsDead) return;

        RespawnTurnsLeft--;
        if (RespawnTurnsLeft <= 0)
        {
            Respawn(boardGrid, boardView);
        }
    }

    /// <summary>
    /// 不屈の精神で復活（レベル1：歩に戻る）
    /// </summary>
    private void Respawn(BoardGrid boardGrid, BoardView boardView)
    {
        IsDead = false;
        CurrentExp = 0; // 経験値リセット
        Type = PieceType.Pawn; // 歩に戻る
        
        UpdateVisuals(); // リスポーンでレベル戻ったので更新

        // 自陣最前列（y = 1 または y = 0）の空きマスを探して配置する
        Vector2Int spawnPos = new Vector2Int(-1, -1);

        // y=1（少し前）から探し、無理ならy=0（最後方）を探す
        for (int y = 1; y >= 0; y--)
        {
            // x中心から左右へ探す（7×7盤面）
            int center = BoardGrid.Width / 2;
            List<int> xSearchOrder = new List<int> { center };
            for (int offset = 1; offset <= center; offset++)
            {
                if (center - offset >= 0) xSearchOrder.Add(center - offset);
                if (center + offset < BoardGrid.Width) xSearchOrder.Add(center + offset);
            }

            foreach (int x in xSearchOrder)
            {
                if (boardGrid.GetPieceAt(new Vector2Int(x, y)) == null)
                {
                    spawnPos = new Vector2Int(x, y);
                    break;
                }
            }
            if (spawnPos.x != -1) break;
        }

        if (spawnPos.x != -1)
        {
            boardGrid.PlacePiece(this, spawnPos);
            gameObject.SetActive(true);

            // ビジュアル位置を更新
            if (boardView != null)
            {
                transform.position = boardView.GetWorldPositionFromGrid(spawnPos);
            }

            Debug.Log($"リスポーンしました: {spawnPos}");
        }
        else
        {
            Debug.LogWarning("リスポーン可能な空きマスが自陣にありません。");
        }
    }

    /// <summary>
    /// 進行度（PieceType）に応じて移動可能座標の計算を切り替えます。
    /// </summary>
    public override List<Vector2Int> GetAvailableMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();

        if (IsDead) return moves;

        switch (Type)
        {
            case PieceType.Pawn:
                moves = GetPawnMoves(board);
                break;
            case PieceType.Silver:
                moves = GetSilverMoves(board);
                break;
            case PieceType.Rook:
                moves = GetRookMoves(board);
                break;
            case PieceType.Hero:
                moves = GetHeroMoves(board);
                break;
        }

        return moves;
    }

    // --- 各形態ごとの移動ロジック ---
    // ユーティリティ：指定座標が盤面内で、空きマスか敵なら追加する
    private void AddMoveIfValid(Piece[,] board, List<Vector2Int> moves, Vector2Int nextPos)
    {
        if (BoardGrid.IsInsideBoard(nextPos))
        {
            Piece target = board[nextPos.x, nextPos.y];
            // ターゲットが空、もしくは自分と違う陣営（敵）なら移動可能
            if (target == null || target.IsEnemy != this.IsEnemy)
            {
                moves.Add(nextPos);
            }
        }
    }

    private List<Vector2Int> GetPawnMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        // 前方へ1マス（主人公は味がなのでy方向に+1）
        AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));
        return moves;
    }

    private List<Vector2Int> GetSilverMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        // 前方3方向＋斜め後方
        AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));   // 前
        AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 1));  // 左前
        AddMoveIfValid(board, moves, Position + new Vector2Int(1, 1));   // 右前
        AddMoveIfValid(board, moves, Position + new Vector2Int(-1, -1)); // 左後
        AddMoveIfValid(board, moves, Position + new Vector2Int(1, -1));  // 右後
        return moves;
    }

    private List<Vector2Int> GetRookMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        // 縦横の直線移動（障害物に当たるまで）
        Vector2Int[] directions = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

        foreach (var dir in directions)
        {
            Vector2Int currentPos = Position + dir;
            while (BoardGrid.IsInsideBoard(currentPos))
            {
                Piece target = board[currentPos.x, currentPos.y];
                if (target == null)
                {
                    moves.Add(currentPos); // 空きマスなら進める
                }
                else
                {
                    if (target.IsEnemy != this.IsEnemy)
                    {
                        moves.Add(currentPos); // 敵ならそこまで進んで取れる
                    }
                    break; // 何らかの駒にぶつかったらその方向の探索は終了
                }
                currentPos += dir;
            }
        }
        return moves;
    }

    private List<Vector2Int> GetHeroMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        // 勇者：全方向への移動（飛車＋角の動きに相当：クイーン）
        Vector2Int[] directions = { 
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        foreach (var dir in directions)
        {
            Vector2Int currentPos = Position + dir;
            while (BoardGrid.IsInsideBoard(currentPos))
            {
                Piece target = board[currentPos.x, currentPos.y];
                if (target == null)
                {
                    moves.Add(currentPos);
                }
                else
                {
                    if (target.IsEnemy != this.IsEnemy)
                    {
                        moves.Add(currentPos);
                    }
                    break;
                }
                currentPos += dir;
            }
        }
        return moves;
    }

    /// <summary>
    /// 死亡中は墓石画像、それ以外は現在のTypeに対応した画像を返します。
    /// </summary>
    protected override string GetSpriteName()
    {
        if (IsDead) return "King_王"; // 仮：墓石画像がなければ王画像で代用
        return base.GetSpriteName();
    }
}
