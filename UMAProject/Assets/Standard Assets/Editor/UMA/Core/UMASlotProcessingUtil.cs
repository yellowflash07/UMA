﻿//	============================================================
//	Name:		UMATextureImporterUtil
//	Author: 	Joen Joensen (@UnLogick)
//	============================================================
#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UMA;


namespace UMAEditor
{
	public static class UMASlotProcessingUtil
	{
		public static SlotDataAsset CreateSlotData(string slotFolder, string assetFolder, string assetName, SkinnedMeshRenderer mesh, Material material, SkinnedMeshRenderer prefabMesh)
		{
			if (!System.IO.Directory.Exists(slotFolder + '/' + assetFolder))
			{
				System.IO.Directory.CreateDirectory(slotFolder + '/' + assetFolder);
			}

			if (!System.IO.Directory.Exists(slotFolder + '/' + assetName))
			{
				System.IO.Directory.CreateDirectory(slotFolder + '/' + assetName);
			}

			GameObject tempGameObject = UnityEngine.Object.Instantiate(mesh.transform.parent.gameObject) as GameObject;
			PrefabUtility.DisconnectPrefabInstance(tempGameObject);
			var resultingSkinnedMeshes = tempGameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer resultingSkinnedMesh = null;
			foreach (var skinnedMesh in resultingSkinnedMeshes)
			{
				if (skinnedMesh.name == mesh.name)
				{
					resultingSkinnedMesh = skinnedMesh;
				}
			}

			Mesh resultingMesh = resultingSkinnedMesh.sharedMesh;
			if (prefabMesh != null)
			{
				resultingMesh = SeamRemoval.PerformSeamRemoval(resultingSkinnedMesh, prefabMesh, 0.0001f);
				resultingSkinnedMesh.sharedMesh = resultingMesh;
				SkinnedMeshAligner.AlignBindPose(prefabMesh, resultingSkinnedMesh);
			}

			var usedBonesDictionary = CompileUsedBonesDictionary(resultingMesh);
			if (usedBonesDictionary.Count != resultingSkinnedMesh.bones.Length)
			{
				resultingMesh = BuildNewReduceBonesMesh(resultingMesh, usedBonesDictionary);
			}

			AssetDatabase.CreateAsset(resultingMesh, slotFolder + '/' + assetName + '/' + mesh.name + ".asset");

			tempGameObject.name = mesh.transform.parent.gameObject.name;
			Transform[] transformList = tempGameObject.GetComponentsInChildren<Transform>();

			GameObject newObject = new GameObject();

			for (int i = 0; i < transformList.Length; i++)
			{
				if (transformList[i].name == "Global")
				{
					transformList[i].parent = newObject.transform;
				}
				else if (transformList[i].name == mesh.name)
				{
					transformList[i].parent = newObject.transform;
				}
			}

			GameObject.DestroyImmediate(tempGameObject);
			resultingSkinnedMesh = newObject.GetComponentInChildren<SkinnedMeshRenderer>();
			if (resultingSkinnedMesh)
			{
				resultingSkinnedMesh.bones = BuildNewReducedBonesList(resultingSkinnedMesh.bones, usedBonesDictionary);
				resultingSkinnedMesh.sharedMesh = resultingMesh;
			}

			var skinnedResult = UnityEditor.PrefabUtility.CreatePrefab(slotFolder + '/' + assetName + '/' + assetName + "_Skinned.prefab", newObject);
			GameObject.DestroyImmediate(newObject);

			var meshgo = skinnedResult.transform.Find(mesh.name);
			var finalMeshRenderer = meshgo.GetComponent<SkinnedMeshRenderer>();

			var slot = ScriptableObject.CreateInstance<SlotDataAsset>();
			slot.slotName = assetName;
			slot.materialSample = material;
			slot.UpdateMeshData(finalMeshRenderer);
			AssetDatabase.CreateAsset(slot, slotFolder + '/' + assetName + '/' + assetName + "_Slot.asset");
			AssetDatabase.SaveAssets();
			return slot;
		}

		public static void OptimizeSlotDataMesh(SkinnedMeshRenderer smr)
		{
			if (smr == null) return;
			var mesh = smr.sharedMesh;

			var usedBonesDictionary = CompileUsedBonesDictionary(mesh);
			var smrOldBones = smr.bones.Length;
			if (usedBonesDictionary.Count != smrOldBones)
			{
				mesh.boneWeights = BuildNewBoneWeights(mesh.boneWeights, usedBonesDictionary);
				mesh.bindposes = BuildNewBindPoses(mesh.bindposes, usedBonesDictionary);
				EditorUtility.SetDirty(mesh);
				smr.bones = BuildNewReducedBonesList(smr.bones, usedBonesDictionary);
				EditorUtility.SetDirty(smr);
				Debug.Log(string.Format("Optimized Mesh {0} from {1} bones to {2} bones.", smr.name, smrOldBones, usedBonesDictionary.Count), smr);
			}
		}

