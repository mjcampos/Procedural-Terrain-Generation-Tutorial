using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {
	public const float maxViewDist = 450;
	public Transform viewer;
	public Material mapMaterial;

	public static Vector2 viewerPosition;
	private static MapGenerator _mapGenerator;
	private int chunkSize;
	private int chunkVisibleInViewDst;
	
	Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

	private void Start() {
		_mapGenerator = FindObjectOfType<MapGenerator>();
		chunkSize = MapGenerator.GetMapChunkSize() - 1;
		chunkVisibleInViewDst = Mathf.RoundToInt(maxViewDist / chunkSize);
	}

	private void Update() {
		viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
		UpdateVisibleChunks();
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
					terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, transform, mapMaterial));
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
		
		public TerrainChunk(Vector2 coord, int size, Transform parent, Material material) {
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
			
			_mapGenerator.RequestMapData(OnMapDataRecieved);
		}

		void OnMapDataRecieved(MapData mapData) {
			_mapGenerator.RequestMeshData(mapData, OnMeshDataReceived);
		}

		void OnMeshDataReceived(MeshData meshData) {
			meshFilter.mesh = meshData.CreateMesh();
		}

		public void UpdateTerrainChunk() {
			float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
			bool visible = viewerDstFromNearestEdge <= maxViewDist;
			SetVisible(visible);
		}

		public void SetVisible(bool visible) {
			meshObject.SetActive(visible);
		}

		public bool IsVisible() {
			return meshObject.activeSelf;
		}
	}
}
