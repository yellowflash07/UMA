using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UMA;


[CustomEditor(typeof(SlotLibrary))]
[CanEditMultipleObjects]
public class SlotLibraryEditor : Editor
{
	private SerializedObject m_Object;
	private SlotLibrary slotLibrary;
	private SerializedProperty m_SlotDataAssetCount;

	private const string kArraySizePath = "slotElementList.Array.size";
	private const string kArrayData = "slotElementList.Array.data[{0}]";

	private bool canUpdate;
	private bool isDirty;

	public void OnEnable()
	{
		m_Object = new SerializedObject(target);
		slotLibrary = m_Object.targetObject as SlotLibrary;
		m_SlotDataAssetCount = m_Object.FindProperty(kArraySizePath);
	}


	private SlotDataAsset[] GetSlotDataAssetArray()
	{

		int arrayCount = m_SlotDataAssetCount.intValue;
		SlotDataAsset[] SlotDataAssetArray = new SlotDataAsset[arrayCount];

		for (int i = 0; i < arrayCount; i++)
		{

			SlotDataAssetArray[i] = m_Object.FindProperty(string.Format(kArrayData, i)).objectReferenceValue as SlotDataAsset;

		}
		return SlotDataAssetArray;

	}

	private void SetSlotDataAsset(int index, SlotDataAsset slotElement)
	{
		m_Object.FindProperty(string.Format(kArrayData, index)).objectReferenceValue = slotElement;
		isDirty = true;
	}

	private SlotDataAsset GetSlotDataAssetAtIndex(int index)
	{
		return m_Object.FindProperty(string.Format(kArrayData, index)).objectReferenceValue as SlotDataAsset;
	}

	private void AddSlotDataAsset(SlotDataAsset slotElement)
	{
		m_SlotDataAssetCount.intValue++;
		SetSlotDataAsset(m_SlotDataAssetCount.intValue - 1, slotElement);
	}


	private void RemoveSlotDataAssetAtIndex(int index)
	{

		for (int i = index; i < m_SlotDataAssetCount.intValue - 1; i++)
		{

			SetSlotDataAsset(i, GetSlotDataAssetAtIndex(i + 1));
		}

		m_SlotDataAssetCount.intValue--;

	}

	private void DropAreaGUI(Rect dropArea)
	{

		var evt = Event.current;

		if (evt.type == EventType.DragUpdated)
		{
			if (dropArea.Contains(evt.mousePosition))
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
			}
		}

