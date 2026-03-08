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
        bool canAttack = false;

        // 1. 全ての敵駒の全移動可能マスを調べ、プレイヤーの駒を攻撃できる手を探す
        foreach (var enemy in enemies)
        {
            if (!enemy.gameObject.activeSelf) continue; // 取られた駒はスキップ

            var moves = enemy.GetAvailableMoves(boardGrid.GetGrid());
            foreach (var move in moves)
            {
                Piece target = boardGrid.GetPieceAt(move);
                // 移動先にプレイヤー（Hero）がいる場合
                if (target != null && !target.IsEnemy)
                {
                    bestPiece = enemy;
                    bestMove = move;
                    canAttack = true;
                    break;
                }
            }
            if (canAttack) break;
        }

        // 2. 攻撃可能な手が無ければ、ランダムに動かせる駒とマスを選ぶ
        if (!canAttack)
        {
            List<EnemyPiece> movableEnemies = new List<EnemyPiece>();
            foreach (var enemy in enemies)
            {
                if (enemy.gameObject.activeSelf && enemy.GetAvailableMoves(boardGrid.GetGrid()).Count > 0)
                {
                    movableEnemies.Add(enemy);
                }
            }

            if (movableEnemies.Count > 0)
            {
                // ランダムな駒を選択
                bestPiece = movableEnemies[Random.Range(0, movableEnemies.Count)];
                var moves = bestPiece.GetAvailableMoves(boardGrid.GetGrid());
                // ランダムな移動先を選択
                bestMove = moves[Random.Range(0, moves.Count)];
            }
        }

        // 3. 実際の移動処理
        if (bestPiece != null)
        {
            Debug.Log($"[EnemyAI] {bestPiece.Type} が {bestMove} へ移動します。 (Attack: {canAttack})");
            
            boardGrid.MovePiece(bestPiece, bestMove);
            
            if (boardView != null)
            {
                Vector3 worldTargetPos = boardView.GetWorldPositionFromGrid(bestMove);
                // TODO: DOTweenなどで滑らかに移動させるのがベストだが、ここでは瞬間移動
                bestPiece.transform.position = worldTargetPos;
            }
        }
        else
        {
            Debug.Log("[EnemyAI] 動かせる駒がありませんでした。パスします。");
        }

        yield return new WaitForSeconds(0.5f); // 移動後待機演出
        onTurnFinished?.Invoke();
    }
}
