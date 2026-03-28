using UnityEngine;

/// <summary>
/// ゲーム内の効果音を管理するシングルトン。
/// AudioClipをInspectorでアサインして使用します。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("SE Clips (assign in Inspector)")]
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip captureClip;
    [SerializeField] private AudioClip evolveClip;
    [SerializeField] private AudioClip gameOverClip;
    [SerializeField] private AudioClip gameClearClip;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayMove()
    {
        PlayClip(moveClip);
    }

    public void PlayCapture()
    {
        PlayClip(captureClip);
    }

    public void PlayEvolve()
    {
        PlayClip(evolveClip);
    }

    public void PlayGameOver()
    {
        PlayClip(gameOverClip);
    }

    public void PlayGameClear()
    {
        PlayClip(gameClearClip);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
