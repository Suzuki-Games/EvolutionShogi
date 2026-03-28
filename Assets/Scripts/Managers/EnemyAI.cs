using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵駒の思考ルーチンを管理します。
/// 方針：1手先で駒を取れるなら取る、無ければランダムに動く軽量AI。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public BoardGrid boardGrid;
    public BoardView boardView;

    [Header("Prefabs for Enemy Drops")]
    [SerializeField] private EnemyPiece enemyPawnPrefab;
    [SerializeField] private EnemyPiece enemyGoldPrefab;

    /// <summary>
    /// AIのターン処理を実行します。
    /// 現在盤面にいる全敵駒の中から1匹を選んで行動させる想定（テスト用簡易実装）。
    /// 実際のゲーム性に合わせて「全敵が1歩ずつ動く」か「1ターンに1匹だけ動く」か調整可能です。
    /// ここでは「全敵駒の中から行動可能なものを1つ選び、最適な手を指す」形式とします。
    /// </summary>
    public IEnumerator ExecuteTurn(List<EnemyPiece> enemies, System.Action onTurnFinished)
    {
        yield return new WaitForSeconds(0.5f); // 思考時間演出

        EnemyPiece bestPiece = null;
        Vector2Int bestMove = Vector2Int.zero;
        int bestScore = int.MinValue;

        // 全敵駒の全手を評価し、最もスコアの高い手を選ぶ
        foreach (var enemy in enemies)
        {
            if (!enemy.gameObject.activeSelf) continue;

            var moves = enemy.GetAvailableMoves(boardGrid.GetGrid());
            foreach (var move in moves)
            {
                int score = EvaluateMove(enemy, move);
                // 同スコアならランダムに入れ替え（単調な動きを防ぐ）
                if (score > bestScore || (score == bestScore && Random.value > 0.5f))
                {
                    bestScore = score;
                    bestPiece = enemy;
                    bestMove = move;
                }
            }
        }

        // 持ち駒の打ち込みも候補に入れる
        Vector2Int dropPos = Vector2Int.zero;
        PieceType dropType = PieceType.Pawn;
        int dropScore = int.MinValue;
        bool shouldDrop = false;

        if (HandManager.Instance != null && HandManager.Instance.EnemyHand.Count > 0)
        {
            // 持ち駒ごとに最適な打ち場所を評価
            HashSet<PieceType> checkedTypes = new HashSet<PieceType>();
            foreach (var handPiece in HandManager.Instance.EnemyHand)
            {
                if (checkedTypes.Contains(handPiece)) continue;
                checkedTypes.Add(handPiece);

                var validDrops = GetEnemyDropPositions(handPiece);
                foreach (var pos in validDrops)
                {
                    int score = EvaluateDrop(handPiece, pos);
                    if (score > dropScore)
                    {
                        dropScore = score;
                        dropPos = pos;
                        dropType = handPiece;
                    }
                }
            }
            shouldDrop = dropScore > bestScore;
        }

        // 持ち駒を打つ方が良い場合
        if (shouldDrop)
        {
            Debug.Log($"[EnemyAI] 持ち駒 {dropType} を {dropPos} に打ちます。");
            HandManager.Instance.UseFromHand(dropType, true);

            EnemyPiece prefab = GetEnemyPrefabForType(dropType);
            if (prefab != null)
            {
                EnemyPiece newPiece = Instantiate(prefab);
                newPiece.Initialize(dropType, true, dropPos);
                boardGrid.PlacePiece(newPiece, dropPos);

                TurnManager tm = FindAnyObjectByType<TurnManager>();
                if (tm != null) tm.RegisterEnemy(newPiece);

                if (boardView != null)
                {
                    newPiece.transform.position = boardView.GetWorldPositionFromGrid(dropPos);
                    newPiece.transform.SetParent(boardView.transform);
                }

                if (AudioManager.Instance != null) AudioManager.Instance.PlayMove();
            }
        }
        // 通常の移動処理
        else if (bestPiece != null)
        {
            bool isAttack = boardGrid.GetPieceAt(bestMove) != null;
            Debug.Log($"[EnemyAI] {bestPiece.Type} が {bestMove} へ移動します。 (Attack: {isAttack})");

            boardGrid.MovePiece(bestPiece, bestMove);

            if (boardView != null)
            {
                Vector3 worldTargetPos = boardView.GetWorldPositionFromGrid(bestMove);
                if (PieceMover.Instance != null)
                {
                    bool animDone = false;
                    PieceMover.Instance.AnimateMove(bestPiece.transform, worldTargetPos, () => animDone = true);
                    yield return new WaitUntil(() => animDone);
                }
                else
                {
                    bestPiece.transform.position = worldTargetPos;
                }
            }
        }
        else
        {
            Debug.Log("[EnemyAI] 動かせる駒がありませんでした。パスします。");
        }

        yield return new WaitForSeconds(0.3f);
        onTurnFinished?.Invoke();
    }

    /// <summary>
    /// 手のスコアを評価する。高いほど良い手。
    /// </summary>
    private int EvaluateMove(EnemyPiece enemy, Vector2Int move)
    {
        int score = 0;
        Piece target = boardGrid.GetPieceAt(move);

        // --- 攻撃評価 ---
        if (target != null && !target.IsEnemy)
        {
            if (target is KingPiece)
                score += 10000; // 王を取れるなら最優先
            else if (target is HeroPiece)
                score += 500;   // 勇者を倒すと相手の成長を止める
            else
                score += target.ExpValue * 100;
        }

        // --- リスク評価（高価値駒の無謀な突撃を防ぐ） ---
        if (enemy.Type == PieceType.Rook || enemy.Type == PieceType.Bishop)
        {
            // 移動先がプレイヤーの攻撃範囲内かチェック
            if (IsThreatenedByPlayer(move))
            {
                // 駒取りでない移動なら大きなペナルティ（無駄死にを防ぐ）
                if (target == null || target.IsEnemy)
                    score -= 300;
                else
                    score -= 50; // 駒取りでもリスクは考慮
            }

            // 飛車・角は自陣に留まるほうがスコアが高い（序盤の守り）
            if (move.y <= 3)
                score -= 100; // 敵陣（y<=3）への深入りを抑制
        }

        // --- 位置評価 ---
        // 前進ボーナス（歩・金は前に出たい）
        if (enemy.Type == PieceType.Pawn || enemy.Type == PieceType.Gold)
        {
            int advance = enemy.Position.y - move.y;
            if (advance > 0) score += advance * 10;
        }

        // 中央寄りボーナス
        int centerX = BoardGrid.Width / 2;
        int distFromCenter = Mathf.Abs(move.x - centerX);
        score += (3 - distFromCenter);

        return score;
    }

    /// <summary>
    /// 指定マスがプレイヤー駒の攻撃範囲内かどうか判定する
    /// </summary>
    private bool IsThreatenedByPlayer(Vector2Int pos)
    {
        Piece[,] grid = boardGrid.GetGrid();
        for (int x = 0; x < BoardGrid.Width; x++)
        {
            for (int y = 0; y < BoardGrid.Height; y++)
            {
                Piece p = grid[x, y];
                if (p != null && !p.IsEnemy)
                {
                    var moves = p.GetAvailableMoves(grid);
                    if (moves.Contains(pos)) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 敵が持ち駒を打てる位置を取得（敵は上側なのでy=0が最奥段）
    /// </summary>
    private List<Vector2Int> GetEnemyDropPositions(PieceType type)
    {
        List<Vector2Int> positions = new List<Vector2Int>();
        for (int x = 0; x < BoardGrid.Width; x++)
        {
            // 敵歩の二歩チェック
            if (type == PieceType.Pawn)
            {
                bool hasPawnInColumn = false;
                for (int checkY = 0; checkY < BoardGrid.Height; checkY++)
                {
                    Piece p = boardGrid.GetPieceAt(new Vector2Int(x, checkY));
                    if (p != null && p.IsEnemy && p.Type == PieceType.Pawn)
                    {
                        hasPawnInColumn = true;
                        break;
                    }
                }
                if (hasPawnInColumn) continue;
            }

            for (int y = 0; y < BoardGrid.Height; y++)
            {
                // 敵歩はy=0には打てない（動けなくなる）
                if (type == PieceType.Pawn && y == 0) continue;

                Vector2Int pos = new Vector2Int(x, y);
                if (boardGrid.GetPieceAt(pos) == null)
                {
                    positions.Add(pos);
                }
            }
        }
        return positions;
    }

    /// <summary>
    /// 持ち駒を打つ手のスコアを評価
    /// </summary>
    private int EvaluateDrop(PieceType type, Vector2Int pos)
    {
        int score = 0;

        // プレイヤーの王に近い位置に打つと高スコア
        Piece[,] grid = boardGrid.GetGrid();
        for (int x = 0; x < BoardGrid.Width; x++)
        {
            for (int y = 0; y < BoardGrid.Height; y++)
            {
                Piece p = grid[x, y];
                if (p != null && !p.IsEnemy && p is KingPiece)
                {
                    int dist = Mathf.Abs(pos.x - x) + Mathf.Abs(pos.y - y);
                    score += Mathf.Max(0, 10 - dist) * 5;
                }
            }
        }

        // 前線（プレイヤー側）に打つほど攻撃的
        score += (BoardGrid.Height - 1 - pos.y) * 3;

        // 中央寄りボーナス
        int centerX = BoardGrid.Width / 2;
        score += (3 - Mathf.Abs(pos.x - centerX));

        return score;
    }

    private EnemyPiece GetEnemyPrefabForType(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return enemyPawnPrefab;
            case PieceType.Gold: return enemyGoldPrefab;
            default: return enemyPawnPrefab;
        }
    }
}