		private static Mesh BuildNewReduceBonesMesh(Mesh sourceMesh, Dictionary<int, int> usedBonesDictionary)
		{
			var newMesh = new Mesh();
			newMesh.vertices = sourceMesh.vertices;
			newMesh.uv = sourceMesh.uv;
			newMesh.uv2 = sourceMesh.uv2;
#if !UNITY_4_6
			newMesh.uv3 = sourceMesh.uv3;
			newMesh.uv4 = sourceMesh.uv4;
#endif
			newMesh.tangents = sourceMesh.tangents;
			newMesh.normals = sourceMesh.normals;
			newMesh.name = sourceMesh.name;
			newMesh.colors32 = sourceMesh.colors32;
			newMesh.colors = sourceMesh.colors;
			newMesh.boneWeights = BuildNewBoneWeights(sourceMesh.boneWeights, usedBonesDictionary);
			newMesh.bindposes = BuildNewBindPoses(sourceMesh.bindposes, usedBonesDictionary);
			for (int i = 0; i < sourceMesh.subMeshCount; i++)
			{
				newMesh.SetTriangles(sourceMesh.GetTriangles(i), i);
			}
			return newMesh;
		}

		private static Matrix4x4[] BuildNewBindPoses(Matrix4x4[] bindPoses, Dictionary<int, int> usedBonesDictionary)
		{
			var res = new Matrix4x4[usedBonesDictionary.Count];
			foreach (var entry in usedBonesDictionary)
			{
				res[entry.Value] = bindPoses[entry.Key];
			}
			return res;
		}

		private static BoneWeight[] BuildNewBoneWeights(BoneWeight[] boneWeight, Dictionary<int, int> usedBonesDictionary)
		{
			var res = new BoneWeight[boneWeight.Length];
			for (int i = 0; i < boneWeight.Length; i++)
			{
				UpdateBoneWeight(ref boneWeight[i], ref res[i], usedBonesDictionary);
			}
			return res;
		}

		private static void UpdateBoneWeight(ref BoneWeight source, ref BoneWeight dest, Dictionary<int, int> usedBonesDictionary)
		{
			dest.weight0 = source.weight0;
			dest.weight1 = source.weight1;
			dest.weight2 = source.weight2;
			dest.weight3 = source.weight3;
			dest.boneIndex0 = UpdateBoneIndex(source.boneIndex0, usedBonesDictionary);
			dest.boneIndex1 = UpdateBoneIndex(source.boneIndex1, usedBonesDictionary);
			dest.boneIndex2 = UpdateBoneIndex(source.boneIndex2, usedBonesDictionary);
			dest.boneIndex3 = UpdateBoneIndex(source.boneIndex3, usedBonesDictionary);
		}

		private static int UpdateBoneIndex(int boneIndex, Dictionary<int, int> usedBonesDictionary)
		{
			int res;
			if (usedBonesDictionary.TryGetValue(boneIndex, out res)) return res;
			return 0;
		}

		private static Transform[] BuildNewReducedBonesList(Transform[] bones, Dictionary<int, int> usedBonesDictionary)
		{
			var res = new Transform[usedBonesDictionary.Count];
			foreach (var entry in usedBonesDictionary)
			{
				res[entry.Value] = bones[entry.Key];
			}
			return res;
		}

		private static Dictionary<int, int> CompileUsedBonesDictionary(Mesh resultingMesh)
		{
			var res = new Dictionary<int, int>();
			var boneWeights = resultingMesh.boneWeights;
			for (int i = 0; i < boneWeights.Length; i++)
			{
				AddBoneWeightToUsedBones(res, ref boneWeights[i]);
			}
			return res;
		}

		private static void AddBoneWeightToUsedBones(Dictionary<int, int> res, ref BoneWeight boneWeight)
		{
			if (boneWeight.weight0 > 0 && !res.ContainsKey(boneWeight.boneIndex0)) res.Add(boneWeight.boneIndex0, res.Count);
			if (boneWeight.weight1 > 0 && !res.ContainsKey(boneWeight.boneIndex1)) res.Add(boneWeight.boneIndex1, res.Count);
			if (boneWeight.weight2 > 0 && !res.ContainsKey(boneWeight.boneIndex2)) res.Add(boneWeight.boneIndex2, res.Count);
			if (boneWeight.weight3 > 0 && !res.ContainsKey(boneWeight.boneIndex3)) res.Add(boneWeight.boneIndex3, res.Count);
		}
	}
}
#endif