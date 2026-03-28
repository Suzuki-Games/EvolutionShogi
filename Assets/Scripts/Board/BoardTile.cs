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

    public void ResetHighlight()
    {
        spriteRenderer.color = normalColor;
    }

    public void SetHighlight(bool isAttackable)
    {
        spriteRenderer.color = isAttackable ? attackColor : highlightColor;
    }
}
