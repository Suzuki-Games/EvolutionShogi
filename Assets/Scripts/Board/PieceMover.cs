using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 駒の移動アニメーションを処理するユーティリティ。
/// Coroutineベースの滑らかなLerp移動を提供します。
/// </summary>
public class PieceMover : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;

    public static PieceMover Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// 駒を目標位置へ滑らかに移動させます。完了時にonCompleteが呼ばれます。
    /// </summary>
    public void AnimateMove(Transform piece, Vector3 targetPos, Action onComplete = null)
    {
        StartCoroutine(MoveCoroutine(piece, targetPos, onComplete));
    }

    private IEnumerator MoveCoroutine(Transform piece, Vector3 targetPos, Action onComplete)
    {
        Vector3 startPos = piece.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
            piece.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        piece.position = targetPos;
        onComplete?.Invoke();
    }
}
