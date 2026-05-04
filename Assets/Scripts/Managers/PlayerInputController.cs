using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーのマウス入力（タップ入力）を受け取り、
/// 駒の選択や移動先マスの指定、持ち駒の打ち込みを制御します。
/// </summary>
public class PlayerInputController : MonoBehaviour
{
    [SerializeField] private BoardGrid boardGrid;
    [SerializeField] private BoardView boardView;
    [SerializeField] private TurnManager turnManager;
    [SerializeField] private UIManager uiManager;

    [Header("Prefabs for Hand Drops")]
    [SerializeField] private AllyPiece allyPawnPrefab;
    [SerializeField] private AllyPiece allyGoldPrefab;
    [SerializeField] private AllyPiece allySilverPrefab;
    [SerializeField] private AllyPiece allyRookPrefab;
    [SerializeField] private AllyPiece allyBishopPrefab;

    private Piece selectedPiece;
    private List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();

    // 持ち駒の打ち込みモード
    private bool isDropMode = false;
    private PieceType dropPieceType;

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (turnManager == null || turnManager.CurrentState != GameState.PlayerTurn)
            return;

        // 進化先選択モーダル表示中は盤面クリックを受け付けない（裏側で別の駒を動かされないように）
        if (turnManager.IsHeroEvolutionPending())
            return;

