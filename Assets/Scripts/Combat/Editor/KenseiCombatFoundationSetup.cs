using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kensei.Editor
{
    public static class KenseiCombatFoundationSetup
    {
        private const string ScenePath = "Assets/Scenes/Kensei.unity";
        private const string PlayerLayerName = "PlayerHurtBox";
        private const string EnemyLayerName = "EnemyHurtBox";
        private const string WeaponLayerName = "Weapon";
        private const string PlayerHurtBoxObjectName = "PlayerHurtBox";
        private const string WeaponHitBoxObjectName = "KatanaHitBox";
        private const string WeaponDataRootFolder = "Assets/Data";
        private const string WeaponDataFolder = "Assets/Data/Weapons";
        private const string WeaponAssetPath = "Assets/Data/Weapons/Katana.asset";
        private const string WeaponPrefabPath = "Assets/Synty/PolygonSamuraiEmpire/Prefabs/Weapons/SM_Wep_Sword_01.prefab";
        private const string CombatAnimatorControllerAssetPath = "Assets/Animation/Controllers/AC_Polygon_Masculine_Combat.controller";
        private const string BaseAnimatorControllerAssetPath =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller";
        private const string PlayerVisualRootName = "SM_Chr_Samurai_Male_01";
        private static readonly Vector3 WeaponPositionOffset = Vector3.zero;
        private static readonly Vector3 WeaponRotationOffset = Vector3.zero;

        private static readonly string[] RightHandCandidates =
        {
            "RightHand",
            "Right Hand",
            "R_Hand",
            "R Hand",
            "Hand_R",
            "hand_r",
            "Bip001 R Hand",
            "mixamorig:RightHand",
            "j_r_hand"
        };

        [MenuItem("Tools/Kensei/Apply Combat Foundation Setup")]
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
                Debug.LogError($"Kensei combat foundation setup failed: {exception}");
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
            int playerLayer = EnsureLayer(PlayerLayerName);
            int enemyLayer = EnsureLayer(EnemyLayerName);
            int weaponLayer = EnsureLayer(WeaponLayerName);

            ConfigureCollisionMatrix(playerLayer, enemyLayer, weaponLayer);
            WeaponData katana = EnsureKatanaAsset();
            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
            if (weaponPrefab == null)
            {
                throw new InvalidOperationException($"Could not load weapon prefab at '{WeaponPrefabPath}'.");
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException($"Could not open scene at '{ScenePath}'.");
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                throw new InvalidOperationException("Could not find a Player-tagged GameObject in the Kensei scene.");
            }

            HealthSystem healthSystem = GetOrAddComponent<HealthSystem>(player);
            CombatController combatController = GetOrAddComponent<CombatController>(player);
            PlayerMovementController movementController = player.GetComponent<PlayerMovementController>();
            Animator animator = player.GetComponentInChildren<Animator>(includeInactive: true);
            EnsureCombatAnimatorController(animator);
            Transform rightHandBone = ResolveRightHandTransform(player.transform);
            WeaponMount weaponMount = EnsureWeaponMount(player, rightHandBone, weaponPrefab);
            Transform weaponTransform = weaponMount != null ? weaponMount.EnsureWeaponMounted() : null;

            HurtBox hurtBox = EnsurePlayerHurtBox(player.transform, playerLayer);
            hurtBox.SetHealthSystem(healthSystem);

            HitBox hitBox = EnsureWeaponHitBox(player.transform, weaponTransform, weaponLayer);
            hitBox.SetSourceRoot(player.transform);
            hitBox.SetTargetLayers(LayerMask.GetMask(EnemyLayerName));
            hitBox.SetWeaponData(katana);
            hitBox.DisableHitBox();

            combatController.SetReferences(healthSystem, hitBox, katana, movementController, animator);
            combatController.SetState(CombatState.Idle);

            CombatFoundationTest foundationTest = GetOrAddComponent<CombatFoundationTest>(player);
            foundationTest.SetPlayerHealthForTesting(healthSystem);

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(healthSystem);
            EditorUtility.SetDirty(combatController);
            EditorUtility.SetDirty(hurtBox);
            EditorUtility.SetDirty(hitBox);
            EditorUtility.SetDirty(foundationTest);
            if (weaponMount != null)
            {
                EditorUtility.SetDirty(weaponMount);
            }
            if (weaponTransform != null)
            {
                EditorUtility.SetDirty(weaponTransform.gameObject);
            }
            if (katana != null)
            {
                EditorUtility.SetDirty(katana);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Kensei combat foundation setup completed.");
        }

        private static void EnsureCombatAnimatorController(Animator animator)
        {
            if (animator == null)
            {
                Debug.LogWarning("Could not resolve player Animator for combat setup.");
                return;
            }

            RuntimeAnimatorController combatController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CombatAnimatorControllerAssetPath);
            RuntimeAnimatorController fallbackController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BaseAnimatorControllerAssetPath);

            RuntimeAnimatorController targetController = combatController != null ? combatController : fallbackController;
            if (targetController == null)
            {
                Debug.LogWarning(
                    $"Could not load animator controllers at '{CombatAnimatorControllerAssetPath}' or '{BaseAnimatorControllerAssetPath}'.");
                return;
            }

            if (!ReferenceEquals(animator.runtimeAnimatorController, targetController))
            {
                animator.runtimeAnimatorController = targetController;
            }

            if (combatController == null)
            {
                Debug.LogWarning(
                    $"Combat animator controller missing at '{CombatAnimatorControllerAssetPath}'. " +
                    $"Falling back to '{BaseAnimatorControllerAssetPath}'.");
            }
        }

        private static int EnsureLayer(string layerName)
        {
            int existingLayerIndex = LayerMask.NameToLayer(layerName);
            if (existingLayerIndex >= 0)
            {
                return existingLayerIndex;
            }

            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                throw new InvalidOperationException("Could not load ProjectSettings/TagManager.asset.");
            }

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layersProperty = tagManager.FindProperty("layers");
            if (layersProperty == null || !layersProperty.isArray)
            {
                throw new InvalidOperationException("Could not access layers property from TagManager.");
            }

            for (int layerIndex = 8; layerIndex < 32; layerIndex++)
            {
                SerializedProperty layerProperty = layersProperty.GetArrayElementAtIndex(layerIndex);
                if (!string.IsNullOrEmpty(layerProperty.stringValue))
                {
                    continue;
                }

                layerProperty.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                return layerIndex;
            }

            throw new InvalidOperationException($"No available user layer slot for '{layerName}'.");
        }

        private static void ConfigureCollisionMatrix(int playerLayer, int enemyLayer, int weaponLayer)
        {
            Physics.IgnoreLayerCollision(playerLayer, playerLayer, true);
            Physics.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            Physics.IgnoreLayerCollision(weaponLayer, playerLayer, false);
            Physics.IgnoreLayerCollision(weaponLayer, enemyLayer, false);

            UnityEngine.Object[] dynamicsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (dynamicsAssets != null && dynamicsAssets.Length > 0)
            {
                EditorUtility.SetDirty(dynamicsAssets[0]);
            }
        }

        private static WeaponData EnsureKatanaAsset()
        {
            if (!AssetDatabase.IsValidFolder(WeaponDataRootFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }

            if (!AssetDatabase.IsValidFolder(WeaponDataFolder))
            {
                AssetDatabase.CreateFolder(WeaponDataRootFolder, "Weapons");
            }

            WeaponData existing = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponAssetPath);
            if (existing != null)
            {
                return existing;
            }

            UnityEngine.Object existingMainAsset = AssetDatabase.LoadMainAssetAtPath(WeaponAssetPath);
            if (existingMainAsset != null)
            {
                throw new InvalidOperationException(
                    $"Asset already exists at '{WeaponAssetPath}' but is not a WeaponData. Delete or fix it, then re-run setup.");
            }

            WeaponData katana = ScriptableObject.CreateInstance<WeaponData>();
            katana.weaponName = "Katana";
            katana.baseDamage = 20f;
            katana.attackRange = 2.2f;
            katana.comboResetTime = 0.8f;
            katana.parryWindowDuration = 0.4f;
            katana.damageType = DamageType.LightMelee;

            AssetDatabase.CreateAsset(katana, WeaponAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(WeaponAssetPath, ImportAssetOptions.ForceUpdate);

            WeaponData loadedKatana = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponAssetPath);
            if (loadedKatana == null)
            {
                throw new InvalidOperationException($"Failed to load WeaponData at '{WeaponAssetPath}' after creation.");
            }

            return loadedKatana;
        }

        private static HurtBox EnsurePlayerHurtBox(Transform playerRoot, int playerLayer)
        {
            Transform hurtBoxTransform = playerRoot.Find(PlayerHurtBoxObjectName);
            if (hurtBoxTransform == null)
            {
                GameObject hurtBoxObject = new GameObject(PlayerHurtBoxObjectName);
                hurtBoxTransform = hurtBoxObject.transform;
                hurtBoxTransform.SetParent(playerRoot, worldPositionStays: false);
            }

            GameObject hurtBoxGameObject = hurtBoxTransform.gameObject;
            hurtBoxGameObject.layer = playerLayer;
            hurtBoxTransform.localPosition = Vector3.zero;
            hurtBoxTransform.localRotation = Quaternion.identity;
            hurtBoxTransform.localScale = Vector3.one;

            CapsuleCollider collider = GetOrAddComponent<CapsuleCollider>(hurtBoxGameObject);
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.95f, 0f);
            collider.radius = 0.35f;
            collider.height = 1.7f;
            collider.direction = 1;

            return GetOrAddComponent<HurtBox>(hurtBoxGameObject);
        }

        private static WeaponMount EnsureWeaponMount(GameObject player, Transform handBone, GameObject weaponPrefab)
        {
            WeaponMount weaponMount = GetOrAddComponent<WeaponMount>(player);
            weaponMount.SetMountConfiguration(handBone, weaponPrefab, WeaponPositionOffset, WeaponRotationOffset);
            Transform weaponTransform = weaponMount.EnsureWeaponMounted();
            if (weaponTransform == null && handBone != null && weaponPrefab != null)
            {
                GameObject weaponObject = PrefabUtility.InstantiatePrefab(weaponPrefab, handBone) as GameObject;
                if (weaponObject == null)
                {
                    weaponObject = UnityEngine.Object.Instantiate(weaponPrefab, handBone, false);
                }

                weaponObject.name = weaponPrefab.name;
                weaponTransform = weaponObject.transform;
                weaponTransform.localPosition = WeaponPositionOffset;
                weaponTransform.localRotation = Quaternion.Euler(WeaponRotationOffset);
            }

            return weaponMount;
        }

        private static HitBox EnsureWeaponHitBox(Transform playerRoot, Transform weaponTransform, int weaponLayer)
        {
            Transform parent = weaponTransform != null ? weaponTransform : ResolveRightHandTransform(playerRoot);
            if (parent == null)
            {
                parent = ResolveVisualRoot(playerRoot);
            }

            if (parent == null)
            {
                parent = playerRoot;
            }

            Transform hitBoxTransform = FindChildByName(playerRoot, WeaponHitBoxObjectName);
            if (hitBoxTransform == null)
            {
                GameObject hitBoxObject = new GameObject(WeaponHitBoxObjectName);
                hitBoxTransform = hitBoxObject.transform;
                hitBoxTransform.SetParent(parent, worldPositionStays: false);
            }
            else if (hitBoxTransform.parent != parent)
            {
                hitBoxTransform.SetParent(parent, worldPositionStays: false);
            }

            hitBoxTransform.localPosition = new Vector3(0f, 0.45f, 0f);
            hitBoxTransform.localRotation = Quaternion.identity;
            hitBoxTransform.localScale = Vector3.one;

            GameObject hitBoxGameObject = hitBoxTransform.gameObject;
            hitBoxGameObject.layer = weaponLayer;

            CapsuleCollider collider = GetOrAddComponent<CapsuleCollider>(hitBoxGameObject);
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.radius = 0.06f;
            collider.height = 0.9f;
            collider.direction = 1;

            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(hitBoxGameObject);
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            return GetOrAddComponent<HitBox>(hitBoxGameObject);
        }

        private static Transform ResolveRightHandTransform(Transform playerRoot)
        {
            Transform visualRoot = ResolveVisualRoot(playerRoot);
            if (visualRoot == null)
            {
                return null;
            }

            Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int nameIndex = 0; nameIndex < RightHandCandidates.Length; nameIndex++)
            {
                string candidate = RightHandCandidates[nameIndex];
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform current = transforms[transformIndex];
                    if (string.Equals(current.name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return current;
                    }
                }
            }

            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                string normalizedName = NormalizeName(transforms[transformIndex].name);
                if (normalizedName.Contains("righthand") || normalizedName.Contains("rhand"))
                {
                    return transforms[transformIndex];
                }
            }

            return null;
        }

        private static Transform ResolveVisualRoot(Transform playerRoot)
        {
            Transform exact = playerRoot.Find(PlayerVisualRootName);
            if (exact != null)
            {
                return exact;
            }

            return playerRoot.childCount > 0 ? playerRoot.GetChild(0) : playerRoot;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            char[] buffer = new char[value.Length];
            int bufferCount = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character))
                {
                    continue;
                }

                buffer[bufferCount++] = char.ToLowerInvariant(character);
            }

            return new string(buffer, 0, bufferCount);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            int childCount = root.childCount;
            for (int index = 0; index < childCount; index++)
            {
                Transform match = FindChildByName(root.GetChild(index), childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
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
