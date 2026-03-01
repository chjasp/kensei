using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kensei.Editor
{
    public static class KenseiTouchLocomotionSetup
    {
        private const string ScenePath = "Assets/Scenes/Kensei.unity";
        private const string SceneCameraName = "Game Camera";
        private const string CanvasName = "TouchHUD";
        private const string JoystickRootName = "MoveJoystick";
        private const string JoystickBackgroundName = "JoystickBackground";
        private const string JoystickHandleName = "JoystickHandle";
        private const string LookDragRegionName = "LookDragRegion";
        private const string AttackButtonName = "AttackButton";
        private const string ParryButtonName = "ParryButton";
        private const string AttackLabelName = "Label";
        private const string ParryLabelName = "Label";
        private const string AttackLabelText = "ATK";
        private const string ParryLabelText = "PRY";
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const string CombatAnimatorControllerAssetPath =
            "Assets/Animation/Controllers/AC_Polygon_Masculine_Combat.controller";
        private const string SyntyAnimatorControllerAssetPath =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller";
        private const string SyntyCharactersModelAssetPath = "Assets/Synty/PolygonSamuraiEmpire/Models/Characters.fbx";
        private const string PlayerActionMapName = "Player";
        private const string MoveActionName = "Move";
        private static readonly Vector2 AttackButtonSize = new Vector2(120f, 120f);
        private static readonly Vector2 AttackButtonOffset = new Vector2(-72f, 72f);
        private static readonly Vector2 ParryButtonSize = new Vector2(80f, 80f);
        private static readonly Vector2 ParryButtonOffset = new Vector2(-172f, 162f);
        private static readonly Color CombatButtonColor = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color CombatLabelColor = new Color(0f, 0f, 0f, 0.88f);

        [MenuItem("Tools/Kensei/Apply Touch Locomotion Setup")]
        public static void ApplyFromMenu()
        {
            ApplyInternal();
        }

        public static void ApplyFromBatch()
        {
            bool success = false;
            try
            {
                ApplyInternal();
                success = true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Kensei touch locomotion setup failed: {exception}");
            }
            finally
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(success ? 0 : 1);
                }
            }
        }

        private static void ApplyInternal()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new System.InvalidOperationException($"Could not open scene at '{ScenePath}'.");
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                throw new System.InvalidOperationException("Could not find a Player-tagged GameObject in Kensei scene.");
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController = player.AddComponent<CharacterController>();
            }
            ConfigureCharacterController(characterController);

            PlayerMovementController movement = player.GetComponent<PlayerMovementController>();
            if (movement == null)
            {
                movement = player.AddComponent<PlayerMovementController>();
            }

            Transform cameraTransform = ResolveCameraTransform();
            Transform visualRoot = ResolveVisualRoot(player.transform);

            EnsureEventSystem();
            VirtualJoystick joystick = EnsureTouchHud(
                player,
                out TouchLookDragRegion lookDragRegion,
                out CombatTouchHUD combatTouchHUD);

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (inputActions == null)
            {
                Debug.LogWarning($"Could not load InputActionAsset at '{InputActionsAssetPath}'. Falling back to joystick-only movement.");
            }
            movement.SetDependencies(
                joystick,
                cameraTransform,
                visualRoot,
                inputActions,
                PlayerActionMapName,
                MoveActionName);
            movement.SetVisualYawOffset(0f);

            Animator animator = EnsureAnimator(visualRoot);
            SyntyLocomotionAnimatorDriver animatorDriver = EnsureAnimatorDriver(
                player,
                animator,
                characterController,
                visualRoot,
                movement);

            GameCameraController cameraController = cameraTransform != null
                ? cameraTransform.GetComponent<GameCameraController>()
                : null;
            if (cameraController != null)
            {
                cameraController.SetTouchLookRegion(lookDragRegion);
                EditorUtility.SetDirty(cameraController);
            }

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(movement);
            EditorUtility.SetDirty(joystick);
            if (animator != null)
            {
                EditorUtility.SetDirty(animator);
            }
            if (animatorDriver != null)
            {
                EditorUtility.SetDirty(animatorDriver);
            }
            if (lookDragRegion != null)
            {
                EditorUtility.SetDirty(lookDragRegion);
            }
            if (combatTouchHUD != null)
            {
                EditorUtility.SetDirty(combatTouchHUD);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("Kensei touch locomotion setup completed.");
        }

        private static void ConfigureCharacterController(CharacterController characterController)
        {
            characterController.center = new Vector3(0f, 0.95f, 0f);
            characterController.height = 1.9f;
            characterController.radius = 0.35f;
            characterController.slopeLimit = 50f;
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.08f;
            characterController.minMoveDistance = 0f;
        }

        private static Transform ResolveCameraTransform()
        {
            GameObject namedCamera = GameObject.Find(SceneCameraName);
            if (namedCamera != null)
            {
                return namedCamera.transform;
            }

            Camera fallbackCamera = Camera.main;
            if (fallbackCamera == null)
            {
                fallbackCamera = Object.FindAnyObjectByType<Camera>();
            }

            return fallbackCamera != null ? fallbackCamera.transform : null;
        }

        private static Transform ResolveVisualRoot(Transform playerTransform)
        {
            Transform exactMatch = playerTransform.Find("SM_Chr_Samurai_Male_01");
            if (exactMatch != null)
            {
                return exactMatch;
            }

            return playerTransform.childCount > 0 ? playerTransform.GetChild(0) : playerTransform;
        }

        private static Animator EnsureAnimator(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                return null;
            }

            Animator animator = visualRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visualRoot.gameObject.AddComponent<Animator>();
            }

            RuntimeAnimatorController combatController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatAnimatorControllerAssetPath);
            RuntimeAnimatorController baseController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SyntyAnimatorControllerAssetPath);

            RuntimeAnimatorController selectedController = ResolvePreferredAnimatorController(
                animator.runtimeAnimatorController,
                combatController,
                baseController);

            if (selectedController == null)
            {
                Debug.LogWarning(
                    $"Could not load animator controllers at '{CombatAnimatorControllerAssetPath}' or '{SyntyAnimatorControllerAssetPath}'.");
            }
            else if (!ReferenceEquals(animator.runtimeAnimatorController, selectedController))
            {
                animator.runtimeAnimatorController = selectedController;
            }

            if (combatController == null && baseController != null && ReferenceEquals(selectedController, baseController))
            {
                Debug.LogWarning(
                    $"Combat animator controller missing at '{CombatAnimatorControllerAssetPath}'. " +
                    $"Falling back to '{SyntyAnimatorControllerAssetPath}'.");
            }

            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                Avatar humanoidAvatar = ResolveHumanoidAvatar();
                if (humanoidAvatar != null)
                {
                    animator.avatar = humanoidAvatar;
                }
                else
                {
                    Debug.LogWarning(
                        $"Could not find a valid humanoid Avatar in '{SyntyCharactersModelAssetPath}'. " +
                        "Assign an Avatar manually on the player Animator if animations do not retarget.");
                }
            }

            animator.applyRootMotion = false;
            animator.enabled = true;
            return animator;
        }

        private static RuntimeAnimatorController ResolvePreferredAnimatorController(
            RuntimeAnimatorController currentController,
            RuntimeAnimatorController combatController,
            RuntimeAnimatorController baseController)
        {
            if (IsCombatController(currentController, combatController))
            {
                return currentController;
            }

            if (currentController == null || IsBaseSyntyController(currentController, baseController))
            {
                if (combatController != null)
                {
                    return combatController;
                }

                return baseController;
            }

            return currentController;
        }

        private static bool IsCombatController(RuntimeAnimatorController controller, RuntimeAnimatorController loadedCombatController)
        {
            if (controller == null)
            {
                return false;
            }

            if (loadedCombatController != null && ReferenceEquals(controller, loadedCombatController))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(controller);
            return !string.IsNullOrEmpty(path)
                   && string.Equals(path, CombatAnimatorControllerAssetPath, System.StringComparison.Ordinal);
        }

        private static bool IsBaseSyntyController(RuntimeAnimatorController controller, RuntimeAnimatorController loadedBaseController)
        {
            if (controller == null)
            {
                return false;
            }

            if (loadedBaseController != null && ReferenceEquals(controller, loadedBaseController))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(controller);
            return !string.IsNullOrEmpty(path)
                   && string.Equals(path, SyntyAnimatorControllerAssetPath, System.StringComparison.Ordinal);
        }

        private static Avatar ResolveHumanoidAvatar()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SyntyCharactersModelAssetPath);
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is Avatar avatar && avatar.isValid && avatar.isHuman)
                {
                    return avatar;
                }
            }

            return null;
        }

        private static SyntyLocomotionAnimatorDriver EnsureAnimatorDriver(
            GameObject player,
            Animator animator,
            CharacterController characterController,
            Transform visualRoot,
            PlayerMovementController movement)
        {
            SyntyLocomotionAnimatorDriver driver = player.GetComponent<SyntyLocomotionAnimatorDriver>();
            if (driver == null)
            {
                driver = player.AddComponent<SyntyLocomotionAnimatorDriver>();
            }

            driver.SetDependencies(animator, characterController, visualRoot, movement);
            return driver;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule[] uiModules = eventSystem.GetComponents<InputSystemUIInputModule>();
            InputSystemUIInputModule inputSystemUiInputModule;
            if (uiModules.Length == 0)
            {
                inputSystemUiInputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            else
            {
                inputSystemUiInputModule = uiModules[0];
                for (int index = 1; index < uiModules.Length; index++)
                {
                    Object.DestroyImmediate(uiModules[index]);
                }
            }

            if (NeedsDefaultUiActions(inputSystemUiInputModule))
            {
                inputSystemUiInputModule.AssignDefaultActions();
                inputSystemUiInputModule.enabled = false;
                inputSystemUiInputModule.enabled = true;
                EditorUtility.SetDirty(inputSystemUiInputModule);
            }

            StandaloneInputModule standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule != null)
            {
                Object.DestroyImmediate(standaloneInputModule);
            }
        }

        private static bool NeedsDefaultUiActions(InputSystemUIInputModule inputSystemUiInputModule)
        {
            if (inputSystemUiInputModule == null || inputSystemUiInputModule.actionsAsset == null)
            {
                return true;
            }

            return inputSystemUiInputModule.point == null ||
                   inputSystemUiInputModule.point.action == null ||
                   inputSystemUiInputModule.leftClick == null ||
                   inputSystemUiInputModule.leftClick.action == null ||
                   inputSystemUiInputModule.move == null ||
                   inputSystemUiInputModule.move.action == null ||
                   inputSystemUiInputModule.submit == null ||
                   inputSystemUiInputModule.submit.action == null ||
                   inputSystemUiInputModule.cancel == null ||
                   inputSystemUiInputModule.cancel.action == null;
        }

        private static VirtualJoystick EnsureTouchHud(
            GameObject player,
            out TouchLookDragRegion lookDragRegion,
            out CombatTouchHUD combatTouchHUD)
        {
            GameObject canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            }

            canvasObject.layer = LayerMask.NameToLayer("UI");

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.localScale = Vector3.one;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster graphicRaycaster = canvasObject.GetComponent<GraphicRaycaster>();
            graphicRaycaster.ignoreReversedGraphics = true;

            GameObject lookDragRegionObject = FindOrCreateChild(canvasObject.transform, LookDragRegionName);
            lookDragRegionObject.layer = LayerMask.NameToLayer("UI");
            RectTransform lookDragRegionRect = EnsureRectTransform(lookDragRegionObject);
            lookDragRegionRect.anchorMin = new Vector2(0.5f, 0f);
            lookDragRegionRect.anchorMax = new Vector2(1f, 1f);
            lookDragRegionRect.pivot = new Vector2(0.5f, 0.5f);
            lookDragRegionRect.anchoredPosition = Vector2.zero;
            lookDragRegionRect.sizeDelta = Vector2.zero;
            lookDragRegionRect.localScale = Vector3.one;

            Image lookDragRegionImage = lookDragRegionObject.GetComponent<Image>();
            if (lookDragRegionImage == null)
            {
                lookDragRegionImage = lookDragRegionObject.AddComponent<Image>();
            }
            lookDragRegionImage.color = new Color(1f, 1f, 1f, 0f);
            lookDragRegionImage.raycastTarget = true;

            if (lookDragRegionObject.GetComponent<CanvasRenderer>() == null)
            {
                lookDragRegionObject.AddComponent<CanvasRenderer>();
            }

            lookDragRegion = lookDragRegionObject.GetComponent<TouchLookDragRegion>();
            if (lookDragRegion == null)
            {
                lookDragRegion = lookDragRegionObject.AddComponent<TouchLookDragRegion>();
            }

            GameObject joystickRootObject = FindOrCreateChild(canvasObject.transform, JoystickRootName);
            joystickRootObject.layer = LayerMask.NameToLayer("UI");
            RectTransform joystickRootRect = EnsureRectTransform(joystickRootObject);
            joystickRootRect.anchorMin = Vector2.zero;
            joystickRootRect.anchorMax = Vector2.zero;
            joystickRootRect.pivot = Vector2.zero;
            joystickRootRect.anchoredPosition = new Vector2(56f, 56f);
            joystickRootRect.sizeDelta = new Vector2(220f, 220f);
            joystickRootRect.localScale = Vector3.one;

            Image joystickInputArea = joystickRootObject.GetComponent<Image>();
            if (joystickInputArea == null)
            {
                joystickInputArea = joystickRootObject.AddComponent<Image>();
            }
            joystickInputArea.color = new Color(1f, 1f, 1f, 0f);
            joystickInputArea.raycastTarget = true;

            VirtualJoystick joystick = joystickRootObject.GetComponent<VirtualJoystick>();
            if (joystick == null)
            {
                joystick = joystickRootObject.AddComponent<VirtualJoystick>();
            }

            SerializedObject joystickSerializedObject = new SerializedObject(joystick);
            SerializedProperty inputRegionProperty = joystickSerializedObject.FindProperty("inputRegion");
            if (inputRegionProperty != null)
            {
                inputRegionProperty.objectReferenceValue = joystickRootRect;
            }

            SerializedProperty allowMouseFallbackProperty = joystickSerializedObject.FindProperty("allowMouseFallback");
            if (allowMouseFallbackProperty != null)
            {
                allowMouseFallbackProperty.boolValue = true;
            }

            joystickSerializedObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject backgroundObject = FindOrCreateChild(joystickRootObject.transform, JoystickBackgroundName);
            backgroundObject.layer = LayerMask.NameToLayer("UI");
            RectTransform backgroundRect = EnsureRectTransform(backgroundObject);
            backgroundRect.anchorMin = new Vector2(0f, 0f);
            backgroundRect.anchorMax = new Vector2(0f, 0f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = new Vector2(110f, 110f);
            backgroundRect.sizeDelta = new Vector2(180f, 180f);
            backgroundRect.localScale = Vector3.one;

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            if (backgroundImage == null)
            {
                backgroundImage = backgroundObject.AddComponent<Image>();
            }
            backgroundImage.color = new Color(1f, 1f, 1f, 0.22f);
            backgroundImage.raycastTarget = false;

            if (backgroundObject.GetComponent<CanvasRenderer>() == null)
            {
                backgroundObject.AddComponent<CanvasRenderer>();
            }

            GameObject handleObject = FindOrCreateChild(backgroundObject.transform, JoystickHandleName);
            handleObject.layer = LayerMask.NameToLayer("UI");
            RectTransform handleRect = EnsureRectTransform(handleObject);
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(90f, 90f);
            handleRect.localScale = Vector3.one;

            Image handleImage = handleObject.GetComponent<Image>();
            if (handleImage == null)
            {
                handleImage = handleObject.AddComponent<Image>();
            }
            handleImage.color = new Color(1f, 1f, 1f, 0.6f);
            handleImage.raycastTarget = false;

            if (handleObject.GetComponent<CanvasRenderer>() == null)
            {
                handleObject.AddComponent<CanvasRenderer>();
            }

            joystick.SetVisualReferences(backgroundRect, handleRect);
            joystick.SetTuning(90f, 0.12f, 20f, true);

            MeleeAttackSystem meleeAttackSystem = player != null ? player.GetComponent<MeleeAttackSystem>() : null;
            CombatController combatController = player != null ? player.GetComponent<CombatController>() : null;
            combatTouchHUD = EnsureCombatTouchHud(canvasObject, meleeAttackSystem, combatController);

            return joystick;
        }

        private static CombatTouchHUD EnsureCombatTouchHud(
            GameObject canvasObject,
            MeleeAttackSystem meleeAttackSystem,
            CombatController combatController)
        {
            if (canvasObject == null)
            {
                return null;
            }

            GameObject attackButtonObject = FindOrCreateChild(canvasObject.transform, AttackButtonName);
            attackButtonObject.layer = LayerMask.NameToLayer("UI");
            RectTransform attackRect = EnsureRectTransform(attackButtonObject);
            attackRect.anchorMin = new Vector2(1f, 0f);
            attackRect.anchorMax = new Vector2(1f, 0f);
            attackRect.pivot = new Vector2(1f, 0f);
            attackRect.anchoredPosition = AttackButtonOffset;
            attackRect.sizeDelta = AttackButtonSize;
            attackRect.localScale = Vector3.one;

            ConfigureCombatButtonVisual(
                attackButtonObject,
                attackRect,
                AttackLabelName,
                AttackLabelText,
                fontSize: 36);

            CombatPointerDownButton attackPointerDownButton =
                GetOrAddComponent<CombatPointerDownButton>(attackButtonObject);

            GameObject parryButtonObject = FindOrCreateChild(canvasObject.transform, ParryButtonName);
            parryButtonObject.layer = LayerMask.NameToLayer("UI");
            RectTransform parryRect = EnsureRectTransform(parryButtonObject);
            parryRect.anchorMin = new Vector2(1f, 0f);
            parryRect.anchorMax = new Vector2(1f, 0f);
            parryRect.pivot = new Vector2(1f, 0f);
            parryRect.anchoredPosition = ParryButtonOffset;
            parryRect.sizeDelta = ParryButtonSize;
            parryRect.localScale = Vector3.one;

            ConfigureCombatButtonVisual(
                parryButtonObject,
                parryRect,
                ParryLabelName,
                ParryLabelText,
                fontSize: 24);

            CombatPointerDownButton parryPointerDownButton =
                GetOrAddComponent<CombatPointerDownButton>(parryButtonObject);

            attackRect.SetAsLastSibling();
            parryRect.SetAsLastSibling();

            CombatTouchHUD combatTouchHUD = GetOrAddComponent<CombatTouchHUD>(canvasObject);
            combatTouchHUD.SetReferences(
                meleeAttackSystem,
                combatController,
                attackPointerDownButton,
                parryPointerDownButton);
            return combatTouchHUD;
        }

        private static void ConfigureCombatButtonVisual(
            GameObject buttonObject,
            RectTransform buttonRect,
            string labelObjectName,
            string labelText,
            int fontSize)
        {
            if (buttonObject.GetComponent<CanvasRenderer>() == null)
            {
                buttonObject.AddComponent<CanvasRenderer>();
            }

            Image buttonImage = GetOrAddComponent<Image>(buttonObject);
            buttonImage.color = CombatButtonColor;
            buttonImage.raycastTarget = true;

            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (circleSprite != null)
            {
                buttonImage.sprite = circleSprite;
                buttonImage.type = Image.Type.Simple;
                buttonImage.preserveAspect = true;
            }

            Button button = GetOrAddComponent<Button>(buttonObject);
            button.transition = Selectable.Transition.None;
            button.targetGraphic = buttonImage;

            GameObject labelObject = FindOrCreateChild(buttonRect, labelObjectName);
            labelObject.layer = LayerMask.NameToLayer("UI");
            RectTransform labelRect = EnsureRectTransform(labelObject);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            labelRect.localScale = Vector3.one;

            if (labelObject.GetComponent<CanvasRenderer>() == null)
            {
                labelObject.AddComponent<CanvasRenderer>();
            }

            Text label = GetOrAddComponent<Text>(labelObject);
            label.text = labelText;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = CombatLabelColor;
            label.raycastTarget = false;
            label.resizeTextForBestFit = false;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;

            Font labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelFont == null)
            {
                labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (labelFont != null)
            {
                label.font = labelFont;
            }
        }

        private static GameObject FindOrCreateChild(Transform parent, string childName)
        {
            Transform existingChild = parent.Find(childName);
            if (existingChild != null)
            {
                if (existingChild is RectTransform)
                {
                    return existingChild.gameObject;
                }

                GameObject replacement = new GameObject(childName, typeof(RectTransform));
                replacement.transform.SetParent(parent, false);
                replacement.transform.SetSiblingIndex(existingChild.GetSiblingIndex());
                Object.DestroyImmediate(existingChild.gameObject);
                return replacement;
            }

            GameObject child = new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static RectTransform EnsureRectTransform(GameObject gameObject)
        {
            RectTransform rectTransform = gameObject.transform as RectTransform;
            if (rectTransform == null)
            {
                throw new System.InvalidOperationException(
                    $"GameObject '{gameObject.name}' must have a RectTransform for touch UI setup.");
            }

            return rectTransform;
        }

        private static T GetOrAddComponent<T>(GameObject targetObject) where T : Component
        {
            T component = targetObject.GetComponent<T>();
            if (component == null)
            {
                component = targetObject.AddComponent<T>();
            }

            return component;
        }
    }
}
