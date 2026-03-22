using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    [SerializeField] private Image CardBg;
    [SerializeField] private Image SymbolImage;
    [SerializeField] private Image TextImage;

    internal void SetData(Sprite cardBg, Sprite Symbol, Sprite Text)
    {
        CardBg.sprite = cardBg;
        SymbolImage.sprite = Symbol;
        TextImage.sprite = Text;
    }
}
