using DotsAndBoxes.Gameplay.Audio;
using DotsAndBoxes.Gameplay;
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.EditorTools
{
    public static class AudioSystemInstaller
    {
        private const string BOOTSTRAP_SCENE_PATH = "Assets/01_Main/01_Scenes/BootStrap.unity";
        private const string IN_GAME_SCENE_PATH = "Assets/01_Main/01_Scenes/InGame.unity";
        private const string PREFAB_FOLDER_PATH = "Assets/01_Main/03_Prefabs/System";
        private const string PREFAB_PATH = PREFAB_FOLDER_PATH + "/AudioSystem.prefab";
        private const string AUDIO_FOLDER_PATH = "Assets/01_Main/04_Resource/Audio";
        private const string MIXER_FOLDER_PATH = AUDIO_FOLDER_PATH + "/Mixer";
        private const string MIXER_PATH = MIXER_FOLDER_PATH + "/MainAudioMixer.mixer";
        private const string BOARD_CUE_FOLDER_PATH = AUDIO_FOLDER_PATH + "/Que/Board";
        private const string EDGE_PREVIEW_CUE_PATH = BOARD_CUE_FOLDER_PATH + "/EdgePreview_AudioCue.asset";
        private const string EDGE_CONFIRMED_CUE_PATH = BOARD_CUE_FOLDER_PATH + "/EdgeConfirmed_AudioCue.asset";
        private const string BOX_COMPLETED_CUE_PATH = BOARD_CUE_FOLDER_PATH + "/BoxCompleted_AudioCue.asset";
        private const string TURN_CHANGED_CUE_PATH = BOARD_CUE_FOLDER_PATH + "/TurnChanged_AudioCue.asset";

        [MenuItem("Tools/Dots And Boxes/Install Audio System")]
        public static void Install()
        {
            EnsureFolder(PREFAB_FOLDER_PATH);
            EnsureFolder(MIXER_FOLDER_PATH);

            AudioMixer audioMixer = CreateAudioMixer();
            GameObject audioSystemPrefab = CreateOrUpdatePrefab(audioMixer);

            InstallPrefabInBootstrap(audioSystemPrefab);
            InstallGameBoardAudioFeedback();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("AudioSystem과 게임보드 효과음 설치가 완료되었습니다. BgmPlaylistPlayer에 BGM 클립을 할당해 주세요.");
        }

        public static void InstallBatch()
        {
            try
            {
                Install();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static AudioMixer CreateAudioMixer()
        {
            AudioMixer existingMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);

            if (existingMixer != null)
            {
                EnsureExposedVolumeParameters(existingMixer);
                return existingMixer;
            }

            Assembly unityEditorAssembly = typeof(Editor).Assembly;
            Type controllerType = unityEditorAssembly.GetType("UnityEditor.Audio.AudioMixerController");

            if (controllerType == null)
            {
                Debug.LogWarning("AudioMixerController 형식을 찾을 수 없어 Mixer 생성을 건너뜁니다.");
                return null;
            }

            MethodInfo createMethod = controllerType.GetMethod(
                "CreateMixerControllerAtPath" ,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            object controller = createMethod?.Invoke(null , new object[] { MIXER_PATH });
            AudioMixer audioMixer = controller as AudioMixer;

            if (audioMixer == null)
            {
                Debug.LogWarning("MainAudioMixer를 생성하지 못했습니다. Source 볼륨 제어로 동작합니다.");
                return null;
            }

            object masterGroup = controllerType.GetProperty(
                "masterGroup" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(controller);

            object bgmGroup = CreateMixerGroup(controllerType , controller , "BGM" , masterGroup);
            object sfxGroup = CreateMixerGroup(controllerType , controller , "SFX" , masterGroup);

            CreateMixerGroup(controllerType , controller , "UI" , sfxGroup);

            EnsureExposedVolumeParameters(audioMixer);

            EditorUtility.SetDirty(audioMixer);
            AssetDatabase.SaveAssets();

            return audioMixer;
        }

        private static object CreateMixerGroup(
            Type controllerType ,
            object controller ,
            string groupName ,
            object parentGroup)
        {
            MethodInfo createGroupMethod = controllerType.GetMethod(
                "CreateNewGroup" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            MethodInfo addChildMethod = controllerType.GetMethod(
                "AddChildToParent" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            object group = createGroupMethod?.Invoke(controller , new object[] { groupName , false });

            if (group != null && parentGroup != null)
            {
                addChildMethod?.Invoke(controller , new[] { group , parentGroup });
            }

            return group;
        }

        private static void EnsureExposedVolumeParameters(AudioMixer audioMixer)
        {
            if (audioMixer == null)
            {
                return;
            }

            Type controllerType = audioMixer.GetType();
            object controller = audioMixer;

            ExposeGroupVolume(controllerType , controller , FindMixerGroup(audioMixer , "Master") , "MasterVolume");
            ExposeGroupVolume(controllerType , controller , FindMixerGroup(audioMixer , "BGM") , "BgmVolume");
            ExposeGroupVolume(controllerType , controller , FindMixerGroup(audioMixer , "SFX") , "SfxVolume");

            EditorUtility.SetDirty(audioMixer);
            AssetDatabase.SaveAssets();
        }

        private static void ExposeGroupVolume(
            Type controllerType ,
            object controller ,
            AudioMixerGroup group ,
            string parameterName)
        {
            if (group == null)
            {
                Debug.LogWarning($"{parameterName}에 연결할 AudioMixerGroup을 찾지 못했습니다.");
                return;
            }

            Type groupType = group.GetType();
            MethodInfo getVolumeGuidMethod = groupType.GetMethod(
                "GetGUIDForVolume" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            object volumeGuid = getVolumeGuidMethod?.Invoke(group , null);

            if (volumeGuid == null)
            {
                Debug.LogWarning($"{group.name} 그룹의 Volume GUID를 찾지 못했습니다.");
                return;
            }

            PropertyInfo exposedParametersProperty = controllerType.GetProperty(
                "exposedParameters" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Array exposedParameters = exposedParametersProperty?.GetValue(controller) as Array;

            if (TryRenameExposedParameter(exposedParameters , volumeGuid , parameterName))
            {
                exposedParametersProperty?.SetValue(controller , exposedParameters);
                return;
            }

            Type exposedParameterType = exposedParametersProperty?.PropertyType.GetElementType();

            if (exposedParameterType == null)
            {
                Debug.LogWarning("AudioMixer 노출 파라미터 형식을 찾지 못했습니다.");
                return;
            }

            int currentCount = exposedParameters?.Length ?? 0;
            Array updatedParameters = Array.CreateInstance(exposedParameterType , currentCount + 1);

            if (exposedParameters != null)
            {
                Array.Copy(exposedParameters , updatedParameters , currentCount);
            }

            object newParameter = Activator.CreateInstance(exposedParameterType);

            exposedParameterType.GetField(
                "guid" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(newParameter , volumeGuid);

            exposedParameterType.GetField(
                "name" ,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(newParameter , parameterName);

            updatedParameters.SetValue(newParameter , currentCount);
            exposedParametersProperty?.SetValue(controller , updatedParameters);
        }

        private static bool TryRenameExposedParameter(
            Array exposedParameters ,
            object volumeGuid ,
            string parameterName)
        {
            if (exposedParameters == null)
            {
                return false;
            }

            for (int i = 0; i < exposedParameters.Length; i++)
            {
                object exposedParameter = exposedParameters.GetValue(i);
                Type exposedParameterType = exposedParameter.GetType();

                FieldInfo guidField = exposedParameterType.GetField(
                    "guid" ,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                FieldInfo nameField = exposedParameterType.GetField(
                    "name" ,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                object exposedGuid = guidField?.GetValue(exposedParameter);

                if (exposedGuid == null || !exposedGuid.Equals(volumeGuid))
                {
                    continue;
                }

                nameField?.SetValue(exposedParameter , parameterName);
                exposedParameters.SetValue(exposedParameter , i);
                return true;
            }

            return false;
        }

        private static GameObject CreateOrUpdatePrefab(AudioMixer audioMixer)
        {
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);

            if (existingPrefab != null)
            {
                return existingPrefab;
            }

            GameObject temporaryRoot = new GameObject("AudioSystem");

            try
            {
                AudioProvider audioProvider = temporaryRoot.AddComponent<AudioProvider>();
                BgmPlaylistPlayer bgmPlaylistPlayer = temporaryRoot.AddComponent<BgmPlaylistPlayer>();
                SfxPlayer sfxPlayer = temporaryRoot.AddComponent<SfxPlayer>();

                AudioSource bgmSourceA = CreateAudioSource(temporaryRoot.transform , "BgmSourceA");
                AudioSource bgmSourceB = CreateAudioSource(temporaryRoot.transform , "BgmSourceB");
                AudioSource sfxSource = CreateAudioSource(temporaryRoot.transform , "SfxSource");
                AudioSource uiSource = CreateAudioSource(temporaryRoot.transform , "UiSource");

                AssignMixerGroups(audioMixer , bgmSourceA , bgmSourceB , sfxSource , uiSource);
                ConfigureBgmPlayer(bgmPlaylistPlayer , bgmSourceA , bgmSourceB);
                ConfigureSfxPlayer(sfxPlayer , sfxSource , uiSource);
                ConfigureProvider(audioProvider , bgmPlaylistPlayer , sfxPlayer , audioMixer);

                return PrefabUtility.SaveAsPrefabAsset(temporaryRoot , PREFAB_PATH);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporaryRoot);
            }
        }

        private static AudioSource CreateAudioSource(Transform parentTrans , string objectName)
        {
            GameObject sourceObj = new GameObject(objectName);
            sourceObj.transform.SetParent(parentTrans , false);

            AudioSource audioSource = sourceObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            return audioSource;
        }

        private static void AssignMixerGroups(
            AudioMixer audioMixer ,
            AudioSource bgmSourceA ,
            AudioSource bgmSourceB ,
            AudioSource sfxSource ,
            AudioSource uiSource)
        {
            if (audioMixer == null)
            {
                return;
            }

            AudioMixerGroup bgmGroup = FindMixerGroup(audioMixer , "BGM");
            AudioMixerGroup sfxGroup = FindMixerGroup(audioMixer , "SFX");
            AudioMixerGroup uiGroup = FindMixerGroup(audioMixer , "UI");

            bgmSourceA.outputAudioMixerGroup = bgmGroup;
            bgmSourceB.outputAudioMixerGroup = bgmGroup;
            sfxSource.outputAudioMixerGroup = sfxGroup;
            uiSource.outputAudioMixerGroup = uiGroup != null ? uiGroup : sfxGroup;
        }

        private static AudioMixerGroup FindMixerGroup(AudioMixer audioMixer , string groupName)
        {
            AudioMixerGroup[] groups = audioMixer.FindMatchingGroups(groupName);

            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].name == groupName)
                {
                    return groups[i];
                }
            }

            return null;
        }

        private static void ConfigureBgmPlayer(
            BgmPlaylistPlayer bgmPlaylistPlayer ,
            AudioSource bgmSourceA ,
            AudioSource bgmSourceB)
        {
            SerializedObject serializedPlayer = new SerializedObject(bgmPlaylistPlayer);

            serializedPlayer.FindProperty("_bgmSourceA").objectReferenceValue = bgmSourceA;
            serializedPlayer.FindProperty("_bgmSourceB").objectReferenceValue = bgmSourceB;
            serializedPlayer.FindProperty("_playMode").enumValueIndex = (int)BGM_PLAY_MODE_ENUM.SHUFFLE;
            serializedPlayer.FindProperty("_playOnStart").boolValue = true;
            serializedPlayer.FindProperty("_crossFadeDuration").floatValue = 1.5f;

            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSfxPlayer(
            SfxPlayer sfxPlayer ,
            AudioSource sfxSource ,
            AudioSource uiSource)
        {
            SerializedObject serializedPlayer = new SerializedObject(sfxPlayer);

            serializedPlayer.FindProperty("_sfxSource").objectReferenceValue = sfxSource;
            serializedPlayer.FindProperty("_uiSource").objectReferenceValue = uiSource;

            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProvider(
            AudioProvider audioProvider ,
            BgmPlaylistPlayer bgmPlaylistPlayer ,
            SfxPlayer sfxPlayer ,
            AudioMixer audioMixer)
        {
            SerializedObject serializedProvider = new SerializedObject(audioProvider);

            serializedProvider.FindProperty("_bgmPlaylistPlayer").objectReferenceValue = bgmPlaylistPlayer;
            serializedProvider.FindProperty("_sfxPlayer").objectReferenceValue = sfxPlayer;
            serializedProvider.FindProperty("_audioMixer").objectReferenceValue = audioMixer;

            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallPrefabInBootstrap(GameObject audioSystemPrefab)
        {
            if (audioSystemPrefab == null)
            {
                throw new InvalidOperationException("AudioSystem 프리팹을 생성하지 못했습니다.");
            }

            Scene bootstrapScene = SceneManager.GetSceneByPath(BOOTSTRAP_SCENE_PATH);
            bool wasAlreadyLoaded = bootstrapScene.IsValid() && bootstrapScene.isLoaded;

            if (!wasAlreadyLoaded)
            {
                bootstrapScene = EditorSceneManager.OpenScene(BOOTSTRAP_SCENE_PATH , OpenSceneMode.Additive);
            }

            try
            {
                if (HasAudioProvider(bootstrapScene))
                {
                    return;
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(audioSystemPrefab , bootstrapScene) as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException("BootStrap 씬에 AudioSystem을 생성하지 못했습니다.");
                }

                instance.name = "AudioSystem";
                EditorSceneManager.MarkSceneDirty(bootstrapScene);
                EditorSceneManager.SaveScene(bootstrapScene);
            }
            finally
            {
                if (!wasAlreadyLoaded)
                {
                    EditorSceneManager.CloseScene(bootstrapScene , true);
                }
            }
        }

        private static bool HasAudioProvider(Scene scene)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();

            for (int i = 0; i < rootObjects.Length; i++)
            {
                if (rootObjects[i].GetComponent<AudioProvider>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void InstallGameBoardAudioFeedback()
        {
            AudioCueData edgePreviewCue = LoadRequiredCue(EDGE_PREVIEW_CUE_PATH);
            AudioCueData edgeConfirmedCue = LoadRequiredCue(EDGE_CONFIRMED_CUE_PATH);
            AudioCueData boxCompletedCue = LoadRequiredCue(BOX_COMPLETED_CUE_PATH);
            AudioCueData turnChangedCue = LoadRequiredCue(TURN_CHANGED_CUE_PATH);

            Scene inGameScene = SceneManager.GetSceneByPath(IN_GAME_SCENE_PATH);
            bool wasAlreadyLoaded = inGameScene.IsValid() && inGameScene.isLoaded;

            if ( !wasAlreadyLoaded )
            {
                inGameScene = EditorSceneManager.OpenScene(IN_GAME_SCENE_PATH , OpenSceneMode.Additive);
            }

            try
            {
                GameBoardUI gameBoardUI = FindGameBoardUI(inGameScene);

                if ( gameBoardUI == null )
                {
                    throw new InvalidOperationException("InGame 씬에서 GameBoardUI를 찾지 못했습니다.");
                }

                GameBoardAudioFeedback audioFeedback = gameBoardUI.GetComponent<GameBoardAudioFeedback>();

                if ( audioFeedback == null )
                {
                    audioFeedback = gameBoardUI.gameObject.AddComponent<GameBoardAudioFeedback>();
                }

                SerializedObject serializedFeedback = new SerializedObject(audioFeedback);

                serializedFeedback.FindProperty("_edgePreviewCue").objectReferenceValue = edgePreviewCue;
                serializedFeedback.FindProperty("_edgeConfirmedCue").objectReferenceValue = edgeConfirmedCue;
                serializedFeedback.FindProperty("_boxCompletedCue").objectReferenceValue = boxCompletedCue;
                serializedFeedback.FindProperty("_turnChangedCue").objectReferenceValue = turnChangedCue;
                serializedFeedback.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedGameBoardUI = new SerializedObject(gameBoardUI);

                serializedGameBoardUI.FindProperty("_audioFeedback").objectReferenceValue = audioFeedback;
                serializedGameBoardUI.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(audioFeedback);
                EditorUtility.SetDirty(gameBoardUI);
                EditorSceneManager.MarkSceneDirty(inGameScene);
                EditorSceneManager.SaveScene(inGameScene);
            }
            finally
            {
                if ( !wasAlreadyLoaded )
                {
                    EditorSceneManager.CloseScene(inGameScene , true);
                }
            }
        }

        private static GameBoardUI FindGameBoardUI(Scene scene)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();

            for ( int i = 0; i < rootObjects.Length; i++ )
            {
                GameBoardUI gameBoardUI = rootObjects[i].GetComponentInChildren<GameBoardUI>(true);

                if ( gameBoardUI != null )
                {
                    return gameBoardUI;
                }
            }

            return null;
        }

        private static AudioCueData LoadRequiredCue(string cuePath)
        {
            AudioCueData cueData = AssetDatabase.LoadAssetAtPath<AudioCueData>(cuePath);

            if ( cueData == null )
            {
                throw new InvalidOperationException($"AudioCueData를 찾지 못했습니다: {cuePath}");
            }

            return cueData;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parentPath = folderPath.Substring(0 , folderPath.LastIndexOf('/'));
            string folderName = folderPath.Substring(folderPath.LastIndexOf('/') + 1);

            EnsureFolder(parentPath);
            AssetDatabase.CreateFolder(parentPath , folderName);
        }
    }
}
