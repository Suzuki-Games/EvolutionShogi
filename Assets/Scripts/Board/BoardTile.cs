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
    [SerializeField] private Color normalColor = Color.white;       // 通常の色
    [SerializeField] private Color highlightColor = Color.green;    // 移動可能なマスの色
    [SerializeField] private Color attackColor = Color.red;         // 敵がいるマスの色

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

    // デバッグ時用の描画補助
    private void OnMouseEnter()
    {
        // TODO: マウスホバー時の簡易表示
    }
}