		if (evt.type == EventType.DragPerform)
		{
			if (dropArea.Contains(evt.mousePosition))
			{
				DragAndDrop.AcceptDrag();

				UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences as UnityEngine.Object[];
				for (int i = 0; i < draggedObjects.Length; i++)
				{
					if (draggedObjects[i])
					{
						SlotDataAsset tempSlotDataAsset = draggedObjects[i] as SlotDataAsset;
						if (tempSlotDataAsset)
						{
							AddSlotDataAsset(tempSlotDataAsset);
							continue;
						}

						var path = AssetDatabase.GetAssetPath(draggedObjects[i]);
						if (System.IO.Directory.Exists(path))
						{
							var assetFiles = System.IO.Directory.GetFiles(path, "*.asset");
							foreach (var assetFile in assetFiles)
							{
								tempSlotDataAsset = AssetDatabase.LoadAssetAtPath(assetFile, typeof(SlotDataAsset)) as SlotDataAsset;
								if (tempSlotDataAsset)
								{
									AddSlotDataAsset(tempSlotDataAsset);
								}
							}
						}
					}
				}
			}
		}
	}

	public override void OnInspectorGUI()
	{
		m_Object.Update();

		GUILayout.Label("slotElementList", EditorStyles.boldLabel);

		SlotDataAsset[] slotElementList = GetSlotDataAssetArray();

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Order by Name"))
		{
			canUpdate = false;

			List<SlotDataAsset> SlotDataAssetTemp = slotElementList.ToList();

			//Make sure there's no invalid data
			for (int i = 0; i < SlotDataAssetTemp.Count; i++)
			{
				if (SlotDataAssetTemp[i] == null)
				{
					SlotDataAssetTemp.RemoveAt(i);
					i--;
				}
			}

			SlotDataAssetTemp.Sort((x, y) => x.name.CompareTo(y.name));

			for (int i = 0; i < SlotDataAssetTemp.Count; i++)
			{
				SetSlotDataAsset(i, SlotDataAssetTemp[i]);
			}

		}

		if (GUILayout.Button("Update List"))
		{
			isDirty = true;
			canUpdate = false;
		}


		GUILayout.EndHorizontal();

		GUILayout.Space(20);
		Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
		GUI.Box(dropArea, "Drag Slots here");
		GUILayout.Space(20);


		for (int i = 0; i < m_SlotDataAssetCount.intValue; i++)
		{
			GUILayout.BeginHorizontal();

			SlotDataAsset result = EditorGUILayout.ObjectField(slotElementList[i], typeof(SlotDataAsset), true) as SlotDataAsset;

			if (GUI.changed && canUpdate)
			{
				SetSlotDataAsset(i, result);
			}

			if (GUILayout.Button("-", GUILayout.Width(20.0f)))
			{
				canUpdate = false;
				RemoveSlotDataAssetAtIndex(i);
			}

			GUILayout.EndHorizontal();

			if (i == m_SlotDataAssetCount.intValue - 1)
			{
				canUpdate = true;

				if (isDirty)
				{
					slotLibrary.UpdateDictionary();
					isDirty = false;
				}
			}
		}

		DropAreaGUI(dropArea);

		if (GUILayout.Button("Add SlotDataAsset"))
		{
			AddSlotDataAsset(null);
		}

		if (GUILayout.Button("Clear List"))
		{
			m_SlotDataAssetCount.intValue = 0;
		}

		if (GUILayout.Button("Remove Invalid Slot Data"))
		{
			RemoveInvalidSlotDataAsset(slotElementList);
		}

		if (GUILayout.Button("Validate Texture Name Lists"))
		{
			ValidateSlotDataAssetTextureNameLists(slotElementList);
		}

		m_Object.ApplyModifiedProperties();

	}

	private void ValidateSlotDataAssetTextureNameLists(SlotDataAsset[] slotElementList)
	{
		var uma = GameObject.Find("UMA");
		if (uma == null) return;
		var generators = uma.GetComponentsInChildren<UMAGeneratorBase>();
		if (generators.Length == 0) return;
		string[] defaultTextureNameList = generators[0].textureNameList;

		for (int i = m_SlotDataAssetCount.intValue - 1; i >= 0; i--)
		{
			if (slotElementList[i])
			{
				if (slotElementList[i].textureNameList == null || slotElementList[i].textureNameList.Length == 0)
				{
					ValidateSlotDataAssetTextureNameList(defaultTextureNameList, slotElementList[i]);
				}
			}
		}
	}

	private void ValidateSlotDataAssetTextureNameList(string[] defaultTextureNameList, SlotDataAsset SlotDataAsset)
	{
		bool valid = true;
		for(int i = 0; i < defaultTextureNameList.Length; i++ )
		{
			valid = valid && SlotDataAsset.materialSample.HasProperty(defaultTextureNameList[i]);
		}
		if( !valid )
		{
			var properties = ShaderUtil.GetPropertyCount(SlotDataAsset.materialSample.shader);
			var newTextureNameList = new List<string>(16);
			for(int i = 0; i < properties; i++ )
			{
				if( ShaderUtil.GetPropertyType(SlotDataAsset.materialSample.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv )
				{
					newTextureNameList.Add(ShaderUtil.GetPropertyName(SlotDataAsset.materialSample.shader, i));
				}
			}
			Debug.LogWarning("Fixed SlotDataAsset: " + SlotDataAsset.slotName);
			SlotDataAsset.textureNameList = newTextureNameList.ToArray();
			EditorUtility.SetDirty(SlotDataAsset);
		}
	}

	private void RemoveInvalidSlotDataAsset(SlotDataAsset[] slotElementList)
	{
		for (int i = m_SlotDataAssetCount.intValue - 1; i >= 0; i--)
		{
			if (slotElementList[i] == null)
			{
				RemoveSlotDataAssetAtIndex(i);
			}
		}
	}
}
