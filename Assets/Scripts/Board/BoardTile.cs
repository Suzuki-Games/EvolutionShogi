using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各マスのビジュアルと状態（ハイライトなど）を管理するスクリプト。
/// クリック判定用のColliderをアタッチして使用します。
/// </summary>
public class BoardTile : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.green;
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private Color shockwaveColor = new Color(1f, 0.6f, 0f); // 衝撃波（オレンジ）

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
    /// マスを通常状態に戻す
    /// </summary>
    public void ResetHighlight()
    {
        spriteRenderer.color = normalColor;
    }

    /// <summary>
    /// メリハリをつけるため、移動可能なマスや敵がいるマスを光らせる
    /// </summary>
    public void SetHighlight(bool isAttackable)
    {
        spriteRenderer.color = isAttackable ? attackColor : highlightColor;
    }

    /// <summary>
    /// 衝撃波の範囲をオレンジ色でハイライト
    /// </summary>
    public void SetShockwaveHighlight()
    {
        spriteRenderer.color = shockwaveColor;
    }

    // デバッグ時用の描画補助
    private void OnMouseEnter()
    {
        // TODO: マウスホバー時の簡易表示
    }
}
