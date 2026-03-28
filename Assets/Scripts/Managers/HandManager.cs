using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持ち駒（取った駒を再利用するシステム）を管理します。
/// プレイヤーと敵それぞれの手持ち駒リストを保持します。
/// </summary>
public class HandManager : MonoBehaviour
{
    public static HandManager Instance { get; private set; }

    // プレイヤーの持ち駒
    private List<PieceType> playerHand = new List<PieceType>();
    // 敵の持ち駒
    private List<PieceType> enemyHand = new List<PieceType>();

    public IReadOnlyList<PieceType> PlayerHand => playerHand;
    public IReadOnlyList<PieceType> EnemyHand => enemyHand;

    public event System.Action OnHandChanged;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// 取った駒を手持ちに追加する。
    /// 勇者(Hero)を取った場合は歩として手持ちに入る。
    /// 王は持ち駒にできない。
    /// </summary>
    public void AddToHand(PieceType type, bool addToEnemy)
    {
        // 王は持ち駒にできない
        if (type == PieceType.King) return;

        // 勇者は歩に戻して持ち駒にする
        PieceType handType = (type == PieceType.Hero) ? PieceType.Pawn : type;

        if (addToEnemy)
            enemyHand.Add(handType);
        else
            playerHand.Add(handType);

        Debug.Log($"[HandManager] {(addToEnemy ? "敵" : "味方")}の持ち駒に{handType}を追加。");
        OnHandChanged?.Invoke();
    }

    /// <summary>
    /// 持ち駒を使用する（リストから除去）
    /// </summary>
    public bool UseFromHand(PieceType type, bool fromEnemy)
    {
        List<PieceType> hand = fromEnemy ? enemyHand : playerHand;
        if (hand.Remove(type))
        {
            Debug.Log($"[HandManager] {(fromEnemy ? "敵" : "味方")}の持ち駒から{type}を使用。");
            OnHandChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 持ち駒を全てクリアする（ゲームリセット用）
    /// </summary>
    public void ClearAll()
    {
        playerHand.Clear();
        enemyHand.Clear();
        OnHandChanged?.Invoke();
    }
}
