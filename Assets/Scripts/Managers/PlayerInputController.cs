using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーのマウス入力（タップ入力）を受け取り、
/// 駒の選択や移動先マスの指定を制御します。
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    [SerializeField] private BoardGrid boardGrid;
    [SerializeField] private BoardView boardView;
    [SerializeField] private TurnManager turnManager;

    private Piece selectedPiece; // 現在選択中の駒
    private List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();

    void Update()
    {
        // 自分のターンでない場合は入力を受け付けない
        if (turnManager == null || turnManager.CurrentState != GameState.PlayerTurn)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        // マウス座標からワールドの2D座標へ変換
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        
        // Raycastでクリックしたオブジェクト（Collider2Dを持つもの）を取得
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            // まず盤面のタイルをクリックしたか判定
            BoardTile clickedTile = hit.collider.GetComponent<BoardTile>();
            if (clickedTile != null)
            {
                OnTileClicked(clickedTile);
                return;
            }

            // 次に駒を直接クリックしたか判定
            Piece clickedPiece = hit.collider.GetComponent<Piece>();
            if (clickedPiece != null)
            {
                OnPieceClicked(clickedPiece);
                return;
            }
        }
    }

    private void OnPieceClicked(Piece piece)
    {
        // 味方の駒であれば選択する
        if (!piece.IsEnemy)
        {
            SelectPiece(piece);
        }
        else if (selectedPiece != null)
        {
            // すでに味方の駒を選択中で、敵の駒をクリックした場合は、攻撃（移動）の判定に渡す
            TryMoveTo(piece.Position);
        }
    }

    private void OnTileClicked(BoardTile tile)
    {
        // すでに駒を選択済みの状態でタイル（空きマス）をクリックした場合、移動の判定に渡す
        if (selectedPiece != null)
        {
            TryMoveTo(tile.GridPosition);
        }
        else
        {
            // 駒が選択されていない場合はそのタイル上に味方駒があるか探して選択する
            Piece pieceOnTile = boardGrid.GetPieceAt(tile.GridPosition);
            if (pieceOnTile != null && !pieceOnTile.IsEnemy)
            {
                SelectPiece(pieceOnTile);
            }
        }
    }

    private void SelectPiece(Piece piece)
    {
        selectedPiece = piece;
        
        // 選択した駒の移動可能範囲を取得
        currentAvailableMoves = selectedPiece.GetAvailableMoves(boardGrid.GetGrid());
        
        // BoardViewにハイライトを依頼
        if (boardView != null)
        {
            boardView.HighlightMoves(currentAvailableMoves);
        }
        
        Debug.Log($"駒を選択しました: {piece.name} at {piece.Position}");
    }

    private void TryMoveTo(Vector2Int targetPos)
    {
        // クリックした座標が移動可能リストに含まれているか確認
        if (currentAvailableMoves.Contains(targetPos))
        {
            // 実際のロジック（配列やEXP処理）の更新
            boardGrid.MovePiece(selectedPiece, targetPos);
            
            // ビジュアル（位置）の更新
            Vector3 worldTargetPos = boardView.GetWorldPositionFromGrid(targetPos);
            selectedPiece.transform.position = worldTargetPos;
            
            // ハイライトを消去して選択解除
            boardView.ClearAllHighlights();
            selectedPiece = null;
            currentAvailableMoves.Clear();

            // ターンを終了し、敵のターンへ移行する
            turnManager.EndPlayerTurn();
        }
        else
        {
            // 移動できないマスを選んだ場合は選択をキャンセルする
            boardView.ClearAllHighlights();
            selectedPiece = null;
            currentAvailableMoves.Clear();
            Debug.Log("移動可能範囲外です。選択を解除しました。");
        }
    }
}
