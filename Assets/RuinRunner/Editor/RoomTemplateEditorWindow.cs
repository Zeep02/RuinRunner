#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RuinRunner.EditorTools
{
    public sealed class RoomTemplateEditorWindow : EditorWindow
    {
        private const string RoomFolder = "Assets/RuinRunner/Resources/Rooms";

        private RoomTemplate current;
        private SerializedObject serializedRoom;
        private Vector2 scroll;
        private Texture2D preview;

        [MenuItem("RuinRunner/Room Designer", priority = 4)]
        public static void Open()
        {
            GetWindow<RoomTemplateEditorWindow>("Room Designer");
        }

        public static void GenerateStarterTemplates()
        {
            EnsureRoomFolder();
            foreach (var template in RoomTemplateFactory.CreateStarterTemplates())
            {
                var path = RoomFolder + "/" + template.roomId + ".asset";
                if (AssetDatabase.LoadAssetAtPath<RoomTemplate>(path) == null)
                    AssetDatabase.CreateAsset(template, path);
                else
                    DestroyImmediate(template);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Room Designer] Starter room templates are ready in " + RoomFolder);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ruin Runner Room Designer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "탑다운 XY 전투 방입니다. 장애물은 모두 고형이며 점프/낙하는 없습니다. 출구 방향과 웨이브 번호를 관리하고, 저장 전 접근 검사를 실행하세요.",
                MessageType.Info);

            var selected = (RoomTemplate)EditorGUILayout.ObjectField("Room Template", current, typeof(RoomTemplate), false);
            if (selected != current) SelectRoom(selected);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("새 방 만들기")) CreateRoom();
            if (GUILayout.Button("기본 방 7개 생성")) GenerateStarterTemplates();
            EditorGUILayout.EndHorizontal();

            if (current == null)
            {
                EditorGUILayout.Space(12);
                EditorGUILayout.LabelField("편집할 방 에셋을 선택하세요.");
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            serializedRoom.Update();
            Draw("roomId", "저장 ID");
            Draw("displayName", "표시 이름");
            Draw("selectionWeight", "등장 가중치");
            Draw("bossRoom", "보스 방");
            Draw("preferredExitDirection", "다음 방 방향");
            Draw("automaticWaveSize", "자동 웨이브당 몬스터 수");
            Draw("accentColor", "테마 색상");
            Draw("playerSpawn", "플레이어 시작 위치");
            Draw("roomSize", "방 크기 (가로/세로 월드 단위)");
            Draw("walkableOutline", "전투 구역 윤곽 (비어 있으면 자연형)", true);
            EditorGUILayout.Space(8);
            Draw("platforms", "고형 장애물 목록", true);
            EditorGUILayout.Space(8);
            Draw("enemies", "몬스터 배치 목록", true);
            EditorGUILayout.Space(8);
            Draw("decorations", "환경 오브젝트 (식생/항아리/깃발/화로)", true);
            if (serializedRoom.ApplyModifiedProperties()) ClearPreview();
            DrawLayoutPreview();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("저장"))
            {
                EditorUtility.SetDirty(current);
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button("JSON 내보내기")) ExportJson();
            if (GUILayout.Button("JSON 가져오기")) ImportJson();
            if (GUILayout.Button("접근 검사")) ValidateCurrent();
            EditorGUILayout.EndHorizontal();
        }

        private void ValidateCurrent()
        {
            var issues = RoomLayoutRules.Validate(current);
            EditorUtility.DisplayDialog("탑다운 방 접근 검사", issues.Count == 0
                ? "플레이어 시작점에서 네 방향 입구·출구 접근점, 상자 위치, 적 스폰으로 연결됩니다. 실제 물리 이동 검사는 별도입니다."
                : string.Join("\n", issues), "확인");
        }

        private void DrawLayoutPreview()
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("탑다운 배치 미리보기 — 흰색: 시작, 청록: 상자, 빨강: 적");
            var rect = GUILayoutUtility.GetRect(240f, 220f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(.12f, .19f, .14f));
            var scale = Mathf.Min(rect.width / (current.Size.x + 2f), rect.height / (current.Size.y + 2f));
            if (preview == null) preview = DungeonMapView.CreateFootprint(current);
            GUI.DrawTexture(new Rect(rect.center - current.Size * scale * .5f, current.Size * scale), preview);
            foreach (RoomTravelDirection direction in System.Enum.GetValues(typeof(RoomTravelDirection)))
                DrawPreviewMarker(rect, current.ExitPoint(direction), scale, new Color(1f, .75f, .3f));
            DrawPreviewMarker(rect, current.playerSpawn, scale, Color.white);
            DrawPreviewMarker(rect, RoomLayoutRules.RewardPosition, scale, Color.cyan);
            if (current.enemies != null)
                foreach (var enemy in current.enemies)
                    DrawPreviewMarker(rect, enemy.position, scale, enemy.type == EnemyType.Boss ? Color.magenta : Color.red);
            if (current.decorations != null)
                foreach (var item in current.decorations) DrawPreviewMarker(rect, item.position, scale, Color.green);
        }

        private static Vector2 PreviewPoint(Rect rect, Vector2 point, float scale) =>
            rect.center + new Vector2(point.x, -point.y) * scale;

        private static void DrawPreviewMarker(Rect rect, Vector2 point, float scale, Color color)
        {
            var position = PreviewPoint(rect, point, scale);
            EditorGUI.DrawRect(new Rect(position.x - 4f, position.y - 4f, 8f, 8f), color);
        }

        private void Draw(string propertyName, string label, bool includeChildren = false)
        {
            EditorGUILayout.PropertyField(serializedRoom.FindProperty(propertyName), new GUIContent(label), includeChildren);
        }

        private void SelectRoom(RoomTemplate room)
        {
            ClearPreview();
            current = room;
            serializedRoom = current == null ? null : new SerializedObject(current);
        }

        private void CreateRoom()
        {
            EnsureRoomFolder();
            var path = EditorUtility.SaveFilePanelInProject("새 방 저장", "room_new", "asset", "Resources/Rooms 아래에 저장하세요.", RoomFolder);
            if (string.IsNullOrEmpty(path)) return;

            var room = CreateInstance<RoomTemplate>();
            AssetDatabase.CreateAsset(room, path);
            AssetDatabase.SaveAssets();
            SelectRoom(room);
            Selection.activeObject = room;
        }

        private void ExportJson()
        {
            var path = EditorUtility.SaveFilePanel("방 JSON 내보내기", Application.dataPath, current.roomId, "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, EditorJsonUtility.ToJson(current, true));
        }

        private void ImportJson()
        {
            var path = EditorUtility.OpenFilePanel("방 JSON 가져오기", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            Undo.RecordObject(current, "Import Room JSON");
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(path), current);
            EditorUtility.SetDirty(current);
            serializedRoom.Update();
            ClearPreview();
        }

        private void OnDisable() => ClearPreview();
        private void ClearPreview() 
{ 
if (preview != null) DestroyImmediate(preview); 
preview = null; 
}

        private static void EnsureRoomFolder()
        {
            Directory.CreateDirectory(RoomFolder);
            AssetDatabase.Refresh();
        }
    }
}
#endif
