using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {
	private const float viewerMoveThresholdForChunkUpdate = 25f;

	private const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
	
	public LODInfo[] detailLevels;
	public static float maxViewDist;
	public Transform viewer;
	public Material mapMaterial;

	public static Vector2 viewerPosition;
	private Vector2 viewerPositionOld;
	private static MapGenerator _mapGenerator;
	private int chunkSize;
	private int chunkVisibleInViewDst;
	
	Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

	private void Start() {
		_mapGenerator = FindObjectOfType<MapGenerator>();

		maxViewDist = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
		chunkSize = MapGenerator.GetMapChunkSize() - 1;
		chunkVisibleInViewDst = Mathf.RoundToInt(maxViewDist / chunkSize);
		
		UpdateVisibleChunks();
	}

	private void Update() {
		viewerPosition = new Vector2(viewer.position.x, viewer.position.z);

		if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate) {
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunks();
		}
	}

	void UpdateVisibleChunks() {
		for (int i = 0; i < terrainChunksVisibleLastUpdate.Count; i++) {
			terrainChunksVisibleLastUpdate[i].SetVisible(false);
		}
		
		terrainChunksVisibleLastUpdate.Clear();
		
		int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

		for (int yOffset = -chunkVisibleInViewDst; yOffset <= chunkVisibleInViewDst; yOffset++) {
			for (int xOffset = -chunkVisibleInViewDst; xOffset <= chunkVisibleInViewDst; xOffset++) {
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

				if (terrainChunkDictionary.ContainsKey(viewedChunkCoord)) {
					terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();

					if (terrainChunkDictionary[viewedChunkCoord].IsVisible()) {
						terrainChunksVisibleLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
					}
				} else {
					terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
				}
			}
		}
	}
	
	public class TerrainChunk {
		private GameObject meshObject;
		private Vector2 position;
		private Bounds bounds;

		private MapData mapData;

		private MeshRenderer meshRenderer;
		private MeshFilter meshFilter;

		private LODInfo[] detailLevels;
		private LODMesh[] lodMeshes;

		private bool mapDataReceived;
		private int previousLODIndex = -1;
		
		public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material) {
			this.detailLevels = detailLevels;
			position = coord * size;
			bounds = new Bounds(position, Vector2.one * size);
			Vector3 positionV3 = new Vector3(position.x, 0, position.y);
			
			meshObject = new GameObject("Terrain Chunk");
			meshRenderer = meshObject.AddComponent<MeshRenderer>();
			meshFilter = meshObject.AddComponent<MeshFilter>();
			meshRenderer.material = material;
			
			meshObject.transform.position = positionV3;
			meshObject.transform.parent = parent;
			SetVisible(false);
			
			lodMeshes = new LODMesh[detailLevels.Length];

			for (int i = 0; i < detailLevels.Length; i++) {
				lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
			}
			
			_mapGenerator.RequestMapData(position, OnMapDataRecieved);
		}

		void OnMapDataRecieved(MapData mapData) {
			this.mapData = mapData;
			mapDataReceived = true;
			Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.GetMapChunkSize(),
				MapGenerator.GetMapChunkSize());

			meshRenderer.material.mainTexture = texture;
			
			UpdateTerrainChunk();
		}

		public void UpdateTerrainChunk() {
			if (mapDataReceived) {
				float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
				bool visible = viewerDstFromNearestEdge <= maxViewDist;

				if (visible) {
					int lodIndex = 0;

					for (int i = 0; i < detailLevels.Length - 1; i++) {
						if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold) {
							lodIndex = i + 1;
						} else {
							break;
						}
					}

					if (lodIndex != previousLODIndex) {
						LODMesh lodMesh = this.lodMeshes[lodIndex];

						if (lodMesh.hasMesh) {
							previousLODIndex = lodIndex;
							meshFilter.mesh = lodMesh.mesh;
						} else if (!lodMesh.hasRequestedMesh) {
							lodMesh.RequestMesh(mapData);
						}
					}
				}
			
				SetVisible(visible);
			}
		}

		public void SetVisible(bool visible) {
			meshObject.SetActive(visible);
		}

		public bool IsVisible() {
			return meshObject.activeSelf;
		}
	}
	
	class LODMesh {
		public Mesh mesh;
		public bool hasRequestedMesh;
		public bool hasMesh;

		private int lod;

		private System.Action updateCallback;

		public LODMesh(int lod, System.Action updateCallback) {
			this.lod = lod;
			this.updateCallback = updateCallback;
		}

		void OnMeshDataReceived(MeshData meshData) {
			mesh = meshData.CreateMesh();
			hasMesh = true;

			updateCallback();
		}

		public void RequestMesh(MapData mapData) {
			hasRequestedMesh = true;
			_mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
		}
	}
	
	[System.Serializable]
	public struct LODInfo {
		public int lod;
		public float visibleDstThreshold;
	}
}
