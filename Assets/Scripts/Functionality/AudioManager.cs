using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
internal class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource gameSoundSource;
    // [SerializeField] private AudioSource uiSource;

    [Header("Background")]
    [SerializeField] private AudioClip bgMusic;

    [Header("Game Sounds")]
    [SerializeField] private AudioClip ChipSound;
    [SerializeField] private AudioClip PlaceBetNow;
    [SerializeField] private AudioClip BetLocked;
    [SerializeField] private AudioClip Chips;
    [SerializeField] private AudioClip Timer;
    [SerializeField] private AudioClip UiButton;
    [SerializeField] private AudioClip BetPlaced;
    [SerializeField] private AudioClip RoundClosed;
    [SerializeField] private AudioClip WheelSpin;


    private bool isGameMuted = false;
    private bool isMusicMuted = false;

    private void Start()
    {
        PlayBackground();
    }



    internal void PlayBackground()
    {
        if (!bgMusic) return;

        bgMusicSource.clip = bgMusic;
        bgMusicSource.loop = true;
        if (!bgMusicSource.isPlaying)
            bgMusicSource.Play();
    }

    internal void StopBackground()
    {
        bgMusicSource.Stop();
    }

    internal void PlayChipSound()
    {
        PlayGame(ChipSound, false);
    }

    internal void PlayPlaceBetNow()
    {
        PlayGame(PlaceBetNow, false);
    }
    internal void PlayBetLocked()
    {
        PlayGame(BetLocked, false);
    }

    internal void PlayChips()
    {
        PlayGame(Chips, false);
    }
    internal void PlayTimer()
    {
        PlayGame(Timer, false);
    }
    internal void PlayUiButton()
    {
        PlayGame(UiButton, false);
    }
    internal void PlayBetPlaced()
    {
        PlayGame(BetPlaced, false);
    }
    internal void PlayRoundClosed()
    {
        PlayGame(RoundClosed, false);
    }
    internal void PlayWheelSpin()
    {
        PlayGame(WheelSpin, true);
    }

    private void PlayGame(AudioClip clip, bool loop)
    {
        if (!clip) return;

        gameSoundSource.Stop();
        gameSoundSource.clip = clip;
        gameSoundSource.loop = loop;
        gameSoundSource.Play();
    }

    internal void StopGameAudio()
    {
        gameSoundSource.Stop();
        gameSoundSource.loop = false;
    }


    internal void MuteBackground(bool mute) => bgMusicSource.mute = mute;
    internal void MuteGame(bool mute) => gameSoundSource.mute = mute;
    // internal void MuteUI(bool mute) => uiSource.mute = mute;
}
