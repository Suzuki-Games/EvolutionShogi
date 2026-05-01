using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各マスのビジュアルと状態（ハイライトなど）を管理するスクリプト。
/// クリック判定用のColliderをアタッチして使用します。
///
/// ハイライトには「選択中の移動先」と「敵の攻撃範囲（脅威）」の2系統があり、
/// 移動先表示が優先される（プレイヤーが駒を選んでいる時の視認性確保）。
/// </summary>
public class BoardTile : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.green;
    [SerializeField] private Color attackColor = Color.red;

    // 敵攻撃範囲の警告色。プレハブ側で旧値が serialize されていた問題があったため
    // SerializeField を外してコード固定にし、再生のたびに必ずこの色になるようにしている。
    // 「敵駒を取れる時の赤」と同等の鮮やかさ（純赤）にして視認性を最大化。
    private readonly Color threatColor = Color.red;

    private enum SelectionState { None, Move, Attack }
    private SelectionState selectionState = SelectionState.None;
    private bool isThreatened = false;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Setup(Vector2Int pos)
    {
        GridPosition = pos;
        ResetHighlight();
    }

    /// <summary>
    /// 移動先ハイライトをクリア（脅威表示は維持）。
    /// </summary>
    public void ResetHighlight()
    {
        selectionState = SelectionState.None;
        ApplyColor();
    }

    /// <summary>
    /// 移動先ハイライトを設定（攻撃可能なら赤、空きマスなら緑）。
    /// </summary>
    public void SetHighlight(bool isAttackable)
    {
        selectionState = isAttackable ? SelectionState.Attack : SelectionState.Move;
        ApplyColor();
    }

    /// <summary>
    /// 敵の攻撃範囲かどうかを設定。選択ハイライトが優先される。
    /// </summary>
    public void SetThreat(bool threatened)
    {
        isThreatened = threatened;
        ApplyColor();
    }

    /// <summary>
    /// 全状態を考慮して最終色を決定。
    /// 優先度: 攻撃ハイライト > 移動ハイライト > 脅威表示 > 通常色
    /// </summary>
    private void ApplyColor()
    {
        if (selectionState == SelectionState.Attack)
            spriteRenderer.color = attackColor;
        else if (selectionState == SelectionState.Move)
            spriteRenderer.color = highlightColor;
        else if (isThreatened)
            spriteRenderer.color = threatColor;
        else
            spriteRenderer.color = normalColor;
    }
}
