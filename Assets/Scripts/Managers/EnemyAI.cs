using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵駒の思考ルーチンを管理します。
/// 1ターンに最大2回行動し、各行動で全敵駒の全手をスコア評価して最善手を選ぶ。
/// 評価軸：攻撃価値・勇者からの距離・前進度・自陣リスク。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    public BoardGrid boardGrid;
    public BoardView boardView;

    [Header("Prefabs for Enemy Drops")]
    [SerializeField] private EnemyPiece enemyPawnPrefab;
    [SerializeField] private EnemyPiece enemyGoldPrefab;

    [Header("AI Settings")]
    [Tooltip("敵が1ターンに行動できる最大回数")]
    [SerializeField] private int actionsPerTurn = 2;

    // ターン中の盤面評価用キャッシュ（勇者の位置と進化状態）
    private Vector2Int? cachedHeroPos;
    private bool cachedHeroEvolved;

    /// <summary>
    /// AIのターン処理。最大 actionsPerTurn 回まで連続行動する。
    /// 1回目: 最善の通常移動 vs 持ち駒打ちの比較。
    /// 2回目以降: 通常移動のみ（連続打ち込みは強すぎるため）。
    /// </summary>
    public IEnumerator ExecuteTurn(List<EnemyPiece> enemies, System.Action onTurnFinished)
    {
        yield return new WaitForSeconds(0.4f);

        HashSet<EnemyPiece> alreadyMoved = new HashSet<EnemyPiece>();
        TurnManager tm = FindAnyObjectByType<TurnManager>();

        for (int actionIndex = 0; actionIndex < actionsPerTurn; actionIndex++)
        {
            // ゲームが終了していればこれ以上動かない（王を取った直後など）
            if (tm != null && tm.CurrentState == GameState.GameOver) break;

            // ターンの各行動前に勇者キャッシュを更新（勇者が取られた・進化したケースに対応）
            RefreshHeroCache();

            // --- 通常移動の最善手を探す ---
            EnemyPiece bestPiece = null;
            Vector2Int bestMove = Vector2Int.zero;
            int bestScore = int.MinValue;

            foreach (var enemy in enemies)
            {
                if (!enemy.gameObject.activeSelf) continue;
                if (alreadyMoved.Contains(enemy)) continue; // 同じ駒は1ターンに1回まで

                var moves = enemy.GetAvailableMoves(boardGrid.GetGrid());
                foreach (var move in moves)
                {
                    // ── 単ターン即詰み防止 ──
                    // 2手目以降では王を取る手を禁止する。これがないと「1手目で詰みの形を作り
                    // 2手目で王を取る」コンボが防御不能になる（持ち駒打ち→翌アクションで王捕獲など）。
                    // この規則によりプレイヤーは必ず1ターンの応手機会を得られる。
                    if (actionIndex >= 1)
                    {
                        Piece target = boardGrid.GetPieceAt(move);
                        if (target is KingPiece && !target.IsEnemy) continue;
                    }

                    int score = EvaluateMove(enemy, move);
                    if (score > bestScore || (score == bestScore && Random.value > 0.5f))
                    {
                        bestScore = score;
                        bestPiece = enemy;
                        bestMove = move;
                    }
                }
            }

            // --- 持ち駒打ちは1回目の行動でのみ評価（連続打ち抑止）---
            bool shouldDrop = false;
            Vector2Int dropPos = Vector2Int.zero;
            PieceType dropType = PieceType.Pawn;

            if (actionIndex == 0 && HandManager.Instance != null && HandManager.Instance.EnemyHand.Count > 0)
            {
                int dropScore = int.MinValue;
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

            // --- 行動の実行 ---
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

                    if (tm != null) tm.RegisterEnemy(newPiece);

                    if (boardView != null)
                    {
                        newPiece.transform.position = boardView.GetWorldPositionFromGrid(dropPos);
                        newPiece.transform.SetParent(boardView.transform);
                    }

                    if (AudioManager.Instance != null) AudioManager.Instance.PlayMove();
                }
            }
            else if (bestPiece != null)
            {
                bool isAttack = boardGrid.GetPieceAt(bestMove) != null;
                Debug.Log($"[EnemyAI] [{actionIndex + 1}/{actionsPerTurn}] {bestPiece.Type} が {bestMove} へ移動します。 (Attack: {isAttack})");

                boardGrid.MovePiece(bestPiece, bestMove);
                alreadyMoved.Add(bestPiece);

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
                Debug.Log("[EnemyAI] 動かせる駒がありません。残りの行動をスキップします。");
                break;
            }

            // 次の行動までの間隔
            yield return new WaitForSeconds(0.25f);
        }

        yield return new WaitForSeconds(0.2f);
        onTurnFinished?.Invoke();
    }

    /// <summary>
    /// 盤面をスキャンして勇者の位置と進化状態をキャッシュする。
    /// EvaluateMoveで毎回スキャンすると O(N*49) になるためターン開始時にまとめて取得。
    /// </summary>
    private void RefreshHeroCache()
    {
        cachedHeroPos = null;
        cachedHeroEvolved = false;

        Piece[,] grid = boardGrid.GetGrid();
        for (int x = 0; x < BoardGrid.Width; x++)
        {
            for (int y = 0; y < BoardGrid.Height; y++)
            {
                if (grid[x, y] is HeroPiece hero && !hero.IsEnemy && !hero.IsDead)
                {
                    cachedHeroPos = new Vector2Int(x, y);
                    cachedHeroEvolved = (hero.Type != PieceType.Pawn);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 手のスコアを評価する。高いほど良い手。
    /// 評価軸：攻撃価値（取れる駒）／勇者からの距離／前進度／自陣リスク。
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

            // 自陣（y<=3）侵入の扱いは勇者の進化状態で反転する。
            // 序盤（勇者=歩）は守り重視で篭る。勇者が銀以上に進化したら攻撃モードに切り替え、
            // 飛車・角を前線へ送り出してプレッシャーを掛ける。
            if (move.y <= 3)
            {
                if (cachedHeroEvolved)
                    score += 30;  // 攻撃モード：前進を後押し
                else
                    score -= 100; // 守りモード：序盤は自陣維持
            }
        }

        // --- 位置評価 ---
        // 前進ボーナス（歩・金は前に出たい）
        if (enemy.Type == PieceType.Pawn || enemy.Type == PieceType.Gold)
        {
            int advance = enemy.Position.y - move.y;
            if (advance > 0) score += advance * 10;
        }

        // 勇者狙いボーナス：勇者からのチェビシェフ距離が近いほど高スコア。
        // 旧「中央寄りボーナス（最大3）」では端の駒が永遠に動かなかったため、
        // 勇者を中心にした包囲評価へ置き換える（最大32点で前進ボーナスと釣り合う規模）。
        if (cachedHeroPos.HasValue)
        {
            Vector2Int hp = cachedHeroPos.Value;
            int dist = Mathf.Max(Mathf.Abs(move.x - hp.x), Mathf.Abs(move.y - hp.y));
            score += Mathf.Max(0, 8 - dist) * 4;
        }
        else
        {
            // 勇者がリスポーン待機中などで盤面にいない場合は中央寄りボーナスにフォールバック
            int centerX = BoardGrid.Width / 2;
            score += (3 - Mathf.Abs(move.x - centerX));
        }

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
