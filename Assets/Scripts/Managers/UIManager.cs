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
    [SerializeField] private GameObject gamePanel; // 盤面やステータス表示など
    [SerializeField] private GameObject resultPanel;

    [Header("Result UI Objects")]
    // ユーザーが作成した `Win` と `Lose` のGameObjectそのものをアサインします
    [SerializeField] private GameObject winTextObject;
    [SerializeField] private GameObject loseTextObject;

    private void Start()
    {
        // 最初はタイトル画面を表示
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

        // ゲームの初期化処理を走らせる
        GameManager.Instance.StartGame();
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
