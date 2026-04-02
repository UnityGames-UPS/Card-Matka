using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


internal class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource gameSoundSource;
    [SerializeField] private AudioSource cardSoundSource;
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
    [SerializeField] private AudioClip Bonus;

    [Header("Card Sounds")]
    [SerializeField] private AudioClip KingSpade;
    [SerializeField] private AudioClip KingHeart;
    [SerializeField] private AudioClip KingClub;
    [SerializeField] private AudioClip KingDiamond;

    [SerializeField] private AudioClip QueenSpade;
    [SerializeField] private AudioClip QueenHeart;
    [SerializeField] private AudioClip QueenClub;
    [SerializeField] private AudioClip QueenDiamond;

    [SerializeField] private AudioClip JackSpade;
    [SerializeField] private AudioClip JackHeart;
    [SerializeField] private AudioClip JackClub;
    [SerializeField] private AudioClip JackDiamond;

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
    internal void PlayBonus()
    {
        PlayGame(WheelSpin, false);
    }

    internal void PlayKingSpade()
    {
        PlayCard(KingSpade, false);
    }
    internal void PlayKingHeart()
    {
        PlayCard(KingHeart, false);
    }
    internal void PlayKingClub()
    {
        PlayCard(KingClub, false);
    }
    internal void PlayKingDiamond()
    {
        PlayCard(KingDiamond, false);
    }

    internal void PlayQueenSpade()
    {
        PlayCard(QueenSpade, false);
    }
    internal void PlayQueenHeart()
    {
        PlayCard(QueenHeart, false);
    }
    internal void PlayQueenClub()
    {
        PlayCard(QueenClub, false);
    }
    internal void PlayQueenDiamond()
    {
        PlayCard(QueenDiamond, false);
    }

    internal void PlayJackSpade()
    {
        PlayCard(JackSpade, false);
    }
    internal void PlayJackHeart()
    {
        PlayCard(JackHeart, false);
    }
    internal void PlayJackClub()
    {
        PlayCard(JackClub, false);
    }
    internal void PlayJackDiamond()
    {
        PlayCard(JackDiamond, false);
    }

    private void PlayGame(AudioClip clip, bool loop)
    {
        if (!clip) return;

        gameSoundSource.Stop();
        gameSoundSource.clip = clip;
        gameSoundSource.loop = loop;
        gameSoundSource.Play();
    }

    private void PlayCard(AudioClip clip, bool loop)
    {
        if (!clip) return;

        cardSoundSource.Stop();
        cardSoundSource.clip = clip;
        cardSoundSource.loop = loop;
        cardSoundSource.Play();
    }

    internal void StopGameAudio()
    {
        gameSoundSource.Stop();
        gameSoundSource.loop = false;
    }


    internal void MuteBackground(bool mute) => bgMusicSource.mute = mute;
    internal void MuteGame(bool mute)
    {
        gameSoundSource.mute = mute;
        cardSoundSource.mute = mute;
    }
    // internal void MuteUI(bool mute) => uiSource.mute = mute;
}
