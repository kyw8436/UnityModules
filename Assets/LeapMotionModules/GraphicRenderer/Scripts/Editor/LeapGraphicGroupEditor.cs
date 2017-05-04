/******************************************************************************
 * Copyright (C) Leap Motion, Inc. 2011-2017.                                 *
 * Leap Motion proprietary and  confidential.                                 *
 *                                                                            *
 * Use subject to the terms of the Leap Motion SDK Agreement available at     *
 * https://developer.leapmotion.com/sdk_agreement, or another agreement       *
 * between Leap Motion and you, your company or other organization.           *
 ******************************************************************************/

using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using Leap.Unity;
using Leap.Unity.Query;

namespace Leap.Unity.GraphicalRenderer {

  public class LeapGuiGroupEditor {
    public const int BUTTON_WIDTH = 30;
    public const int REFRESH_WIDTH = 78;

    private LeapGraphicRenderer _renderer;
    private SerializedObject _serializedObject;

    private SerializedProperty _supportInfo;

    private SerializedProperty _groupProperty;

    private SerializedProperty _multiFeatureList;
    private SerializedProperty _multiRenderingMethod;
    private SerializedProperty _featureTable;
    private ReorderableList _featureList;
    private MonoScript _renderingMethodMonoScript;

    private List<SerializedProperty> _cachedPropertyList;
    private List<float> _cachedPropertyHeights;

    private SerializedProperty _renderingMethod;

    private GenericMenu _addRenderingMethodMenu;
    private GenericMenu _addFeatureMenu;

    public LeapGuiGroupEditor(LeapGraphicRenderer renderer, SerializedObject serializedObject) {
      _renderer = renderer;
      _serializedObject = serializedObject;

      var allTypes = Assembly.GetAssembly(typeof(LeapGraphicRenderer)).GetTypes();
      var allRenderingMethods = allTypes.Query().
                                         Where(t => !t.IsAbstract &&
                                                    !t.IsGenericType &&
                                                     t.IsSubclassOf(typeof(LeapRenderingMethod)));

      _addRenderingMethodMenu = new GenericMenu();
      foreach (var renderingMethod in allRenderingMethods) {
        _addRenderingMethodMenu.AddItem(new GUIContent(LeapGraphicTagAttribute.GetTag(renderingMethod)),
                                        false,
                                        () => {
                                          serializedObject.ApplyModifiedProperties();
                                          Undo.RecordObject(_renderer, "Changed rendering method");
                                          _renderer.editor.ChangeRenderingMethod(renderingMethod, addFeatures: false);
                                          serializedObject.Update();
                                          _renderer.editor.ScheduleEditorUpdate();
                                        });
      }

      var allFeatures = allTypes.Query().
                                 Where(t => !t.IsAbstract &&
                                            !t.IsGenericType &&
                                             t.IsSubclassOf(typeof(LeapGraphicFeatureBase)));

      _addFeatureMenu = new GenericMenu();
      foreach (var feature in allFeatures) {
        _addFeatureMenu.AddItem(new GUIContent(LeapGraphicTagAttribute.GetTag(feature)),
                                false,
                                () => {
                                  serializedObject.ApplyModifiedProperties();
                                  Undo.RecordObject(_renderer, "Added feature");
                                  _renderer.editor.AddFeature(feature);
                                  _serializedObject.Update();
                                });
      }
    }

    public void Invalidate() {
      _featureList = null;
      _renderingMethodMonoScript = null;
    }

    public void DoGuiLayout(SerializedProperty groupProperty) {
      using (new ProfilerSample("Draw Graphic Group")) {
        init(groupProperty);

        drawRendererHeader();

        drawMonoScript();

        drawStatsArea();

        EditorGUILayout.PropertyField(_renderingMethod, includeChildren: true);

        EditorGUILayout.Space();

        drawFeatureHeader();

        _featureList.DoLayoutList();
      }
    }

