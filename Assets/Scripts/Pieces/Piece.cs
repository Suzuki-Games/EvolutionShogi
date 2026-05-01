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
        
        UpdateVisuals();
    }

    /// <summary>
    /// 駒が取られた時の処理（共通基盤）。
    /// 即時消滅ではなく拡大＋フェードアウト演出を挟むことで撃破の手応えを出す。
    /// 盤面データ（BoardGrid）からはこの呼び出し前に除去済み前提なので、
    /// 演出中の見た目だけ残るが論理的な干渉はない。
    /// </summary>
    public virtual void OnTaken()
    {
        // クリック判定を無効化して演出中の誤操作を防ぐ
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // GameObjectが既に非アクティブの場合はコルーチンが走らないので即時非表示
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        StartCoroutine(DefeatAnimationCoroutine());
    }

    /// <summary>
    /// 撃破演出：白フラッシュ → 拡大しながら高速回転＆フェードアウトして消える。
    /// 控えめだと「取った！」の手応えが薄いため、瞬間的な白フラッシュで視線を引き付けてから
    /// 大きく弾けるように拡大させ、撃破の爽快感を出している。
    /// </summary>
    private IEnumerator DefeatAnimationCoroutine()
    {
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        Color startColor = sr != null ? sr.color : Color.white;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * 2.4f;   // 1.6 → 2.4 で爆発感アップ
        float duration = 0.55f;                  // 0.35 → 0.55 で見える時間を確保
        float elapsed = 0f;

        // ① 開幕フラッシュ：色を一瞬白に飛ばす（視線をキャッチ）
        if (sr != null) sr.color = Color.white;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // スケール：イージングで一気に膨らんでから減速
            float scaleT = 1f - (1f - t) * (1f - t); // ease-out quad
            transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);

            // 回転：540deg/秒で勢いを出す
            transform.Rotate(0f, 0f, 540f * Time.deltaTime);

            if (sr != null)
            {
                // 色：白フラッシュ → 元色にすばやく戻し、その間にアルファをフェード
                float colorT = Mathf.Clamp01(t / 0.3f);            // 最初の30%でフラッシュ解除
                Color blended = Color.Lerp(Color.white, startColor, colorT);
                blended.a = Mathf.Lerp(startColor.a, 0f, t);       // 全体でアルファをフェード
                sr.color = blended;
            }
            yield return null;
        }

        // 状態を元に戻してから非表示化（プールや再利用の安全性確保）
        transform.localScale = startScale;
        transform.rotation = Quaternion.identity;
        if (sr != null) sr.color = startColor;
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

            // spriteMode: Multiple の場合、Load<Sprite>はnullを返すのでLoadAllで取得
            if (pieceSprite == null)
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(spritePath);
                if (sprites != null && sprites.Length > 0)
                {
                    pieceSprite = sprites[0];
                }
            }

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
