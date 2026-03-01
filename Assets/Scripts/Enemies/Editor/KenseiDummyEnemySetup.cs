using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kensei.Editor
{
    public static class KenseiDummyEnemySetup
    {
        private const string ScenePath = "Assets/Scenes/Kensei.unity";
        private const string EnemyLayerName = "EnemyHurtBox";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string EnemyPrefabsFolder = "Assets/Prefabs/Enemies";
        private const string DummyPrefabPath = "Assets/Prefabs/Enemies/DummyEnemy.prefab";
        private const string DummyPrefabName = "DummyEnemy";
        private const string VisualRootName = "Visual";
        private const string HurtBoxObjectName = "EnemyHurtBox";
        private const string CharacterPrefabPath =
            "Assets/Synty/PolygonSamuraiEmpire/Prefabs/Characters/SM_Chr_Soldier_Male_01.prefab";
        private const string IdleControllerPath =
            "Assets/Synty/AnimationBaseLocomotion/Samples/Animations/Polygon/Masculine/Idles/AC_Idle.controller";

        [MenuItem("Tools/Kensei/Apply Dummy Enemy Setup")]
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
            catch (Exception exception)
            {
                Debug.LogError($"Kensei dummy enemy setup failed: {exception}");
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
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            if (enemyLayer < 0)
            {
                throw new InvalidOperationException(
                    $"Layer '{EnemyLayerName}' does not exist. Run combat foundation setup first.");
            }

            GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (characterPrefab == null)
            {
                throw new InvalidOperationException($"Could not load character prefab at '{CharacterPrefabPath}'.");
            }

            RuntimeAnimatorController idleController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(IdleControllerPath);
            if (idleController == null)
            {
                throw new InvalidOperationException($"Could not load idle controller at '{IdleControllerPath}'.");
            }

            GameObject dummyPrefab = EnsureDummyEnemyPrefab(enemyLayer, characterPrefab, idleController);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException($"Could not open scene at '{ScenePath}'.");
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                throw new InvalidOperationException("Could not find a Player-tagged object in Kensei scene.");
            }

            EnsureDummyEnemyInstances(scene, dummyPrefab, player.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Kensei dummy enemy setup completed.");
        }

        private static GameObject EnsureDummyEnemyPrefab(
            int enemyLayer,
            GameObject characterPrefab,
            RuntimeAnimatorController idleController)
        {
            EnsureFolder(PrefabsFolder);
            EnsureFolder(EnemyPrefabsFolder);

            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            if (existingPrefab == null)
            {
                GameObject createdRoot = new GameObject(DummyPrefabName);
                try
                {
                    ConfigureDummyPrefabRoot(createdRoot, enemyLayer, characterPrefab, idleController);
                    GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(createdRoot, DummyPrefabPath);
                    if (savedPrefab == null)
                    {
                        throw new InvalidOperationException($"Failed to create prefab at '{DummyPrefabPath}'.");
                    }

                    return savedPrefab;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(createdRoot);
                }
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(DummyPrefabPath);
            try
            {
                ConfigureDummyPrefabRoot(prefabRoot, enemyLayer, characterPrefab, idleController);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, DummyPrefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException($"Failed to save prefab at '{DummyPrefabPath}'.");
                }

                return savedPrefab;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureDummyPrefabRoot(
            GameObject root,
            int enemyLayer,
            GameObject characterPrefab,
            RuntimeAnimatorController idleController)
        {
            if (root == null)
            {
                throw new InvalidOperationException("Cannot configure a null dummy prefab root.");
            }

            root.name = DummyPrefabName;
            root.layer = 0;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            HealthSystem healthSystem = GetOrAddComponent<HealthSystem>(root);
            SetSerializedFloat(healthSystem, "maxHealth", 60f);
            SetSerializedFloat(healthSystem, "invincibilityDuration", 0f);

            Transform visualTransform = EnsureVisualRoot(root.transform, characterPrefab);
            Animator animator = GetOrAddComponent<Animator>(visualTransform.gameObject);
            animator.runtimeAnimatorController = idleController;
            animator.applyRootMotion = false;

            Transform hurtBoxTransform = EnsureChild(root.transform, HurtBoxObjectName);
            hurtBoxTransform.localPosition = Vector3.zero;
            hurtBoxTransform.localRotation = Quaternion.identity;
            hurtBoxTransform.localScale = Vector3.one;
            hurtBoxTransform.gameObject.layer = enemyLayer;

            CapsuleCollider hurtCollider = GetOrAddComponent<CapsuleCollider>(hurtBoxTransform.gameObject);
            hurtCollider.isTrigger = true;
            hurtCollider.direction = 1;
            hurtCollider.center = new Vector3(0f, 0.95f, 0f);
            hurtCollider.radius = 0.35f;
            hurtCollider.height = 1.8f;

            HurtBox hurtBox = GetOrAddComponent<HurtBox>(hurtBoxTransform.gameObject);
            hurtBox.SetHealthSystem(healthSystem);

            DummyEnemyBehavior behavior = GetOrAddComponent<DummyEnemyBehavior>(root);
            behavior.SetReferences(
                healthSystem,
                animator,
                root.GetComponentsInChildren<Renderer>(includeInactive: true));
        }

        private static Transform EnsureVisualRoot(Transform root, GameObject characterPrefab)
        {
            Transform existingVisual = root.Find(VisualRootName);
            if (existingVisual != null)
            {
                return existingVisual;
            }

            GameObject visualObject = PrefabUtility.InstantiatePrefab(characterPrefab, root) as GameObject;
            if (visualObject == null)
            {
                visualObject = UnityEngine.Object.Instantiate(characterPrefab, root, false);
            }

            visualObject.name = VisualRootName;
            Transform visualTransform = visualObject.transform;
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
            return visualTransform;
        }

        private static void EnsureDummyEnemyInstances(Scene scene, GameObject dummyPrefab, Transform playerTransform)
        {
            DummyPlacement[] placements =
            {
                new DummyPlacement("DummyEnemy_01", forwardDistance: 5f, rightDistance: 0f),
                new DummyPlacement("DummyEnemy_02", forwardDistance: 12f, rightDistance: 4f),
                new DummyPlacement("DummyEnemy_03", forwardDistance: 20f, rightDistance: -3f)
            };

            for (int index = 0; index < placements.Length; index++)
            {
                DummyPlacement placement = placements[index];
                GameObject instance = GameObject.Find(placement.name);
                if (instance == null)
                {
                    instance = PrefabUtility.InstantiatePrefab(dummyPrefab, scene) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            $"Failed to instantiate dummy prefab '{dummyPrefab.name}' into scene.");
                    }

                    instance.name = placement.name;
                }

                Vector3 basePosition =
                    playerTransform.position +
                    (playerTransform.forward * placement.forwardDistance) +
                    (playerTransform.right * placement.rightDistance);
                Vector3 snappedPosition = SnapToGround(basePosition);
                instance.transform.SetPositionAndRotation(snappedPosition, ResolveLookRotation(snappedPosition, playerTransform.position));
                instance.transform.localScale = Vector3.one;
                instance.SetActive(true);

                EditorUtility.SetDirty(instance);
            }
        }

        private static Vector3 SnapToGround(Vector3 worldPosition)
        {
            Vector3 rayOrigin = worldPosition + (Vector3.up * 20f);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                worldPosition.y = hit.point.y;
            }

            return worldPosition;
        }

        private static Quaternion ResolveLookRotation(Vector3 worldPosition, Vector3 lookTarget)
        {
            Vector3 lookDirection = lookTarget - worldPosition;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(parent, worldPositionStays: false);
            return child;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int separatorIndex = folderPath.LastIndexOf('/');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"Invalid folder path '{folderPath}'.");
            }

            string parentPath = folderPath.Substring(0, separatorIndex);
            string folderName = folderPath.Substring(separatorIndex + 1);
            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void SetSerializedFloat(UnityEngine.Object targetObject, string propertyPath, float value)
        {
            if (targetObject == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(targetObject);
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Float)
            {
                return;
            }

            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
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

        private readonly struct DummyPlacement
        {
            public readonly string name;
            public readonly float forwardDistance;
            public readonly float rightDistance;

            public DummyPlacement(string name, float forwardDistance, float rightDistance)
            {
                this.name = name;
                this.forwardDistance = forwardDistance;
                this.rightDistance = rightDistance;
            }
        }
    }
}
