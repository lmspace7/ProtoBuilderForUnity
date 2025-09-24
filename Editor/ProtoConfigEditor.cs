using UnityEditor;
using UnityEngine;
using System.IO;
using LM.ProtoBuilder;

namespace LM.ProtoBuilder.Editor
{
    [CustomEditor(typeof(ProtoConfig))]
    public class ProtoConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty entriesProp = serializedObject.FindProperty("_entries");
            if (entriesProp == null)
            {
                EditorGUILayout.HelpBox("Entries 필드를 찾을 수 없습니다.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField("프로토 설정 항목", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                for (int i = 0; i < entriesProp.arraySize; i++)
                {
                    SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty nameProp = entryProp.FindPropertyRelative("_name");
                    SerializedProperty srcProp = entryProp.FindPropertyRelative("_sourcePath");
                    SerializedProperty dstProp = entryProp.FindPropertyRelative("_destinationPath");
                    SerializedProperty outProp = entryProp.FindPropertyRelative("_outputCsPath");

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.PropertyField(nameProp, new GUIContent("이름"));

                    DrawPathField_Picker("원본 경로", srcProp);
                    DrawPathField_Picker("대상 경로", dstProp);
                    DrawPathField_Picker("출력 경로", outProp);

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("항목 추가"))
                {
                    entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
                }
                if (entriesProp.arraySize > 0)
                {
                    if (GUILayout.Button("마지막 항목 제거"))
                    {
                        entriesProp.DeleteArrayElementAtIndex(entriesProp.arraySize - 1);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 동기화 + 생성"))
            {
                ProtobufBuilder.GenerateAllProtos();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPathField_Picker(string label, SerializedProperty pathProp)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(pathProp, new GUIContent(label));
            if (GUILayout.Button("...", GUILayout.MaxWidth(30)))
            {
                string selected = EditorUtility.OpenFolderPanel(label, GetProjectRoot(), pathProp.stringValue);
                if (string.IsNullOrEmpty(selected) == false)
                {
                    string relative = ToProject_RelativePath(selected);
                    pathProp.stringValue = relative;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private string GetProjectRoot()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Debug.Log($"Project Root: {projectRoot}");
            return projectRoot;
        }

        private string ToProject_RelativePath(string chosenPath)
        {
            string projectRoot = GetProjectRoot();
            string full = Path.GetFullPath(chosenPath);
            if (full.StartsWith(projectRoot))
            {
                string rel = full.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return rel.Replace('\\', '/');
            }
            return full.Replace('\\', '/');
        }
    }
}