    private void init(SerializedProperty groupProperty) {
      Assert.IsNotNull(groupProperty);
      _groupProperty = groupProperty;

      _multiFeatureList = _groupProperty.FindPropertyRelative("_features");
      _multiRenderingMethod = _groupProperty.FindPropertyRelative("_renderingMethod");

      _featureTable = MultiTypedListUtil.GetTableProperty(_multiFeatureList);
      Assert.IsNotNull(_featureTable);

      if (_featureList == null || !SerializedProperty.EqualContents(_featureList.serializedProperty, _featureTable)) {
        _featureList = new ReorderableList(_serializedObject,
                                           _featureTable,
                                           draggable: true,
                                           displayHeader: false,
                                           displayAddButton: false,
                                           displayRemoveButton: false);

        _featureList.showDefaultBackground = false;
        _featureList.headerHeight = 0;
        _featureList.elementHeight = EditorGUIUtility.singleLineHeight;
        _featureList.elementHeightCallback = featureHeightCallback;
        _featureList.drawElementCallback = drawFeatureCallback;
        _featureList.onReorderCallback = onReorderFeaturesCallback;
      }

      _renderingMethod = MultiTypedReferenceUtil.GetReferenceProperty(_multiRenderingMethod);
      _supportInfo = _groupProperty.FindPropertyRelative("_supportInfo");

      _cachedPropertyList = new List<SerializedProperty>();
      _cachedPropertyHeights = new List<float>();

      for (int i = 0; i < _featureTable.arraySize; i++) {
        var idIndex = _featureTable.GetArrayElementAtIndex(i);
        var referenceProp = MultiTypedListUtil.GetReferenceProperty(_multiFeatureList, idIndex);
        _cachedPropertyList.Add(referenceProp);

        //Make sure to add one line for the label
        _cachedPropertyHeights.Add(EditorGUI.GetPropertyHeight(referenceProp) + EditorGUIUtility.singleLineHeight);
      }

      _renderingMethod = MultiTypedReferenceUtil.GetReferenceProperty(_multiRenderingMethod);

      if (_renderingMethodMonoScript == null) {
        _renderingMethodMonoScript = AssetDatabase.FindAssets(_renderingMethod.type).
                                           Query().
                                           Where(guid => !string.IsNullOrEmpty(guid)).
                                           Select(guid => AssetDatabase.GUIDToAssetPath(guid)).
                                           Where(path => Path.GetFileNameWithoutExtension(path) == _renderingMethod.type).
                                           Select(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path)).
                                           FirstOrDefault();
      }
    }

    private void drawRendererHeader() {
      Rect rect = EditorGUILayout.GetControlRect(GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight));
      Rect left, right;
      rect.SplitHorizontallyWithRight(out left, out right, BUTTON_WIDTH * 2);

      if (!EditorApplication.isPlaying && PrefabUtility.GetPrefabType(_renderer) != PrefabType.Prefab) {
        var mesher = _renderer.editor.GetSelectedRenderingMethod() as LeapMesherBase;
        if (mesher != null) {

          Color prevColor = GUI.color;
          if (mesher.IsAtlasDirty) {
            GUI.color = Color.yellow;
          }

          Rect middle;
          left.SplitHorizontallyWithRight(out left, out middle, REFRESH_WIDTH);
          if (GUI.Button(middle, "Refresh Atlas", EditorStyles.miniButtonMid)) {
            _serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(_renderer, "Refreshed atlas");
            EditorUtility.SetDirty(_renderer);
            mesher.RebuildAtlas(new ProgressBar());
            _renderer.editor.ScheduleEditorUpdate();
            _serializedObject.Update();
          }

          GUI.color = prevColor;
        }
      }

