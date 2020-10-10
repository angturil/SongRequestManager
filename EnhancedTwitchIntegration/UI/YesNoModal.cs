using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using System.Reflection;
using TMPro;

namespace SongRequestManager
{
    public class YesNoModal : PersistentSingleton<YesNoModal>
    {
        private Action OnConfirm;
        private Action OnDecline;

        [UIComponent("modal")]
        internal ModalView modal;

        [UIComponent("title")]
        internal TextMeshProUGUI _title;

        [UIComponent("message")]
        internal TextMeshProUGUI _message;

        [UIAction("yes-click")]
        private void YesClick()
        {
            modal.Hide(true);
            OnConfirm?.Invoke();
            OnConfirm = null;
        }

        [UIAction("no-click")]
        private void NoClick()
        {
            modal.Hide(true);
            OnDecline?.Invoke();
            OnDecline = null;
        }

        public void ShowDialog(string title, string message, Action onConfirm = null, Action onDecline = null)
        {
            _title.text = title;
            _message.text = message;

            OnConfirm = onConfirm;
            OnDecline = onDecline;

            modal.Show(true);
        }

        internal void Setup()
        {
            BSMLParser.instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "SongRequestManager.Views.YesNoModal.bsml"), RequestBotListViewController.Instance.gameObject, this);
        }
    }
}
