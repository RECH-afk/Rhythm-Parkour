using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace RKS.HadalZone.Core
{
    public class IntroBootstrap : RKSBehaviour
    {
        [Header("Intro Settings")]
        [SerializeField] private bool showIntro = true;
        [SerializeField] private VideoPlayer videoPlayer;

        protected override void OnReady()
        {
            InitializeDiscordStatus();
            InitializeSaveData();

            if (!showIntro)
            {
                Debug.Log("[IntroBootstrap] Intro disabled. Skipping to next scene.");
                LoadNextScene();
                return;
            }

            if (videoPlayer == null)
            {
                Debug.LogWarning("[IntroBootstrap] VideoPlayer is not assigned. Skipping intro.");
                LoadNextScene();
                return;
            }

            Debug.Log("[IntroBootstrap] Starting intro video...");
            videoPlayer.loopPointReached += OnIntroFinished;
            videoPlayer.Play();
        }

        private void InitializeDiscordStatus()
        {
            if (Localization == null)
                return;

            string message = Localization.currentLanguage switch
            {
                "en_US" => "Initialization...",
                "ru_RU" => "Инициализация...",
                "de_DE" => "Initialisierung...",
                "es_ES" => "Inicializando...",
                _ => "Initialization..."
            };

            DiscordRPC.details = message;
            Debug.Log("[IntroBootstrap] Discord initialized with message: " + message);
        }

        private void InitializeSaveData()
        {
            if (Save.CurrentData == null)
                Save.Load();

            Save.Write();
            Debug.Log("[IntroBootstrap] Save data initialized.");
        }

        private void OnIntroFinished(VideoPlayer source)
        {
            Debug.Log("[IntroBootstrap] Intro video finished.");
            LoadNextScene();
        }

        private void LoadNextScene()
        {
            var data = Save.CurrentData;

            if (data.isFirstRun)
            {
                data.isFirstRun = false;
                Save.Write();
                Debug.Log("[IntroBootstrap] First run → loading 'IsFirstGameOpenScene'.");
                SceneManager.LoadScene("IsFirstGameOpenScene");
                return;
            }

            if (!data.isPlayerAgreedPlay)
            {
                Debug.Log("[IntroBootstrap] Player not agreed → loading 'IsFirstGameOpenScene'.");
                Transition?.LoadScene("IsFirstGameOpenScene");
                return;
            }

            Debug.Log("[IntroBootstrap] Loading main menu...");
            Transition?.LoadScene("IsMenuScene");
        }
    }
}
