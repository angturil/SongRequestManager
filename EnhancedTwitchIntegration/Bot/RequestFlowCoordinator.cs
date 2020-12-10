using BeatSaberMarkupLanguage;
using HMUI;
using IPA.Utilities;
using SongRequestManager.UI;
using System.Linq;
using UnityEngine;

namespace SongRequestManager
{
    public class RequestFlowCoordinator : FlowCoordinator
    {
        private RequestBotListViewController _requestBotListViewController;
        private KeyboardViewController _keyboardViewController;

        public void Awake()
        {
            if (_requestBotListViewController == null && _keyboardViewController == null)
            {
                _requestBotListViewController = BeatSaberUI.CreateViewController<RequestBotListViewController>();
                _keyboardViewController = BeatSaberUI.CreateViewController<KeyboardViewController>();
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("Song Request Manager");
                showBackButton = true;

                ProvideInitialViewControllers(_requestBotListViewController, rightScreenViewController: _keyboardViewController);
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            // dismiss ourselves
            FlowCoordinator flowCoordinator;

            if (Plugin.gameMode == Plugin.GameMode.Solo)
            {
                flowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            }
            else
            {
                flowCoordinator = Resources.FindObjectsOfTypeAll<MultiplayerLevelSelectionFlowCoordinator>().First();
            }

            SetRightScreenViewController(null, ViewController.AnimationType.None);
            flowCoordinator.InvokeMethod<object, FlowCoordinator>("DismissFlowCoordinator", this, ViewController.AnimationDirection.Horizontal, null, false);
        }

        public void Dismiss()
        {
            BackButtonWasPressed(null);
        }

        public void SetTitle(string newTitle)
        {
            base.SetTitle(newTitle);
        }
    }
}
