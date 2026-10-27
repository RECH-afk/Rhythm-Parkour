using UnityEngine;
using UnityEngine.EventSystems;
using RKS.RhythmParkour;
using RKS.RhythmParkour.Core;
using RKS.RhythmParkour.Core.Managers;
using RKS.RhythmParkour.Core.Installers;
using RKS.RhythmParkour.Rhythm;
using RKS.RhythmParkour.UI;
using RKS.RhythmParkour.UI.Timeline;

namespace RKS.RhythmParkour.UI
{
    public sealed class OptionTipSource : RKSBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SettingsOptionTip tip;
        [SerializeField] private string optionId;

        bool _hovered;

        void OnEnable()
        {
            if (_hovered) ShowTip();
        }

        void OnDisable()
        {
            if (_hovered)
            {
                _hovered = false;
                HideTip();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            ShowTip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            HideTip();
        }

        void ShowTip()
        {
            if (tip == null || string.IsNullOrEmpty(optionId)) return;
            tip.Show($"settings.{optionId}.tipTitle", $"settings.{optionId}.tipDesc");
        }

        void HideTip()
        {
            if (tip == null) return;
            tip.ShowDefault();
        }
    }
}
