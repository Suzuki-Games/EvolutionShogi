using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// これまでに作成したクラス（BoardGrid, HeroPiece, EnemyPiece など）の
/// ロジックが正しく動くかをUnityのエディタ上で（画面なしで）テストするためのスクリプトです。
/// </summary>
public class LogicTester : MonoBehaviour
{
    [Header("テスト用コンポーネント")]
    public BoardGrid boardGrid;
    public TurnManager turnManager;
    public EnemyAI enemyAI;

    // テスト用の駒プレハブなどをアサインするか、動的に生成してアタッチします
    void Start()
    {
        Debug.Log("<color=cyan>--- LogicTester: ロジックテストを開始します ---</color>");

        // 1. 各マネージャーの初期化確認
        if (boardGrid == null) boardGrid = gameObject.AddComponent<BoardGrid>();
        if (turnManager == null) turnManager = gameObject.AddComponent<TurnManager>();
        if (enemyAI == null) enemyAI = gameObject.AddComponent<EnemyAI>();

        // テスト環境用の依存性注入
        enemyAI.boardGrid = boardGrid;

        // 2. 駒の生成（今回はテスト用に、GameObjectをその場で空で作ってスクリプトを付与します）
        HeroPiece hero = new GameObject("Hero_Test").AddComponent<HeroPiece>();
        hero.Initialize(PieceType.Pawn, false, new Vector2Int(2, 0)); // 主人公は中央の一番手間(2, 0)
        
        EnemyPiece enemy = new GameObject("Enemy_Pawn_Test").AddComponent<EnemyPiece>();
        enemy.Initialize(PieceType.Pawn, true, new Vector2Int(2, 1)); // 敵は主人公のすぐ目の前(2, 1)

        EnemyPiece enemyBoss = new GameObject("Enemy_Rook_Test").AddComponent<EnemyPiece>();
        enemyBoss.Initialize(PieceType.Rook, true, new Vector2Int(2, 4)); // 奥に強い敵(2, 4)

        // 3. 盤面に配置
        boardGrid.PlacePiece(hero, hero.Position);
        boardGrid.PlacePiece(enemy, enemy.Position);
        boardGrid.PlacePiece(enemyBoss, enemyBoss.Position);

        turnManager.RegisterHero(hero);
        turnManager.RegisterEnemy(enemy);
        turnManager.RegisterEnemy(enemyBoss);

        Debug.Log($"初期配置完了: Hero({hero.Position}), Enemy1({enemy.Position}), Enemy2({enemyBoss.Position})");

        // 4. テスト実行ルーチンを開始
        StartCoroutine(RunTests(hero, enemy, enemyBoss));
    }

    private IEnumerator RunTests(HeroPiece hero, EnemyPiece enemy, EnemyPiece enemyBoss)
    {
        yield return new WaitForSeconds(1f);

        Debug.Log("<color=yellow>テスト1: 主人公の移動範囲（Pawn状態）の取得</color>");
        var moves = hero.GetAvailableMoves(boardGrid.GetGrid());
        foreach (var pos in moves)
        {
            Debug.Log($"-> 移動可能座標: {pos}");
        }
        // ここで (2, 1) つまり敵のいる場所が候補に出るはずです。

        yield return new WaitForSeconds(1f);

        Debug.Log("<color=yellow>テスト2: 主人公が敵(Pawn)を倒して前進</color>");
        // 実際にBoardGridを使って移動させます
        boardGrid.MovePiece(hero, new Vector2Int(2, 1));
        
        // 敵のOnTakenが呼ばれ、gameObjectが非アクティブになるはず
        Debug.Log($"敵1のアクティブ状態: {enemy.gameObject.activeSelf} (Falseなら成功)");
        Debug.Log($"主人公の現在EXP: {hero.CurrentExp} (Pawnを倒したので1になるはず)");

        yield return new WaitForSeconds(1f);

        Debug.Log("<color=yellow>テスト3: 空きマスへ前進して基本EXPを獲得</color>");
        boardGrid.MovePiece(hero, new Vector2Int(2, 2));
        Debug.Log($"主人公の現在EXP: {hero.CurrentExp} (1歩前進したので合計2になるはず)");
        Debug.Log($"主人公のクラス: {hero.Type} (EXP2になったのでSilverに進化するはず)");

        yield return new WaitForSeconds(1f);

        Debug.Log("<color=yellow>テスト4: 敵陣深く(y=4)にいる敵(Rook)を倒してボーナスEXPを獲得</color>");
        // いきなりワープさせて倒してみる
        boardGrid.MovePiece(hero, new Vector2Int(2, 4));
        // Rook自身のExp=5、奥マス(y=4)到達ボーナス=2、合計7獲得するはず。 現在のEXP2 + 7 = 9 になる想定。
        Debug.Log($"敵ボスのアクティブ状態: {enemyBoss.gameObject.activeSelf} (Falseなら成功)");
        Debug.Log($"主人公の現在EXP: {hero.CurrentExp} (合計9になるはず)");
        Debug.Log($"主人公のクラス: {hero.Type} (EXP9なのでRookに進化するはず)");

        yield return new WaitForSeconds(1f);

        Debug.Log("<color=yellow>テスト5: 進化後（飛車）の移動範囲取得テスト</color>");
        var rookMoves = hero.GetAvailableMoves(boardGrid.GetGrid());
        Debug.Log($"飛車(Rook)の移動候補数: {rookMoves.Count}");
        foreach (var pos in rookMoves)
        {
            Debug.Log($"-> 飛車の移動可能座標: {pos}");
        }

        yield return new WaitForSeconds(1f);

        Debug.Log("<color=magenta>テスト6: 敵AIの自動行動テスト（取れるなら取る）</color>");
        // あえて主人公の目の前(2, 3)に新しい敵(Pawn)を配置してプレイヤーの駒を取らせてみる
        EnemyPiece assassin = new GameObject("Enemy_Assassin_Test").AddComponent<EnemyPiece>();
        assassin.Initialize(PieceType.Pawn, true, new Vector2Int(2, 5)); // 盤面外に一旦置く
        boardGrid.PlacePiece(assassin, new Vector2Int(2, 3)); // 盤面に差し込む
        turnManager.RegisterEnemy(assassin);
        
        List<EnemyPiece> testEnemies = new List<EnemyPiece> { enemy, enemyBoss, assassin };
        
        bool isAiDone = false;
        // EnemyAIを叩いて、assassin が 主人公(2, 4にワープしたRook) を狙って攻撃するか確認
        StartCoroutine(enemyAI.ExecuteTurn(testEnemies, () => {
             isAiDone = true; 
        }));

        // AIの行動完了を待つ
        while(!isAiDone) yield return null;
        
        Debug.Log($"[結果] 主人公のアクティブ状態: {hero.gameObject.activeSelf} (AIに取られてFalseなら成功)");
        Debug.Log($"[結果] 主人公の死亡状態(IsDead): {hero.IsDead}");

        // TODO: ここから3ターン経過させてリスポーンするかのテストも追加可能。

        yield return new WaitForSeconds(1f);

        Debug.Log("<color=cyan>--- LogicTester: テスト完了 ---</color>");
    }
}
