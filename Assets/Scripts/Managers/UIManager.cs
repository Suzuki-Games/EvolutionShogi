using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshProを使うために追加

/// <summary>
/// ゲーム内のUI状態（タイトル、ゲーム中、リザルト）を管理します。
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject resultPanel;

    [Header("Result UI Objects")]
    [SerializeField] private GameObject winTextObject;
    [SerializeField] private GameObject loseTextObject;

    [Header("Game HUD")]
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI formText;
    [SerializeField] private TextMeshProUGUI turnText;

    private HeroPiece trackedHero;
    private int turnCount = 0;

    private void Start()
    {
        ShowTitleScreen();
    }

    public void ShowTitleScreen()
    {
        titlePanel.SetActive(true);
        gamePanel.SetActive(false);
        resultPanel.SetActive(false);
    }

    public void OnClickGameStart()
    {
        titlePanel.SetActive(false);
        gamePanel.SetActive(true);
        resultPanel.SetActive(false);
        turnCount = 0;

        GameManager.Instance.StartGame();
    }

    /// <summary>
    /// GameManagerから勇者の参照を受け取り、HUD追跡を開始する
    /// </summary>
    public void TrackHero(HeroPiece hero)
    {
        trackedHero = hero;
        UpdateHeroHUD();
    }

    /// <summary>
    /// ターン開始時に呼ばれる（TurnManagerから）
    /// </summary>
    public void OnNewPlayerTurn()
    {
        turnCount++;
        UpdateHeroHUD();
    }

    /// <summary>
    /// EXP/進化表示だけを更新する（ターンカウントは増やさない）
    /// </summary>
    public void RefreshHeroHUD()
    {
        UpdateHeroHUD();
    }

    private void UpdateHeroHUD()
    {
        if (trackedHero == null) return;

        if (expText != null)
        {
            int nextThreshold = GetNextExpThreshold(trackedHero.CurrentExp);
            if (nextThreshold > 0)
                expText.text = $"EXP: {trackedHero.CurrentExp} / {nextThreshold}";
            else
                expText.text = $"EXP: {trackedHero.CurrentExp} (MAX)";
        }

        if (formText != null)
        {
            string formName = GetFormDisplayName(trackedHero.Type);
            string nextForm = GetNextFormName(trackedHero.Type);
            if (nextForm != null)
                formText.text = $"{formName}  >>  {nextForm}";
            else
                formText.text = $"{formName} (Final)";
        }

        if (turnText != null)
        {
            turnText.text = $"Turn {turnCount}";
        }
    }

    private int GetNextExpThreshold(int currentExp)
    {
        if (currentExp < 2) return 2;
        if (currentExp < 5) return 5;
        if (currentExp < 10) return 10;
        return -1; // MAX
    }

    private string GetFormDisplayName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return "Pawn";
            case PieceType.Silver: return "Silver";
            case PieceType.Rook: return "Rook";
            case PieceType.Hero: return "Hero";
            default: return type.ToString();
        }
    }

    private string GetNextFormName(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return "Silver";
            case PieceType.Silver: return "Rook";
            case PieceType.Rook: return "Hero";
            default: return null;
        }
    }

    public void ShowGameClear()
    {
        gamePanel.SetActive(false);
        resultPanel.SetActive(true);

        // Winの文字だけを表示する
        if (winTextObject != null) winTextObject.SetActive(true);
        if (loseTextObject != null) loseTextObject.SetActive(false);
    }

    public void ShowGameOver()
    {
        gamePanel.SetActive(false);
        resultPanel.SetActive(true);

        // Loseの文字だけを表示する
        if (winTextObject != null) winTextObject.SetActive(false);
        if (loseTextObject != null) loseTextObject.SetActive(true);
    }

    public void OnClickRetry()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
