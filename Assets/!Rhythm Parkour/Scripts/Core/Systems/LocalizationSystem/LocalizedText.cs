using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RKS.HadalZone.Core.Managers;
using RKS.HadalZone.Core;

public class LocalizedText : RKSBehaviour
{
    [SerializeField]
    public string key;

    private LocalizationManager localizationManager;
    private TMP_Text text;

    protected override void OnReady()
    {
        // ищем объект с тегом LocalizationManager и берем у него компонент LocalizationManager, потом ищем текстмешпро у объекта к которому прикреплен данный скрипт и обновляем текст

        UpdateText();

        if (localizationManager == null)
        {
            localizationManager = GameObject.FindGameObjectWithTag("LocalizationManager").GetComponent<LocalizationManager>();
        }
        if(text == null)
        {
            text = GetComponent<TMP_Text>();
        }
        localizationManager.OnLanguageChanged += UpdateText;
    }

    protected override void OnDisposed()
    {
        // вызывается при удалении объекта LocalizationManager

        localizationManager.OnLanguageChanged -= UpdateText;
    }

    public virtual void UpdateText()
    {
        // метод для обновления текста

        if (gameObject == null) return;

        if(localizationManager == null)
        {
            localizationManager = GameObject.FindGameObjectWithTag("LocalizationManager").GetComponent<LocalizationManager>();
        }
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }
        text.text = localizationManager.GetLocalizedValue(key);
    }
}