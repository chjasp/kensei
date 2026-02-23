using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
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
            VirtualJoystick joystick = EnsureTouchHud();

            movement.SetDependencies(joystick, cameraTransform, visualRoot);

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(movement);
            EditorUtility.SetDirty(joystick);
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

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            StandaloneInputModule standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule != null)
            {
                Object.DestroyImmediate(standaloneInputModule);
            }
        }

        private static VirtualJoystick EnsureTouchHud()
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

            return joystick;
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
    }
}