      EditorGUI.LabelField(left, "Renderer", EditorStyles.miniButtonLeft);
      using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlaying)) {
        if (GUI.Button(right, "v", EditorStyles.miniButtonRight)) {
          _addRenderingMethodMenu.ShowAsContext();
        }
      }
    }

    private void drawStatsArea() {
      using (new EditorGUI.DisabledGroupScope(true)) {
        var graphicList = _groupProperty.FindPropertyRelative("_graphics");
        int count = graphicList.arraySize;
        EditorGUILayout.IntField("Attached Graphic Count", count);
      }
    }

    private void drawMonoScript() {
      using (new EditorGUI.DisabledGroupScope(true)) {
        EditorGUILayout.ObjectField("Rendering Method",
                                    _renderingMethodMonoScript,
                                    typeof(MonoScript),
                                    allowSceneObjects: false);
      }
    }

    private void drawFeatureHeader() {
      Rect rect = EditorGUILayout.GetControlRect(GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight));
      Rect left, middle, right;
      rect.SplitHorizontallyWithRight(out middle, out right, BUTTON_WIDTH);
      middle.SplitHorizontallyWithRight(out left, out middle, BUTTON_WIDTH);

      EditorGUI.LabelField(left, "Graphic Features", EditorStyles.miniButtonLeft);

      using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlaying)) {
        EditorGUI.BeginDisabledGroup(_featureTable.arraySize == 0);
        if (GUI.Button(middle, "-", EditorStyles.miniButtonMid) && _featureList.index >= 0) {
          _serializedObject.ApplyModifiedProperties();
          Undo.RecordObject(_renderer, "Removed Feature");
          EditorUtility.SetDirty(_renderer);
          _renderer.editor.RemoveFeature(_featureList.index);
          _serializedObject.Update();
          init(_groupProperty);
        }
        EditorGUI.EndDisabledGroup();

        if (GUI.Button(right, "+", EditorStyles.miniButtonRight)) {
          _addFeatureMenu.ShowAsContext();
        }
      }
    }

    // Feature list callbacks

    private void drawFeatureHeaderCallback(Rect rect) {
      Rect left, right;
      rect.SplitHorizontallyWithRight(out left, out right, BUTTON_WIDTH);

      EditorGUI.LabelField(left, "Graphic Features", EditorStyles.miniButtonLeft);

      if (GUI.Button(right, "+", EditorStyles.miniButtonRight)) {
        _addFeatureMenu.ShowAsContext();
      }
    }

    private float featureHeightCallback(int index) {
      return _cachedPropertyHeights[index];
    }

    delegate void Action<T1, T2, T3, T4, T5>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5);

    private void drawFeatureCallback(Rect rect, int index, bool isActive, bool isFocused) {
      var featureProperty = _cachedPropertyList[index];

      rect = rect.SingleLine();
      string featureName = LeapGraphicTagAttribute.GetTag(featureProperty.type);

      int lastIndexOf = featureName.LastIndexOf('/');
      if (lastIndexOf >= 0) {
        featureName = featureName.Substring(lastIndexOf + 1);
      }

      GUIContent featureLabel = new GUIContent(featureName);

      Color originalColor = GUI.color;

      if (!EditorApplication.isPlaying &&
          index < _supportInfo.arraySize) {
        var supportInfo = _supportInfo.GetArrayElementAtIndex(index);
        var supportProperty = supportInfo.FindPropertyRelative("support");
        var messageProperty = supportInfo.FindPropertyRelative("message");
        switch ((SupportType)supportProperty.intValue) {
          case SupportType.Warning:
            GUI.color = Color.yellow;
            featureLabel.tooltip = messageProperty.stringValue;
            break;
          case SupportType.Error:
            GUI.color = Color.red;
            featureLabel.tooltip = messageProperty.stringValue;
            break;
        }
      }

      Vector2 size = EditorStyles.label.CalcSize(featureLabel);

      Rect labelRect = rect;
      labelRect.width = size.x;

      GUI.Box(labelRect, "");
      EditorGUI.LabelField(labelRect, featureLabel);
      GUI.color = originalColor;


      rect = rect.NextLine().Indent();
      EditorGUI.PropertyField(rect, featureProperty, includeChildren: true);
    }

    private void onReorderFeaturesCallback(ReorderableList list) {
      _renderer.editor.ScheduleEditorUpdate();
    }
  }
}
