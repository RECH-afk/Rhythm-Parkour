namespace EasyPeasyFirstPersonController
{
    using System;
    using UnityEngine;
    using DG.Tweening;

    /// <summary>Оценка ритм-действия игрока (прыжок/слайд/приземление в бит).</summary>
    public enum RhythmGrade { Perfect, Good, Miss }

    public partial class FirstPersonController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Range(0, 100)] public float mouseSensitivity = 50f;
        [Range(0f, 200f)] private float snappiness = 100f;
        [Range(0f, 200f)] public float walkSpeed = 7f; // Увеличено для шустлости
        [Range(0f, 50f)] public float sprintSpeed = 12f;
        [Range(0f, 20f)] public float crouchSpeed = 4f;
        public float crouchHeight = 1f;
        public float crouchCameraHeight = 1f;
        [Tooltip("Скорость опускания камеры при приседе/слайде (м/с) — резко вниз")]
        public float crouchDownSpeed = 30f;
        [Tooltip("Скорость подъёма камеры (м/с) — плавно вверх")]
        public float crouchUpSpeed = 12f;

        [Header("Slide Settings")]
        public float slideSpeed = 15f;
        public float slideDuration = 0.7f;
        public float slideFovBoost = 10f;
        public float slideTiltAngle = 8f;

        [Header("Jump & Gravity")]
        [Range(0f, 20f)] public float jumpSpeed = 6f;
        [Range(0f, 50f)] public float gravity = 19.62f; // Увеличена для более быстрого падения и чётких прыжков
        [Range(0.01f, 0.3f)] public float coyoteTimeDuration = 0.15f;

        [Header("FOV Settings")]
        public float normalFov = 70f; // Чуть шире для динамики
        public float sprintFov = 85f;
        public float fovChangeSpeed = 8f;

        [Header("Zoom Settings")]
        public KeyCode zoomKey = KeyCode.Mouse1;
        public float zoomFov = 40f;
        [Range(0.1f, 1f)] public float zoomSensitivityMultiplier = 0.4f;

        [Space(5)]
        [Header("Camera Settings")]
        public float bobbingAmount = 0.06f;
        public float walkingBobbingSpeed = 16f;
        public float idleBobbingAmount = 0.01f;
        public float idleBobbingSpeed = 2f;
        public float bobPitchAmount = 0.5f;
        public float bobRollAmount = 0.5f;
        public float strafeTiltAngle = 2f;
        public float lookSwayMultiplier = 1.5f;
        public float landingDipAmount = 0.15f;
        public float jumpDipAmount = 0.1f;

        [Header("Rhythm Mechanics")]
        [Tooltip("BPM для внутреннего метронома (запасной вариант, когда нет Conductor). При активной музыке BPM берётся из трека")]
        public float bpm = 120f;
        [Tooltip("Окно в секундах для идеального попадания в бит")]
        public float perfectWindow = 0.05f;
        [Tooltip("Окно в секундах для хорошего попадания")]
        public float goodWindow = 0.15f;
        [Tooltip("Максимальное значение шкалы Грува")]
        public float maxFlowMeter = 100f;
        [Tooltip("Скорость падения шкалы Грува в секунду")]
        public float flowDecayRate = 8f;
        [Tooltip("Множитель скорости во время ритм-рывка")]
        public float beatDashSpeedMultiplier = 2.2f;
        [Tooltip("Длительность ритм-рывка")]
        public float beatDashDuration = 0.25f;

        [Header("Связь с музыкой")]
        [Tooltip("Брать BPM и фазу бита из Conductor — точная синхронизация с треком. Выкл = внутренний метроном")]
        public bool useConductor = true;
        [Tooltip("BPM в этом контроллере сам подтягивается из BPM текущей песни (видно в инспекторе)")]
        public bool syncBpmFromMusic = true;
        [Tooltip("Пульс FOV на каждый бит (0 = выкл)")]
        public float beatFovPulse = 2.5f;
        [Tooltip("Добавка к амплитуде покачивания на бит")]
        public float beatBobPulse = 0.03f;
        [Tooltip("Полных циклов покачивания на 2 бита (0.5 = шаг каждой ногой в бит)")]
        public float bobCyclesPerBeat = 0.5f;
        [Tooltip("Множитель высоты прыжка при идеальном попадании в бит (1 = без бонуса)")]
        public float beatJumpBoost = 1.12f;
        [Tooltip("Буфер прыжка: нажатие чуть раньше приземления всё равно сработает (сек)")]
        public float jumpBufferTime = 0.12f;

        [Header("Прыжки строго в ритм")]
        [Tooltip("Нажатый прыжок выполняется ровно в ближайший бит, а не сразу — игрок всегда прыгает под ритм")]
        public bool quantizeJumpToBeat = true;
        [Tooltip("Нажал раньше чем за столько долей бита — прыжок встанет в очередь на следующий бит")]
        [Range(0.05f, 1f)] public float jumpCaptureBeats = 0.6f;
        [Tooltip("Очередь прыжка сгорает, если бит не наступил за столько битов")]
        public float jumpQueueTimeoutBeats = 1.5f;

        [Header("Зона движения")]
        [Tooltip("Зажать игрока по ширине и длине дорожки (границы берутся из RhythmParkourManager)")]
        public bool clampToTrack = true;
        [Tooltip("Выключить ходьбу вперёд/назад — только стрейфы, прыжки и слайд")]
        public bool lockForwardBack = false;
        [Tooltip("Запас от краёв зоны")]
        public float areaPadding = 0.3f;
        [Tooltip("Бонус Groove за чистый додж ноты (Perfect)")]
        public float dodgeFlowBonus = 6f;
        [Tooltip("Штраф Groove при получении урона")]
        public float damageFlowPenalty = 25f;

        [Header("Тряска камеры под музыку")]
        [Tooltip("Камера дрожит в такт играющему треку (кик на каждый бит)")]
        public bool musicShakeEnabled = true;
        [Tooltip("Сила тряски на бит (смещение камеры в метрах)")]
        [Range(0f, 0.3f)] public float musicShakeAmount = 0.045f;
        [Tooltip("Скорость дрожания")]
        public float musicShakeFrequency = 31f;
        [Tooltip("Сила тряски зависит от реальной громкости/баса трека (анализ спектра)")]
        public bool musicShakeReactToLoudness = true;
        [Tooltip("Усиление сигнала спектра (бас тихих треков тоже будет качать)")]
        public float spectrumGain = 4f;

        [Header("Rhythm Landing Shake Settings")]
        public bool enableLandingShake = true;
        [Range(0f, 1f)] public float landingShakeIntensity = 0.3f;
        [Range(0.1f, 0.8f)] public float landingShakeDuration = 0.4f;
        [Range(8f, 25f)] public float landingShakeFrequency = 12f;
        public Ease shakeEase = Ease.OutQuad;
        public bool syncToBeat = true;
        [Range(0.5f, 2f)] public float beatMultiplier = 1.2f;
        public bool enableComboSystem = true;
        [Range(1f, 3f)] public float comboIntensityMultiplier = 1.15f;

        [Header("Toggles & Checks")]
        public bool canSlide = true;
        public bool canJump = true;
        public bool canSprint = true;
        public bool canCrouch = true;
        public bool canZoom = true;
        public bool coyoteTimeEnabled = true;
        public QueryTriggerInteraction ceilingCheckQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
        public QueryTriggerInteraction groundCheckQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
        public Transform groundCheck;
        public float groundDistance = 0.2f;
        public LayerMask groundMask;

        [Header("References")]
        public Transform playerCamera;
        public Transform cameraParent;

        // Private variables
        private float rotX, rotY, xVelocity, yVelocity;
        private CharacterController characterController;
        private CapsuleCollider playerCapsule; // дублирует CharacterController для честного хитбокса
        private Vector3 moveDirection = Vector3.zero;
        private bool isGrounded, wasGrounded;

        // Snappy input
        private Vector2 rawMoveInput;
        private Vector2 smoothMoveInput;
        private Vector2 moveInputVelocity;
        private float moveSmoothTime = 0.03f; // Мгновенная, но сглаженная реакция

        public bool isSprinting, isCrouching, isSliding, isZooming;
        private float slideTimer, postSlideCrouchTimer;
        private Vector3 slideDirection;
        private float originalHeight, originalCameraParentHeight, coyoteTimer;
        private Camera cam;
        private AudioSource slideAudioSource;

        private float bobTimer, currentBobAmplitude, currentBobSpeed, mouseXSway, defaultPosY;
        private Vector3 currentTilt = Vector3.zero;
        private bool isLook = true, isMove = true;

        private float currentCameraHeight, currentBobOffsetY, currentBobOffsetX, currentFov, fovVelocity;
        private float currentSlideSpeed, slideSpeedVelocity, currentTiltAngle, tiltVelocity, bodyDip, bodyDipVelocity;

        // Landing shake
        private Vector3 landingShakeOffset;
        private float currentShakeIntensity, shakePhase;
        private bool isShaking;
        private Tween shakeTween;

        // Rhythm variables
        private float beatInterval;
        private float nextBeatTime;
        private float lastBeatTime;
        private float flowMeter;
        private bool isBeatDashing;
        private float beatDashTimer;
        private int landingCombo = 0;
        private float lastLandingTime = -1f;
        [Range(0.1f, 2f)] public float comboWindow = 0.5f;

        // Связь с музыкой и игрой (источник бита — Conductor, очки — RhythmScoreManager)
        private Conductor beatConductor;
        private RhythmScoreManager scoreLink;
        private RhythmParkourManager parkourLink;
        private int lastMusicBeat = -1;
        private float beatFovKick;
        private float beatBobKick;
        private float jumpBufferTimer;
        private bool jumpQueued; // нажат прыжок — ждём ближайший бит
        private int jumpQueuedBeat = -1;
        private Vector3 baseCamLocalPos = Vector3.zero;
        private bool hasBaseCamPos;
        private Tween beatDashTween;

        // Тряска камеры под музыку (кик на бит + энергия спектра трека)
        private float musicShakeKick;
        private float musicShakePhase;
        private float musicShakeSeed;
        private float musicEnergyNorm = 0.5f;
        private Vector3 musicShakeOffset = Vector3.zero;
        private readonly float[] spectrumCache = new float[64];
        private bool spectrumSupported = true;

        /// <summary>Энергия музыки 0..1 (бас текущего трека) — можно использовать для своих эффектов.</summary>
        public float MusicEnergy01 => IsMusicActive ? musicEnergyNorm : 0f;

        /// <summary>Действие игрока оценено по ритму (прыжок/слайд). Подписка для UI/эффектов.</summary>
        public event Action<RhythmGrade> onRhythmAction;

        /// <summary>Музыка играет и бит идёт из трека (а не из внутреннего метронома).</summary>
        public bool IsMusicActive => useConductor && beatConductor != null && beatConductor.isPlaying && beatConductor.songPositionBeats >= 0f;
        /// <summary>Текущий BPM: из трека, если музыка играет, иначе внутренний.</summary>
        public float EffectiveBpm => IsMusicActive ? beatConductor.bpm : bpm;
        /// <summary>Индекс текущего бита трека (-1 если музыки нет).</summary>
        public int CurrentBeatIndex => IsMusicActive ? Mathf.FloorToInt(beatConductor.songPositionBeats) : -1;
        /// <summary>Прогресс текущего бита 0..1 (для пульсаций UI/эффектов).</summary>
        public float BeatProgress01
        {
            get
            {
                if (IsMusicActive)
                {
                    float b = beatConductor.songPositionBeats;
                    return Mathf.Clamp01(b - Mathf.Floor(b));
                }
                if (beatInterval > 0.001f) return Mathf.Clamp01((Time.time - lastBeatTime) / beatInterval);
                return 0f;
            }
        }
        /// <summary>Секунд до ближайшего бита (0 = ровно в бит). Точность — по треку.</summary>
        public float BeatOffsetSeconds
        {
            get
            {
                if (IsMusicActive)
                {
                    float b = beatConductor.songPositionBeats;
                    float frac = b - Mathf.Floor(b);
                    return Mathf.Min(frac, 1f - frac) * Mathf.Max(0.01f, beatConductor.secPerBeat);
                }
                float since = Mathf.Max(0f, Time.time - lastBeatTime);
                float to = Mathf.Max(0f, nextBeatTime - Time.time);
                return Mathf.Min(since, to);
            }
        }

        public float CurrentCameraHeight => isCrouching || isSliding ? crouchCameraHeight : originalCameraParentHeight;
        public int CurrentLandingCombo => landingCombo;
        public float CurrentFlowMeter => flowMeter; // Для UI

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
                Debug.LogError("[FPC] Нет CharacterController на игроке — движение не будет работать!", this);
            playerCapsule = GetComponent<CapsuleCollider>();
            if (playerCapsule == null) playerCapsule = GetComponentInChildren<CapsuleCollider>();
            if (playerCamera == null)
            {
                var camInKids = GetComponentInChildren<Camera>();
                if (camInKids != null) playerCamera = camInKids.transform;
                else Debug.LogError("[FPC] Не назначена playerCamera!", this);
            }
            if (playerCamera != null)
            {
                cam = playerCamera.GetComponent<Camera>();
                baseCamLocalPos = playerCamera.localPosition;
                hasBaseCamPos = true;
                if (cameraParent == null) cameraParent = playerCamera.parent;
            }
            if (cameraParent == null)
                Debug.LogError("[FPC] Не назначен cameraParent!", this);
            if (groundCheck == null)
                Debug.LogWarning("[FPC] Не назначен groundCheck — использую characterController.isGrounded", this);
            originalHeight = characterController != null ? characterController.height : 1.8f;
            originalCameraParentHeight = cameraParent != null ? cameraParent.localPosition.y : 1.6f;
            defaultPosY = originalCameraParentHeight;

            slideAudioSource = gameObject.AddComponent<AudioSource>();
            slideAudioSource.playOnAwake = false;
            slideAudioSource.loop = false;

            Cursor.lockState = CursorLockMode.Locked;
            currentCameraHeight = originalCameraParentHeight;
            currentFov = normalFov;
            currentBobAmplitude = idleBobbingAmount;
            currentBobSpeed = idleBobbingSpeed;

            rotX = transform.rotation.eulerAngles.y;
            rotY = playerCamera != null ? playerCamera.localRotation.eulerAngles.x : 0f;
            xVelocity = rotX;
            yVelocity = rotY;

            landingShakeOffset = Vector3.zero;
            currentShakeIntensity = 0f;
            shakePhase = 0f;
            isShaking = false;
            landingCombo = 0;

            // Init rhythm
            beatInterval = 60f / bpm;
            nextBeatTime = Time.time + beatInterval;
            lastBeatTime = Time.time;
            flowMeter = 0f;
        }

        private void OnDestroy()
        {
            if (shakeTween != null && shakeTween.IsActive()) shakeTween.Kill();
            if (beatDashTween != null && beatDashTween.IsActive()) beatDashTween.Kill();
            if (scoreLink != null) scoreLink.onJudgement -= OnScoreJudgement;
        }

        /// <summary>Ленивая привязка к Conductor и счёту (могут появиться позже контроллера).</summary>
        private void EnsureLinks()
        {
            if (useConductor && beatConductor == null) beatConductor = Conductor.Instance;
            // BPM контроллера берём из BPM текущей песни
            if (syncBpmFromMusic && beatConductor != null && beatConductor.bpm > 1f
                && Mathf.Abs(bpm - beatConductor.bpm) > 0.01f)
            {
                SetBPM(beatConductor.bpm);
            }
            if (scoreLink == null)
            {
                var sm = RhythmScoreManager.Instance;
                if (sm != null) { scoreLink = sm; scoreLink.onJudgement += OnScoreJudgement; }
            }
            if (parkourLink == null) parkourLink = RhythmParkourManager.Instance;
        }

        private void Update()
        {
            EnsureLinks();

            if (groundCheck != null)
                isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, groundCheckQueryTriggerInteraction);
            else if (characterController != null)
                isGrounded = characterController.isGrounded;

            // --- RHYTHM TICK: бит из трека, внутренний метроном — только запасной ---
            if (IsMusicActive)
            {
                int beatIdx = Mathf.FloorToInt(beatConductor.songPositionBeats);
                if (beatIdx != lastMusicBeat && beatIdx >= 0)
                {
                    lastMusicBeat = beatIdx;
                    OnMusicBeat(beatIdx);
                }
            }
            else if (Time.time >= nextBeatTime)
            {
                OnBeatHit();
            }

            // очередь прыжка: музыка встала — прыгаем обычным буфером, протухла — сгорает
            if (!isMove) jumpQueued = false;
            else if (jumpQueued)
            {
                if (!IsMusicActive) { jumpQueued = false; jumpBufferTimer = jumpBufferTime; }
                else if (CurrentBeatIndex - jumpQueuedBeat > Mathf.Max(0.5f, jumpQueueTimeoutBeats)) jumpQueued = false;
            }

            // затухание бит-пульса камеры
            float pulseDecay = Mathf.Exp(-7f * Time.deltaTime);
            beatFovKick *= pulseDecay;
            if (beatFovKick < 0.01f) beatFovKick = 0f;
            beatBobKick *= pulseDecay;
            if (beatBobKick < 0.001f) beatBobKick = 0f;

            UpdateMusicShake();

            // Flow decay
            if (flowMeter > 0)
            {
                flowMeter -= flowDecayRate * Time.deltaTime;
                if (flowMeter < 0) flowMeter = 0;
            }

            // Beat dash timer
            if (isBeatDashing)
            {
                beatDashTimer -= Time.deltaTime;
                if (beatDashTimer <= 0) isBeatDashing = false;
            }

            if (canZoom && !isSprinting && !isSliding)
                isZooming = Input.GetKey(zoomKey);
            else
                isZooming = false;

            if (isGrounded && !wasGrounded)
            {
                if (moveDirection.y < -3f)
                    bodyDip = -landingDipAmount * Mathf.Clamp01(Mathf.Abs(moveDirection.y) / 10f);

                if (enableLandingShake && moveDirection.y < -2f)
                    TriggerLandingShake();

                // очередь прыжка дожила до приземления — прыгаем сразу
                if (jumpQueued && !isSliding) { jumpQueued = false; jumpBufferTimer = jumpBufferTime; }

                coyoteTimer = coyoteTimeEnabled ? coyoteTimeDuration : 0f;
            }
            else if (coyoteTimeEnabled && !isGrounded)
            {
                coyoteTimer -= Time.deltaTime;
            }

            bodyDip = Mathf.SmoothDamp(bodyDip, 0f, ref bodyDipVelocity, 0.1f);

            if (isShaking)
            {
                shakePhase += Time.deltaTime * landingShakeFrequency;
                float shakeX = Mathf.Sin(shakePhase) * currentShakeIntensity;
                float shakeY = Mathf.Cos(shakePhase * 0.7f) * currentShakeIntensity * 0.4f;
                landingShakeOffset = new Vector3(shakeX, shakeY, 0f);
            }
            else
            {
                landingShakeOffset = Vector3.Lerp(landingShakeOffset, Vector3.zero, Time.deltaTime * 15f);
            }

            if (isGrounded && moveDirection.y < 0)
            {
                moveDirection.y = -2f; // Стабильное приземление
            }

            HandleLook();
            HandleHeadBob();
            HandleCrouchAndSlide();
            HandleMovement();

            wasGrounded = isGrounded;
        }

        private void HandleLook()
        {
            if (!isLook || playerCamera == null) return;

            float currentSensitivity = mouseSensitivity * (isZooming ? zoomSensitivityMultiplier : 1f);
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            mouseXSway = Mathf.Lerp(mouseXSway, mouseX, Time.deltaTime * 10f);
            rotX += mouseX * 10 * currentSensitivity * Time.deltaTime;
            rotY -= mouseY * 10 * currentSensitivity * Time.deltaTime;
            rotY = Mathf.Clamp(rotY, -90f, 90f);

            xVelocity = Mathf.Lerp(xVelocity, rotX, snappiness * Time.deltaTime);
            yVelocity = Mathf.Lerp(yVelocity, rotY, snappiness * Time.deltaTime);

            float targetTiltAngle = isSliding ? slideTiltAngle : 0f;
            currentTiltAngle = Mathf.SmoothDamp(currentTiltAngle, targetTiltAngle, ref tiltVelocity, 0.15f);

            playerCamera.transform.localRotation = Quaternion.Euler(yVelocity - currentTiltAngle, 0f, 0f);
            transform.rotation = Quaternion.Euler(0f, xVelocity, 0f);
        }

        private void HandleCrouchAndSlide()
        {
            if (characterController == null) return;
            bool wantsToCrouch = canCrouch && Input.GetKey(KeyCode.LeftControl) && !isSliding;
            Vector3 point1 = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
            Vector3 point2 = point1 + Vector3.up * characterController.height * 0.6f;
            float capsuleRadius = characterController.radius * 0.95f;
            float castDistance = isSliding ? originalHeight + 0.2f : originalHeight - crouchHeight + 0.2f;
            bool hasCeiling = Physics.CapsuleCast(point1, point2, capsuleRadius, Vector3.up, castDistance, groundMask, ceilingCheckQueryTriggerInteraction);

            if (isSliding)
            {
                postSlideCrouchTimer = 0.3f;
            }
            else if (postSlideCrouchTimer > 0)
            {
                postSlideCrouchTimer -= Time.deltaTime;
                isCrouching = canCrouch;
            }
            else
            {
                isCrouching = canCrouch && (wantsToCrouch || (hasCeiling && !isSliding));
            }

            if (canSlide && isSprinting && !isSliding && Input.GetKeyDown(KeyCode.LeftControl) && isGrounded)
            {
                isSliding = true;
                slideTimer = slideDuration;
                slideDirection = smoothMoveInput.magnitude > 0.1f ? (transform.right * smoothMoveInput.x + transform.forward * smoothMoveInput.y).normalized : transform.forward;
                currentSlideSpeed = sprintSpeed;

                CheckRhythmAction();
            }

            if (isSliding)
            {
                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0f) isSliding = false; // Убрано !isGrounded, чтобы можно было слетать с платформ в слайде (ритм-фишка)

                float slideProgress = slideTimer / slideDuration;
                float targetSlideSpeed = slideSpeed * Mathf.Lerp(0.7f, 1f, slideProgress);
                currentSlideSpeed = Mathf.SmoothDamp(currentSlideSpeed, targetSlideSpeed, ref slideSpeedVelocity, 0.1f);

                // Интеграция слайда в moveDirection для корректной работы гравитации
                moveDirection.x = slideDirection.x * currentSlideSpeed;
                moveDirection.z = slideDirection.z * currentSlideSpeed;
            }

            float targetHeight = isCrouching || isSliding ? crouchHeight : originalHeight;
            // коллайдеры — МГНОВЕННО (хитбокс честный сразу, без анимации)
            if (Mathf.Abs(characterController.height - targetHeight) > 0.001f)
            {
                characterController.height = targetHeight;
                characterController.center = new Vector3(0f, targetHeight * 0.5f, 0f);
            }
            // CapsuleCollider дублирует CharacterController один в один (тоже мгновенно)
            if (playerCapsule != null)
            {
                playerCapsule.height = characterController.height;
                playerCapsule.center = characterController.center;
                playerCapsule.radius = characterController.radius;
                playerCapsule.direction = 1;
            }
        }

        private void HandleMovement()
        {
            if (characterController == null) return;
            // Snappy input
            rawMoveInput.x = Input.GetAxisRaw("Horizontal");
            rawMoveInput.y = Input.GetAxisRaw("Vertical");
            if (lockForwardBack) rawMoveInput.y = 0f; // только стрейфы — вперёд/назад выкл
            smoothMoveInput = Vector2.SmoothDamp(smoothMoveInput, rawMoveInput, ref moveInputVelocity, moveSmoothTime);

            isSprinting = canSprint && Input.GetKey(KeyCode.LeftShift) && smoothMoveInput.y > 0.1f && isGrounded && !isCrouching && !isSliding && !isZooming;

            // Flow Meter speed bonus (до +50% скорости)
            float flowSpeedMultiplier = 1f + (flowMeter / maxFlowMeter) * 0.5f;

            float currentSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            if (isBeatDashing) currentSpeed *= beatDashSpeedMultiplier;
            currentSpeed *= flowSpeedMultiplier;

            if (!isMove) currentSpeed = 0f;

            Vector3 direction = new Vector3(smoothMoveInput.x, 0f, smoothMoveInput.y);
            Vector3 moveVector = transform.TransformDirection(direction) * currentSpeed;
            moveVector = Vector3.ClampMagnitude(moveVector, currentSpeed);

            if (!isSliding)
            {
                moveDirection.x = moveVector.x;
                moveDirection.z = moveVector.z;
            }

            // Gravity & Jump. С квантованием нажатие превращается в прыжок ровно в бит
            if (canJump && isMove && Input.GetKeyDown(KeyCode.Space) && !isSliding)
            {
                if (quantizeJumpToBeat && IsMusicActive)
                {
                    float secPerBeat = Mathf.Max(0.01f, beatConductor.secPerBeat);
                    float frac = beatConductor.songPositionBeats - Mathf.Floor(beatConductor.songPositionBeats);
                    float toNextBeat = (1f - frac) * secPerBeat;
                    if (BeatOffsetSeconds <= perfectWindow)
                        jumpBufferTimer = jumpBufferTime; // и так в бит — сразу
                    else if (toNextBeat <= Mathf.Max(0.05f, jumpCaptureBeats) * secPerBeat)
                    {
                        jumpQueued = true; // в очередь на ближайший бит
                        jumpQueuedBeat = CurrentBeatIndex;
                    }
                    else
                        jumpBufferTimer = jumpBufferTime; // слишком рано — обычный прыжок со штрафом
                }
                else
                    jumpBufferTimer = jumpBufferTime;
            }
            else
                jumpBufferTimer -= Time.deltaTime;

            if (isGrounded || coyoteTimer > 0f)
            {
                if (canJump && isMove && jumpBufferTimer > 0f && !isSliding)
                    DoJump();
            }
            else
            {
                moveDirection.y -= gravity * Time.deltaTime;
            }

            characterController.Move(moveDirection * Time.deltaTime);

            if (clampToTrack) ClampToTrack();

            // FOV Handling
            float targetFov = normalFov;
            if (isZooming) targetFov = zoomFov;
            else if (isSprinting) targetFov = sprintFov;
            else if (isSliding)
            {
                float slideProgress = slideTimer / slideDuration;
                targetFov = sprintFov + (slideFovBoost * Mathf.Lerp(0f, 1f, 1f - slideProgress));
            }

            float fallFovBoost = (!isGrounded && moveDirection.y < -5f) ? Mathf.Clamp(Mathf.Abs(moveDirection.y) * 0.4f, 0f, 15f) : 0f;
            targetFov += fallFovBoost + beatFovKick; // бит-пульс из OnMusicBeat

            currentFov = Mathf.SmoothDamp(currentFov, targetFov, ref fovVelocity, 1f / fovChangeSpeed);
            if (cam != null) cam.fieldOfView = currentFov;
        }

        private void HandleHeadBob()
        {
            if (cameraParent == null || characterController == null) return;
            Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
            bool isMovingEnough = horizontalVelocity.magnitude > 0.1f;

            float targetBobSpeed = idleBobbingSpeed;
            float targetBobAmount = idleBobbingAmount;

            if (isMovingEnough && isGrounded && !isSliding)
            {
                float speedMultiplier = isSprinting ? 1.4f : (isCrouching ? 0.7f : 1f);
                float amountMultiplier = isSprinting ? 1.4f : (isCrouching ? 0.6f : 1f);
                targetBobSpeed = walkingBobbingSpeed * speedMultiplier;
                targetBobAmount = bobbingAmount * amountMultiplier;
            }
            else if (!isGrounded || isSliding)
            {
                targetBobSpeed = 0f;
                targetBobAmount = 0f;
            }

            currentBobAmplitude = Mathf.Lerp(currentBobAmplitude, targetBobAmount, Time.deltaTime * 8f);
            currentBobSpeed = Mathf.Lerp(currentBobSpeed, targetBobSpeed, Time.deltaTime * 8f);

            if (IsMusicActive)
            {
                // шаги строго в бит: фаза покачивания идёт от трека, а не от времени
                bobTimer = beatConductor.songPositionBeats * Mathf.PI * 2f * Mathf.Max(0.05f, bobCyclesPerBeat);
            }
            else
            {
                bobTimer += Time.deltaTime * currentBobSpeed;
            }

            // бит-пульс добавляется к амплитуде — камера «дышит» под музыку
            float pulseAmp = currentBobAmplitude + beatBobKick;

            currentBobOffsetY = Mathf.Sin(bobTimer) * pulseAmp;
            currentBobOffsetX = Mathf.Cos(bobTimer / 2f) * pulseAmp;

            float bobRatio = pulseAmp / (bobbingAmount > 0.001f ? bobbingAmount : 1f);
            float currentBobPitch = -Mathf.Abs(Mathf.Sin(bobTimer)) * bobPitchAmount * bobRatio;
            float currentBobRoll = Mathf.Cos(bobTimer / 2f) * bobRollAmount * bobRatio;

            float targetStrafeTilt = isGrounded && !isSliding ? (-smoothMoveInput.x * strafeTiltAngle) - (mouseXSway * lookSwayMultiplier) : 0f;
            currentTilt.z = Mathf.Lerp(currentTilt.z, targetStrafeTilt + currentBobRoll, Time.deltaTime * 10f);
            currentTilt.x = Mathf.Lerp(currentTilt.x, currentBobPitch, Time.deltaTime * 10f);

            float targetCameraHeight = isCrouching || isSliding ? crouchCameraHeight : originalCameraParentHeight;
            // камера: вниз — резко, вверх — плавно (анимация остаётся, но присед чувствуется сразу)
            float camSpd = targetCameraHeight < currentCameraHeight ? crouchDownSpeed : crouchUpSpeed;
            currentCameraHeight = Mathf.MoveTowards(currentCameraHeight, targetCameraHeight, camSpd * Time.deltaTime);

            cameraParent.localPosition = new Vector3(
                currentBobOffsetX + landingShakeOffset.x + musicShakeOffset.x,
                currentCameraHeight + currentBobOffsetY + bodyDip + landingShakeOffset.y + musicShakeOffset.y,
                cameraParent.localPosition.z
            );
            cameraParent.localRotation = Quaternion.Euler(currentTilt.x, 0f, currentTilt.z);
        }

        // ================= RHYTHM MECHANICS (бит идёт из Conductor) =================

        /// <summary>Тик запасного метронома — работает только когда нет музыки.</summary>
        private void OnBeatHit()
        {
            lastBeatTime = nextBeatTime;
            nextBeatTime += beatInterval;
        }

        /// <summary>Пульс настоящего бита из трека: FOV-кик + покачивание + кик тряски.</summary>
        private void OnMusicBeat(int beatIndex)
        {
            Vector3 hv = characterController != null
                ? new Vector3(characterController.velocity.x, 0f, characterController.velocity.z)
                : Vector3.zero;
            bool moving = hv.magnitude > 0.5f;
            beatFovKick = Mathf.Max(beatFovKick, beatFovPulse * (moving || isSliding ? 1f : 0.35f));
            beatBobKick = Mathf.Max(beatBobKick, beatBobPulse * (moving ? 1f : 0.4f));
            // тряска: новый кик на бит + новая случайная фаза, чтобы дрожание не было механическим
            musicShakeKick = musicShakeAmount;
            musicShakeSeed = UnityEngine.Random.value * 100f;
            // прыжок из очереди — ровно в бит
            if (jumpQueued && !isSliding && (isGrounded || coyoteTimer > 0f))
            {
                jumpQueued = false;
                jumpBufferTimer = jumpBufferTime;
            }
        }

        /// <summary>Выполнить прыжок: оценка в ритм, буст за Perfect, сброс буферов.</summary>
        private void DoJump()
        {
            RhythmGrade g = CheckRhythmAction();
            float boost = g == RhythmGrade.Perfect ? Mathf.Max(1f, beatJumpBoost) : 1f;
            moveDirection.y = jumpSpeed * boost;
            jumpBufferTimer = 0f;
            jumpQueued = false;
            coyoteTimer = 0f;
            bodyDip = -jumpDipAmount;
        }

        /// <summary>Зажать игрока в границах дорожки (X — ширина, Z — от спавна до деспавна).</summary>
        private void ClampToTrack()
        {
            if (parkourLink == null) return;
            float pad = Mathf.Max(0f, areaPadding);
            Vector3 p = transform.position;
            float minX = parkourLink.trackMinX + pad;
            float maxX = parkourLink.trackMaxX - pad;
            if (minX <= maxX) p.x = Mathf.Clamp(p.x, minX, maxX);
            var sp = parkourLink.spawnPoint;
            var dp = parkourLink.despawnPoint;
            if (sp != null && dp != null)
            {
                float zA = Mathf.Min(sp.position.z, dp.position.z) + pad;
                float zB = Mathf.Max(sp.position.z, dp.position.z) - pad;
                if (zA <= zB) p.z = Mathf.Clamp(p.z, zA, zB);
            }
            transform.position = p;
        }

        /// <summary>
        /// Тряска камеры под играющий трек: кик на бит, сила — от реальной энергии (бас) музыки.
        /// </summary>
        private void UpdateMusicShake()
        {
            if (!musicShakeEnabled || !IsMusicActive)
            {
                musicShakeKick = 0f;
                musicEnergyNorm = 0f;
                musicShakeOffset = Vector3.Lerp(musicShakeOffset, Vector3.zero, Time.deltaTime * 10f);
                return;
            }

            UpdateMusicEnergy();

            musicShakeKick *= Mathf.Exp(-5f * Time.deltaTime);
            if (musicShakeKick < 0.0005f) musicShakeKick = 0f;

            float loudness = musicShakeReactToLoudness ? (0.35f + 0.65f * musicEnergyNorm) : 1f;
            float amp = musicShakeKick * loudness;
            musicShakePhase += Time.deltaTime * Mathf.Max(1f, musicShakeFrequency);
            float s = musicShakeSeed;
            musicShakeOffset = new Vector3(
                (Mathf.Sin(musicShakePhase * 1.13f + s) + Mathf.Sin(musicShakePhase * 2.71f + s * 1.7f) * 0.5f) * amp,
                (Mathf.Cos(musicShakePhase * 0.97f + s * 0.6f) + Mathf.Sin(musicShakePhase * 2.13f + s) * 0.5f) * amp * 0.7f,
                0f);
        }

        /// <summary>Энергия баса текущего трека через спектр AudioSource (быстрая атака, медленный спад).</summary>
        private void UpdateMusicEnergy()
        {
            var src = beatConductor != null ? beatConductor.musicSource : null;
            if (!musicShakeReactToLoudness || src == null || !src.isPlaying)
            {
                if (!musicShakeReactToLoudness) musicEnergyNorm = 0.65f;
                else musicEnergyNorm *= Mathf.Exp(-3f * Time.deltaTime);
                return;
            }
            if (!spectrumSupported) return;
            try
            {
                src.GetSpectrumData(spectrumCache, 0, FFTWindow.BlackmanHarris);
                int n = Mathf.Min(8, spectrumCache.Length); // бас-полоса
                float bass = 0f;
                for (int i = 0; i < n; i++) bass += spectrumCache[i];
                bass /= Mathf.Max(1, n);
                float target = Mathf.Clamp01(bass * Mathf.Max(0.1f, spectrumGain));
                musicEnergyNorm = Mathf.Max(target, musicEnergyNorm * Mathf.Exp(-3f * Time.deltaTime));
            }
            catch
            {
                // платформа не отдаёт спектр — качаем просто по битам
                spectrumSupported = false;
                musicEnergyNorm = 0.65f;
            }
        }

        /// <summary>
        /// Вызывайте из Audio Manager для идеальной синхронизации запасного метронома.
        /// При активной музыке бит всё равно берётся из Conductor.
        /// </summary>
        public void ForceBeat()
        {
            lastBeatTime = Time.time;
            nextBeatTime = lastBeatTime + beatInterval;
            OnBeatHit();
        }

        public void SetBPM(float newBpm)
        {
            bpm = Mathf.Max(20f, newBpm);
            beatInterval = 60f / bpm;
            nextBeatTime = Time.time + beatInterval;
        }

        /// <summary>Оценка действия по расстоянию до ближайшего бита (по треку, если музыка играет).</summary>
        private RhythmGrade CheckRhythmAction()
        {
            float off = BeatOffsetSeconds;
            RhythmGrade grade = off <= perfectWindow ? RhythmGrade.Perfect
                : off <= goodWindow ? RhythmGrade.Good : RhythmGrade.Miss;

            if (grade == RhythmGrade.Perfect)
            {
                ActivateBeatDash();
                flowMeter = Mathf.Min(maxFlowMeter, flowMeter + 20f);
                if (scoreLink != null) scoreLink.AddGrooveHit(true);
            }
            else if (grade == RhythmGrade.Good)
            {
                ActivateBeatDash();
                flowMeter = Mathf.Min(maxFlowMeter, flowMeter + 10f);
                if (scoreLink != null) scoreLink.AddGrooveHit(false);
            }
            else
            {
                flowMeter = Mathf.Max(0, flowMeter - 10f); // Штраф за действие вне ритма
            }
            try { onRhythmAction?.Invoke(grade); } catch (Exception e) { Debug.LogWarning($"[FPC] onRhythmAction: {e.Message}"); }
            return grade;
        }

        /// <summary>Реакция на джаджменты игры: Miss бьёт по Groove, чистый додж — растит.</summary>
        private void OnScoreJudgement(HitJudgement j, int combo)
        {
            if (j == HitJudgement.Miss)
                flowMeter = Mathf.Max(0f, flowMeter - damageFlowPenalty);
            else if (j == HitJudgement.Perfect300)
                flowMeter = Mathf.Min(maxFlowMeter, flowMeter + dodgeFlowBonus);
        }

        private void ActivateBeatDash()
        {
            isBeatDashing = true;
            beatDashTimer = beatDashDuration;

            // Короткий панч камеры с возвратом в исходную позицию (не в ноль!)
            if (playerCamera != null)
            {
                if (!hasBaseCamPos) { baseCamLocalPos = playerCamera.localPosition; hasBaseCamPos = true; }
                if (beatDashTween != null && beatDashTween.IsActive()) beatDashTween.Kill();
                playerCamera.localPosition = baseCamLocalPos + Vector3.forward * 0.12f;
                beatDashTween = playerCamera.transform.DOLocalMove(baseCamLocalPos, 0.18f).SetEase(Ease.OutCubic);
            }
        }

        private void TriggerLandingShake()
        {
            float timeSinceLastLanding = Time.time - lastLandingTime;
            if (enableComboSystem && timeSinceLastLanding < comboWindow && timeSinceLastLanding > 0.05f)
            {
                landingCombo++;
            }
            else
            {
                landingCombo = 1;
            }
            lastLandingTime = Time.time;

            // Проверка, было ли приземление в ритм (по треку, если музыка играет)
            float minDiff = BeatOffsetSeconds;

            float rhythmBonus = 1f;
            if (minDiff <= perfectWindow)
            {
                rhythmBonus = 1.5f;
                flowMeter = Mathf.Min(maxFlowMeter, flowMeter + 15f);
                if (scoreLink != null) scoreLink.AddGrooveHit(true);
                try { onRhythmAction?.Invoke(RhythmGrade.Perfect); } catch {}
            }
            else if (minDiff <= goodWindow)
            {
                rhythmBonus = 1.2f;
                flowMeter = Mathf.Min(maxFlowMeter, flowMeter + 8f);
                if (scoreLink != null) scoreLink.AddGrooveHit(false);
                try { onRhythmAction?.Invoke(RhythmGrade.Good); } catch {}
            }

            float impactIntensity = Mathf.Clamp01(Mathf.Abs(moveDirection.y) / 15f);
            float comboMultiplier = enableComboSystem ? Mathf.Pow(comboIntensityMultiplier, landingCombo - 1) : 1f;
            float finalIntensity = landingShakeIntensity * impactIntensity * rhythmBonus * comboMultiplier;

            if (shakeTween != null && shakeTween.IsActive()) shakeTween.Kill();

            shakePhase = 0f;
            isShaking = true;

            shakeTween = DOTween.To(() => currentShakeIntensity, x => currentShakeIntensity = x, finalIntensity, 0.05f).SetEase(Ease.OutQuad);
            shakeTween.OnComplete(() =>
            {
                shakeTween = DOTween.To(() => currentShakeIntensity, x => currentShakeIntensity = x, 0f, landingShakeDuration)
                    .SetEase(shakeEase)
                    .OnComplete(() =>
                    {
                        isShaking = false;
                        currentShakeIntensity = 0f;
                        landingShakeOffset = Vector3.zero;
                    });
            });
        }

        // ================= PUBLIC API =================

        public void SetControl(bool newState) { SetLookControl(newState); SetMoveControl(newState); }
        public void SetLookControl(bool newState) { isLook = newState; }
        public void SetMoveControl(bool newState) { isMove = newState; }

        public void SetCursorVisibility(bool newVisibility)
        {
            Cursor.lockState = newVisibility ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = newVisibility;
        }

        public void ResetLandingCombo()
        {
            landingCombo = 0;
            lastLandingTime = -1f;
            flowMeter = 0;
            jumpQueued = false;
            jumpBufferTimer = 0f;
        }

        public void TriggerManualShake(float intensity = 1f)
        {
            if (shakeTween != null && shakeTween.IsActive()) shakeTween.Kill();
            shakePhase = 0f;
            isShaking = true;

            shakeTween = DOTween.To(() => currentShakeIntensity, x => currentShakeIntensity = x, landingShakeIntensity * intensity, 0.05f).SetEase(Ease.OutQuad);
            shakeTween.OnComplete(() =>
            {
                shakeTween = DOTween.To(() => currentShakeIntensity, x => currentShakeIntensity = x, 0f, landingShakeDuration)
                    .SetEase(shakeEase)
                    .OnComplete(() =>
                    {
                        isShaking = false;
                        currentShakeIntensity = 0f;
                        landingShakeOffset = Vector3.zero;
                    });
            });
        }
    }
}