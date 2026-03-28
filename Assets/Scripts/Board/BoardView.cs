using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ロジックとしての BoardGrid と連携し、Unityシーン上での盤面生成と描画を行います。
/// </summary>
public class BoardView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardGrid boardGrid;
    
    [Header("Prefabs")]
    [Tooltip("マス目のプレハブ (SpriteRenderer と Collider2D がついたもの)")]
    [SerializeField] private BoardTile tilePrefab;
    
    [Header("Settings")]
    [Tooltip("マス同士の隙間やサイズ調整用")]
    [SerializeField] private float tileSize = 1.1f;

    // 生成したマスのキャッシュ
    private BoardTile[,] tiles = new BoardTile[BoardGrid.Width, BoardGrid.Height];

    private void Start()
    {
        // BoardViewの位置を原点にリセットして盤面を確実に中央に配置
        transform.position = Vector3.zero;
        GenerateBoardVisuals();
    }

    /// <summary>
    /// 5x5の盤面を3D(または2D)空間上に展開して生成します。
    /// </summary>
    private void GenerateBoardVisuals()
    {
        // 5x5の中心を(0,0,0)にするためのオフセット計算
        float offsetX = (BoardGrid.Width - 1) * tileSize / 2f;
        float offsetY = (BoardGrid.Height - 1) * tileSize / 2f;

        for (int x = 0; x < BoardGrid.Width; x++)
        {
            for (int y = 0; y < BoardGrid.Height; y++)
            {
                // 各マスのワールド座標を決定
                Vector3 worldPos = new Vector3(x * tileSize - offsetX, y * tileSize - offsetY, 0);

                // プレハブを生成して配置
                BoardTile newTile = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                newTile.gameObject.name = $"Tile_{x}_{y}";
                newTile.Setup(new Vector2Int(x, y));
                
                tiles[x, y] = newTile;
            }
        }
    }

    /// <summary>
    /// すべてのハイライトをリセットする
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var tile in tiles)
        {
            if (tile != null)
            {
                tile.ResetHighlight();
            }
        }
    }

    /// <summary>
    /// 指定された座標リストにハイライトを適用する
    /// </summary>
    public void HighlightMoves(List<Vector2Int> moves)
    {
        ClearAllHighlights();

        foreach (var move in moves)
        {
            if (BoardGrid.IsInsideBoard(move))
            {
                // 移動先に敵駒がいるかどうか等の判定をBoardGridから貰う
                Piece targetPiece = boardGrid.GetPieceAt(move);
                bool isAttack = (targetPiece != null && targetPiece.IsEnemy);

                tiles[move.x, move.y].SetHighlight(isAttack);
            }
        }
    }

    /// <summary>
    /// 衝撃波の範囲をオレンジ色でハイライトし、一定時間後に自動クリアする
    /// </summary>
    public void ShowShockwaveEffect(List<Vector2Int> positions, float duration = 1.0f)
    {
        foreach (var pos in positions)
        {
            if (BoardGrid.IsInsideBoard(pos))
            {
                tiles[pos.x, pos.y].SetShockwaveHighlight();
            }
        }
        StartCoroutine(ClearShockwaveAfterDelay(positions, duration));
    }

    private IEnumerator ClearShockwaveAfterDelay(List<Vector2Int> positions, float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var pos in positions)
        {
            if (BoardGrid.IsInsideBoard(pos))
            {
                tiles[pos.x, pos.y].ResetHighlight();
            }
        }
    }

    /// <summary>
    /// グリッド座標を実際のワールド座標に変換して返します（駒を動かす先として使用）
    /// </summary>
    public Vector3 GetWorldPositionFromGrid(Vector2Int gridPos)
    {
        if (BoardGrid.IsInsideBoard(gridPos) && tiles[gridPos.x, gridPos.y] != null)
        {
            return tiles[gridPos.x, gridPos.y].transform.position;
        }
        return Vector3.zero;
    }
}
