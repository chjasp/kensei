using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kensei.Editor
{
    public static class KenseiMeleeAttackSetup
    {
        private const string ScenePath = "Assets/Scenes/Kensei.unity";
        private const string SourceControllerPath = "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/AC_Polygon_Masculine.controller";
        private const string TargetControllerFolder = "Assets/Animation/Controllers";
        private const string TargetControllerPath = "Assets/Animation/Controllers/AC_Polygon_Masculine_Combat.controller";
        private const string WeaponAssetPath = "Assets/Data/Weapons/Katana.asset";

        private const string CombatLayerName = "CombatLayer";
        private const string EmptyStateName = "Empty";
        private const string Attack01StateName = "Attack_01";
        private const string Attack02StateName = "Attack_02";
        private const string Attack03StateName = "Attack_03";

        private const string AttackTriggerParameter = "AttackTrigger";
        private const string ComboStepParameter = "ComboStep";
        private const string InCombatParameter = "InCombat";

        private const float KatanaComboResetTime = 0.7f;

        private const string Attack01ClipAssetPath =
            "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/LightCombo01/A_Attack_LightCombo01A_Sword.fbx";
        private const string Attack02ClipAssetPath =
            "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/LightCombo01/A_Attack_LightCombo01B_Sword.fbx";
        private const string Attack03ClipAssetPath =
            "Assets/Synty/AnimationSwordCombat/Animations/Polygon/Attack/LightCombo01/A_Attack_LightCombo01C_Sword.fbx";

        private const string Attack01ClipName = "A_Attack_LightCombo01A_Sword";
        private const string Attack02ClipName = "A_Attack_LightCombo01B_Sword";
        private const string Attack03ClipName = "A_Attack_LightCombo01C_Sword";

        [MenuItem("Tools/Kensei/Apply Melee Attack Setup")]
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
                Debug.LogError($"Kensei melee attack setup failed: {exception}");
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
            AnimatorController combatControllerAsset = EnsureCombatAnimatorController();
            PatchCombatAnimatorController(combatControllerAsset);

            WeaponData katana = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponAssetPath);
            if (katana == null)
            {
                throw new InvalidOperationException($"Could not load WeaponData at '{WeaponAssetPath}'.");
            }

            katana.comboResetTime = KatanaComboResetTime;
            EditorUtility.SetDirty(katana);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                throw new InvalidOperationException($"Could not open scene at '{ScenePath}'.");
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                throw new InvalidOperationException("Could not find a Player-tagged GameObject in Kensei scene.");
            }

            Animator animator = player.GetComponentInChildren<Animator>(includeInactive: true);
            if (animator == null)
            {
                throw new InvalidOperationException("Could not resolve a player Animator.");
            }

            HitBox hitBox = player.GetComponentInChildren<HitBox>(includeInactive: true);
            if (hitBox == null)
            {
                throw new InvalidOperationException("Could not resolve a player HitBox.");
            }

            HealthSystem healthSystem = GetOrAddComponent<HealthSystem>(player);
            CombatController combatController = GetOrAddComponent<CombatController>(player);
            MeleeAttackSystem meleeAttackSystem = GetOrAddComponent<MeleeAttackSystem>(player);
            DebugCombatInput debugCombatInput = GetOrAddComponent<DebugCombatInput>(player);
            PlayerMovementController movementController = player.GetComponent<PlayerMovementController>();
            CharacterController characterController = player.GetComponent<CharacterController>();

            animator.runtimeAnimatorController = combatControllerAsset;
            animator.applyRootMotion = false;

            hitBox.SetWeaponData(katana);

            combatController.SetReferences(healthSystem, hitBox, katana, movementController, animator);
            meleeAttackSystem.SetReferences(
                combatController,
                animator,
                hitBox,
                katana,
                movementController,
                characterController);
            debugCombatInput.SetAttackSystem(meleeAttackSystem);

            if (combatController.CurrentState != CombatState.Dead)
            {
                combatController.SetState(CombatState.Idle);
            }

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(hitBox);
            EditorUtility.SetDirty(healthSystem);
            EditorUtility.SetDirty(combatController);
            EditorUtility.SetDirty(meleeAttackSystem);
            EditorUtility.SetDirty(debugCombatInput);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Kensei melee attack setup completed.");
        }

        private static AnimatorController EnsureCombatAnimatorController()
        {
            AnimatorController existingTarget = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
            if (existingTarget != null)
            {
                return existingTarget;
            }

            AnimatorController sourceController = AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath);
            if (sourceController == null)
            {
                throw new InvalidOperationException(
                    $"Could not load source animator controller at '{SourceControllerPath}'.");
            }

            EnsureFolder("Assets/Animation");
            EnsureFolder(TargetControllerFolder);

            if (!AssetDatabase.CopyAsset(SourceControllerPath, TargetControllerPath))
            {
                throw new InvalidOperationException(
                    $"Failed to clone animator controller from '{SourceControllerPath}' to '{TargetControllerPath}'.");
            }

            AssetDatabase.ImportAsset(TargetControllerPath, ImportAssetOptions.ForceUpdate);
            AnimatorController cloned = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
            if (cloned == null)
            {
                throw new InvalidOperationException($"Failed to load cloned controller at '{TargetControllerPath}'.");
            }

            return cloned;
        }

        private static void PatchCombatAnimatorController(AnimatorController controller)
        {
            EnsureParameter(controller, AttackTriggerParameter, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, ComboStepParameter, AnimatorControllerParameterType.Int, defaultInt: 0);
            EnsureParameter(controller, InCombatParameter, AnimatorControllerParameterType.Bool, defaultBool: false);

            AnimationClip attack01Clip = LoadClipFromAsset(Attack01ClipAssetPath, Attack01ClipName);
            AnimationClip attack02Clip = LoadClipFromAsset(Attack02ClipAssetPath, Attack02ClipName);
            AnimationClip attack03Clip = LoadClipFromAsset(Attack03ClipAssetPath, Attack03ClipName);

            int combatLayerIndex = EnsureCombatLayer(controller);
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer combatLayer = layers[combatLayerIndex];
            combatLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            combatLayer.defaultWeight = 0f;
            combatLayer.avatarMask = null;
            combatLayer.syncedLayerIndex = -1;
            combatLayer.syncedLayerAffectsTiming = false;
            combatLayer.iKPass = false;

            AnimatorStateMachine stateMachine = combatLayer.stateMachine;
            if (stateMachine == null)
            {
                stateMachine = new AnimatorStateMachine { name = CombatLayerName };
                AssetDatabase.AddObjectToAsset(stateMachine, controller);
                combatLayer.stateMachine = stateMachine;
            }

            AnimatorState emptyState = EnsureState(stateMachine, EmptyStateName, new Vector3(240f, 180f, 0f));
            AnimatorState attack01State = EnsureState(stateMachine, Attack01StateName, new Vector3(540f, 80f, 0f));
            AnimatorState attack02State = EnsureState(stateMachine, Attack02StateName, new Vector3(820f, 180f, 0f));
            AnimatorState attack03State = EnsureState(stateMachine, Attack03StateName, new Vector3(1100f, 280f, 0f));

            emptyState.motion = null;
            attack01State.motion = attack01Clip;
            attack02State.motion = attack02Clip;
            attack03State.motion = attack03Clip;
            stateMachine.defaultState = emptyState;

            HashSet<AnimatorState> managedStates = new HashSet<AnimatorState>
            {
                emptyState,
                attack01State,
                attack02State,
                attack03State
            };

            RemoveManagedTransitions(emptyState, managedStates);
            RemoveManagedTransitions(attack01State, managedStates);
            RemoveManagedTransitions(attack02State, managedStates);
            RemoveManagedTransitions(attack03State, managedStates);

            AnimatorStateTransition emptyToAttack01 = emptyState.AddTransition(attack01State);
            ConfigureAttackTransition(emptyToAttack01, comboStep: 1);

            AnimatorStateTransition attack01ToAttack02 = attack01State.AddTransition(attack02State);
            ConfigureAttackTransition(attack01ToAttack02, comboStep: 2);

            AnimatorStateTransition attack02ToAttack03 = attack02State.AddTransition(attack03State);
            ConfigureAttackTransition(attack02ToAttack03, comboStep: 3);

            ConfigureReturnToEmptyTransition(attack01State.AddTransition(emptyState));
            ConfigureReturnToEmptyTransition(attack02State.AddTransition(emptyState));
            ConfigureReturnToEmptyTransition(attack03State.AddTransition(emptyState));

            layers[combatLayerIndex] = combatLayer;
            controller.layers = layers;

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType,
            bool defaultBool = false,
            int defaultInt = 0)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                AnimatorControllerParameter existing = parameters[index];
                if (!string.Equals(existing.name, parameterName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (existing.type == parameterType)
                {
                    return;
                }

                controller.RemoveParameter(existing);
                break;
            }

            AnimatorControllerParameter parameter = new AnimatorControllerParameter
            {
                name = parameterName,
                type = parameterType,
                defaultBool = defaultBool,
                defaultInt = defaultInt
            };
            controller.AddParameter(parameter);
        }

        private static int EnsureCombatLayer(AnimatorController controller)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int index = 0; index < layers.Length; index++)
            {
                if (string.Equals(layers[index].name, CombatLayerName, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            AnimatorStateMachine stateMachine = new AnimatorStateMachine { name = CombatLayerName };
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            AnimatorControllerLayer combatLayer = new AnimatorControllerLayer
            {
                name = CombatLayerName,
                stateMachine = stateMachine,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
                avatarMask = null
            };

            controller.AddLayer(combatLayer);
            return controller.layers.Length - 1;
        }

        private static AnimatorState EnsureState(
            AnimatorStateMachine stateMachine,
            string stateName,
            Vector3 position)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int index = 0; index < states.Length; index++)
            {
                AnimatorState state = states[index].state;
                if (state != null && string.Equals(state.name, stateName, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return stateMachine.AddState(stateName, position);
        }

        private static void RemoveManagedTransitions(
            AnimatorState fromState,
            HashSet<AnimatorState> managedStates)
        {
            AnimatorStateTransition[] transitions = fromState.transitions;
            for (int index = transitions.Length - 1; index >= 0; index--)
            {
                AnimatorStateTransition transition = transitions[index];
                if (transition == null || transition.destinationState == null)
                {
                    continue;
                }

                if (managedStates.Contains(transition.destinationState))
                {
                    fromState.RemoveTransition(transition);
                }
            }
        }

        private static void ConfigureAttackTransition(AnimatorStateTransition transition, int comboStep)
        {
            transition.hasExitTime = false;
            transition.exitTime = 0f;
            transition.duration = 0f;
            transition.offset = 0f;
            transition.hasFixedDuration = true;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, AttackTriggerParameter);
            transition.AddCondition(AnimatorConditionMode.Equals, comboStep, ComboStepParameter);
        }

        private static void ConfigureReturnToEmptyTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = true;
            transition.exitTime = 0.98f;
            transition.duration = 0.06f;
            transition.offset = 0f;
            transition.hasFixedDuration = true;
            transition.interruptionSource = TransitionInterruptionSource.None;
            transition.orderedInterruption = true;
            transition.canTransitionToSelf = false;
        }

        private static AnimationClip LoadClipFromAsset(string assetPath, string clipName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is AnimationClip clip && string.Equals(clip.name, clipName, StringComparison.Ordinal))
                {
                    return clip;
                }
            }

            throw new InvalidOperationException(
                $"Could not find clip '{clipName}' in asset '{assetPath}'.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            if (separator <= 0)
            {
                throw new InvalidOperationException($"Invalid folder path: '{path}'.");
            }

            string parent = path.Substring(0, separator);
            string name = path.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
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
