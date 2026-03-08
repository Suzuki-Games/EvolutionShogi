using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    }

    /// <summary>
    /// 駒が取られた時の処理（共通基盤）。
    /// </summary>
    public virtual void OnTaken()
    {
        // デフォルトではオブジェクトを破棄または非表示にする
        gameObject.SetActive(false);
    }
}
