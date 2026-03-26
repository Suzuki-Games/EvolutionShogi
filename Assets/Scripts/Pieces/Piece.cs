using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 駒の種類を定義します。
/// 進化将棋では主に主人公のジョブチェンジや敵駒の種別判定に使用します。
/// </summary>
public enum PieceType
{
    Pawn,   // 歩
    Silver, // 銀
    Rook,   // 飛車
    Hero,   // 勇者
    King,   // 王将
    Gold,   // 金
    Bishop  // 角
}

/// <summary>
/// すべての駒の基本となる抽象クラス。
/// MonoBehaviourを継承し、Unity上のオブジェクトとして振る舞います。
/// </summary>
public abstract class Piece : MonoBehaviour
{
    [Header("Piece Status")]
    [Tooltip("駒の種類")]
    public PieceType Type;

    [Tooltip("敵駒かどうか")]
    public bool IsEnemy;

    [Tooltip("現在の盤面座標")]
    public Vector2Int Position;

    [Header("Reward Status")]
    [Tooltip("この駒を取った時に得られる基本経験値")]
    public int ExpValue = 1;

    /// <summary>
    /// その駒が現在移動可能な座標リストを返す抽象メソッド。
    /// サブクラス（各駒）で独自の移動ルールをオーバーライドして実装します。
    /// </summary>
    /// <param name="board">現在の盤面全体の参照</param>
    /// <returns>移動可能な座標のリスト</returns>
    public abstract List<Vector2Int> GetAvailableMoves(Piece[,] board);

    /// <summary>
    /// 座標の初期化を行います。
    /// </summary>
    public virtual void Initialize(PieceType type, bool isEnemy, Vector2Int startPos)
    {
        Type = type;
        IsEnemy = isEnemy;
        Position = startPos;
        
        UpdateVisuals();
    }

    /// <summary>
    /// 駒が取られた時の処理（共通基盤）。
    /// </summary>
    public virtual void OnTaken()
    {
        // デフォルトではオブジェクトを破棄または非表示にする
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 見た目の更新（色や画像）を行います。
    /// </summary>
    public virtual void UpdateVisuals()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            // 味方は青系、敵は赤系に色付け
            sr.color = IsEnemy ? new Color(0.8f, 0.4f, 0.4f) : new Color(0.4f, 0.6f, 1f);

            // 駒種に対応した画像をResourcesから読み込んで適用
            string spritePath = "PieceImages/" + GetSpriteName();
            Sprite pieceSprite = Resources.Load<Sprite>(spritePath);
            Debug.Log($"[Piece] Loading: {spritePath} → {(pieceSprite != null ? "成功" : "失敗(null)")}");
            if (pieceSprite != null)
            {
                sr.sprite = pieceSprite;
                sr.sortingOrder = 1; // 盤面タイルより前面に表示
            }
        }
    }

    /// <summary>
    /// 駒の種類に対応するスプライトファイル名を返します。
    /// </summary>
    protected virtual string GetSpriteName()
    {
        switch (Type)
        {
            case PieceType.Pawn:   return "Pawn_歩";
            case PieceType.Silver: return "Silver_銀";
            case PieceType.Rook:   return "Rook_飛";
            case PieceType.Hero:   return "Hero_勇";
            case PieceType.King:   return "King_王";
            case PieceType.Gold:   return "Gold_金";
            case PieceType.Bishop: return "Bishop_角";
            default: return "";
        }
    }
}