        // 旧 Input Manager の Input.GetMouseButtonDown / Input.mousePosition は Unity 6 で
        // 非推奨警告が出るため、新 Input System の Mouse.current 経由に置き換えている。
        // Mouse.current が null になり得る環境（マウス未接続）に備えて毎回ガードする。
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            HandleClick(mouse.position.ReadValue());
        }

        // 右クリックで選択解除
        if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelSelection();
        }
    }

    private void HandleClick(Vector2 screenPos)
    {
        Vector2 mousePos = mainCamera.ScreenToWorldPoint(screenPos);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null)
        {
            BoardTile clickedTile = hit.collider.GetComponent<BoardTile>();
            if (clickedTile != null)
            {
                OnTileClicked(clickedTile);
                return;
            }

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
        if (isDropMode)
        {
            // 打ち込みモード中に駒がある場所をクリック→キャンセル
            CancelSelection();
            return;
        }

        if (!piece.IsEnemy)
        {
            SelectPiece(piece);
        }
        else if (selectedPiece != null)
        {
            TryMoveTo(piece.Position);
        }
    }

    private void OnTileClicked(BoardTile tile)
    {
        if (isDropMode)
        {
            TryDropPiece(tile.GridPosition);
            return;
        }

        if (selectedPiece != null)
        {
            TryMoveTo(tile.GridPosition);
        }
        else
        {
            Piece pieceOnTile = boardGrid.GetPieceAt(tile.GridPosition);
            if (pieceOnTile != null && !pieceOnTile.IsEnemy)
            {
                SelectPiece(pieceOnTile);
            }
        }
    }

    private void SelectPiece(Piece piece)
    {
        isDropMode = false;
        selectedPiece = piece;
        currentAvailableMoves = selectedPiece.GetAvailableMoves(boardGrid.GetGrid());

        if (boardView != null)
        {
            boardView.HighlightMoves(currentAvailableMoves);
        }

        Debug.Log($"駒を選択しました: {piece.name} at {piece.Position}");
    }

    /// <summary>
    /// UIManagerから呼ばれる：持ち駒を選択して打ち込みモードに入る
    /// </summary>
    public void EnterDropMode(PieceType type)
    {
        selectedPiece = null;
        isDropMode = true;
        dropPieceType = type;

        // 全空きマスをハイライト（二歩チェック付き）
        currentAvailableMoves = GetValidDropPositions(type);
        if (boardView != null)
        {
            boardView.HighlightMoves(currentAvailableMoves);
        }

        Debug.Log($"持ち駒モード: {type} を打つ場所を選んでください");
    }

    /// <summary>
    /// 持ち駒を打てる位置を取得する
    /// </summary>
    private List<Vector2Int> GetValidDropPositions(PieceType type)
    {
        List<Vector2Int> positions = new List<Vector2Int>();

        for (int x = 0; x < BoardGrid.Width; x++)
        {
            // 歩の二歩チェック: 同じ列に味方の歩がいたらその列には打てない
            if (type == PieceType.Pawn)
            {
                bool hasPawnInColumn = false;
                for (int checkY = 0; checkY < BoardGrid.Height; checkY++)
                {
                    Piece p = boardGrid.GetPieceAt(new Vector2Int(x, checkY));
                    if (p != null && !p.IsEnemy && p.Type == PieceType.Pawn && !(p is HeroPiece))
                    {
                        hasPawnInColumn = true;
                        break;
                    }
                }
                if (hasPawnInColumn) continue;
            }

            for (int y = 0; y < BoardGrid.Height; y++)
            {
                // 歩は最奥段（y=6）には打てない（動けなくなるため）
                if (type == PieceType.Pawn && y == BoardGrid.Height - 1) continue;

                Vector2Int pos = new Vector2Int(x, y);
                if (boardGrid.GetPieceAt(pos) == null)
                {
                    positions.Add(pos);
                }
            }
        }

        return positions;
    }

    private void TryDropPiece(Vector2Int targetPos)
    {
        if (!currentAvailableMoves.Contains(targetPos))
        {
            CancelSelection();
            return;
        }

        // HandManagerから持ち駒を消費
        if (HandManager.Instance == null || !HandManager.Instance.UseFromHand(dropPieceType, false))
        {
            CancelSelection();
            return;
        }

        // プレハブから味方駒を生成して盤面に配置
        AllyPiece prefab = GetAllyPrefabForType(dropPieceType);
        if (prefab != null)
        {
            AllyPiece newPiece = Instantiate(prefab);
            newPiece.Initialize(dropPieceType, false, targetPos);
            boardGrid.PlacePiece(newPiece, targetPos);

            if (boardView != null)
            {
                newPiece.transform.position = boardView.GetWorldPositionFromGrid(targetPos);
                newPiece.transform.SetParent(boardView.transform);
            }

            // SE再生
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMove();
        }

        // 選択解除してターン終了
        boardView.ClearAllHighlights();
        isDropMode = false;
        selectedPiece = null;
        currentAvailableMoves.Clear();

        if (uiManager != null) uiManager.RefreshHeroHUD();
        turnManager.EndPlayerTurn();
    }

    private AllyPiece GetAllyPrefabForType(PieceType type)
    {
        AllyPiece prefab = null;
        switch (type)
        {
            case PieceType.Pawn: prefab = allyPawnPrefab; break;
            case PieceType.Gold: prefab = allyGoldPrefab; break;
            case PieceType.Silver: prefab = allySilverPrefab; break;
            case PieceType.Rook: prefab = allyRookPrefab; break;
            case PieceType.Bishop: prefab = allyBishopPrefab; break;
        }
        // プレハブが未アサインの場合、allyPawnPrefabを代用（Initialize()で正しいTypeが設定される）
        return prefab != null ? prefab : allyPawnPrefab;
    }

    private void TryMoveTo(Vector2Int targetPos)
    {
        if (currentAvailableMoves.Contains(targetPos))
        {
            Piece movingPiece = selectedPiece;

            boardGrid.MovePiece(movingPiece, targetPos);

            if (uiManager != null) uiManager.RefreshHeroHUD();

            boardView.ClearAllHighlights();
            selectedPiece = null;
            currentAvailableMoves.Clear();

            Vector3 worldTargetPos = boardView.GetWorldPositionFromGrid(targetPos);
            if (PieceMover.Instance != null)
            {
                PieceMover.Instance.AnimateMove(movingPiece.transform, worldTargetPos, () =>
                {
                    StartCoroutine(EndPlayerTurnAfterEvolutionResolved());
                });
            }
            else
            {
                movingPiece.transform.position = worldTargetPos;
                StartCoroutine(EndPlayerTurnAfterEvolutionResolved());
            }
        }
        else
        {
            CancelSelection();
        }
    }

    /// <summary>
    /// 進化選択モーダルが開いていればプレイヤーの選択完了を待ってからターンを終える。
    /// 多段進化（例：歩→金→飛車を1手で達成）した場合も連続するモーダル全てが閉じるまで待機する。
    /// </summary>
    private IEnumerator EndPlayerTurnAfterEvolutionResolved()
    {
        while (turnManager.IsHeroEvolutionPending())
        {
            yield return null;
        }
        turnManager.EndPlayerTurn();
    }

    private void CancelSelection()
    {
        boardView.ClearAllHighlights();
        selectedPiece = null;
        isDropMode = false;
        currentAvailableMoves.Clear();
    }
}
