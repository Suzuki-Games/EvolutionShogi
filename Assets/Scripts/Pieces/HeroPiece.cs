using System;
using System.Collections;
using System.Collections.Generic;
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
    /// (PieceType oldType, PieceType newType)
    /// </summary>
    public event Action<PieceType, PieceType> OnEvolved;

    /// <summary>
    /// 前進時や駒を取った時に得られる経験値を敵陣に近いほどボーナス付与して処理します。
    /// </summary>
    public void AddExp(int baseAmount, Vector2Int targetPos)
    {
        if (IsDead) return;

        int bonusExp = 0;
        if (targetPos.y >= 5)
        {
            bonusExp = (targetPos.y - 4);
        }

        int totalExp = baseAmount + bonusExp;

        Debug.Log($"[HeroPiece] 経験値獲得: 基本={baseAmount}, ボーナス={bonusExp}, 合計={totalExp}");

        CurrentExp += totalExp;
        CheckEvolution();
    }

    /// <summary>
    /// 経験値に基づいた進化ロジック
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

            // 進化スケールアニメーション
            StartCoroutine(EvolutionScaleAnimation());

            // 外部に進化イベントを通知（UIテキスト演出用）
            OnEvolved?.Invoke(oldType, Type);
        }

        UpdateVisuals();
    }

    /// <summary>
    /// 進化時に駒が一瞬大きくなるアニメーション
    /// </summary>
    private IEnumerator EvolutionScaleAnimation()
    {
        float duration = 0.4f;
        float maxScale = 1.6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 膨らんで戻るカーブ（sin波の前半）
            float scale = 1f + (maxScale - 1f) * Mathf.Sin(t * Mathf.PI);
            transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 主人公が取られた時の特別ルール（リスポーン待機状態へ）。
    /// 撃破演出（拡大＋回転＋フェード）は基底の OnTaken に任せる。
    /// </summary>
    public override void OnTaken()
    {
        IsDead = true;
        RespawnTurnsLeft = 3;
        base.OnTaken();
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
        CurrentExp = 0;
        Type = PieceType.Pawn;

        UpdateVisuals();

        Vector2Int spawnPos = new Vector2Int(-1, -1);

        for (int y = 1; y >= 0; y--)
        {
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

            // OnTakenで無効化したColliderを復活させる（再選択可能にするため必須）
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = true;

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

    private List<Vector2Int> GetPawnMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));
        return moves;
    }

    private List<Vector2Int> GetSilverMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        AddMoveIfValid(board, moves, Position + new Vector2Int(0, 1));
        AddMoveIfValid(board, moves, Position + new Vector2Int(-1, 1));
        AddMoveIfValid(board, moves, Position + new Vector2Int(1, 1));
        AddMoveIfValid(board, moves, Position + new Vector2Int(-1, -1));
        AddMoveIfValid(board, moves, Position + new Vector2Int(1, -1));
        return moves;
    }

    private List<Vector2Int> GetRookMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
        Vector2Int[] directions = { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) };

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

    private List<Vector2Int> GetHeroMoves(Piece[,] board)
    {
        List<Vector2Int> moves = new List<Vector2Int>();
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
    /// 勇者駒を金色で他の味方駒と区別する
    /// </summary>
    public override void UpdateVisuals()
    {
        base.UpdateVisuals();

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && !IsDead)
        {
            sr.color = new Color(1f, 0.85f, 0.3f);
        }
    }

    protected override string GetSpriteName()
    {
        if (IsDead) return "King_王";
        return base.GetSpriteName();
    }
}
