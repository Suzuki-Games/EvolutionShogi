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

        // 3. 実際の移動処理
        if (bestPiece != null)
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

        if (target != null && !target.IsEnemy)
        {
            // 駒取りボーナス（高価値ほど高スコア）
            if (target is KingPiece)
                score += 10000; // 王を取れるなら最優先
            else if (target is HeroPiece)
                score += 500;   // 勇者を倒すと相手の成長を止められる
            else
                score += target.ExpValue * 100;
        }

        // 前進ボーナス（プレイヤー側へ進む = y が小さい方向）
        int advance = enemy.Position.y - move.y;
        if (advance > 0) score += advance * 5;

        // 中央寄りボーナス（盤面制圧）
        int centerX = BoardGrid.Width / 2;
        int distFromCenter = Mathf.Abs(move.x - centerX);
        score += (3 - distFromCenter);

        return score;
    }
}
