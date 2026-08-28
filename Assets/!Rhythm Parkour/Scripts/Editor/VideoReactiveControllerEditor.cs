using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VideoReactiveController))]
public class VideoReactiveControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var t = target as VideoReactiveController;
        EditorGUILayout.Space();
        if (GUILayout.Button("Найти VideoPlayer + Назначить RT"))
        {
            t.SendMessage("ContextFind", null, SendMessageOptions.DontRequireReceiver);
            if (t.videoPlayer != null && t.videoPlayer.targetTexture == null)
            {
                t.SendMessage("TrySetupRenderTexture", null, SendMessageOptions.DontRequireReceiver);
            }
            EditorUtility.SetDirty(t);
        }
        if (GUILayout.Button("Создать Demo материал"))
        {
            t.SendMessage("CreateDemoMaterial", null, SendMessageOptions.DontRequireReceiver);
        }
        EditorGUILayout.HelpBox("1) VideoPlayer должен рендерить в RenderTexture\n2) RenderTexture должна быть в _VideoTex материала\n3) Этот скрипт делает это автоматом каждый кадр.", MessageType.Info);
    }
}
