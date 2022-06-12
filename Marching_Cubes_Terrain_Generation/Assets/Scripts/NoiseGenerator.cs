using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class NoiseMapGenerator {
	#region Class Variables.

	private static Dictionary<Vector2, FallOffData> falloffMap;
	#endregion

	#region Class Public Functions.
	public static float[,] GenerateNoiseMap(int mapWidth, int mapLength, float sampleSize, Vector3 chunkPos, bool useFalloff, NoiseSettings noiseSettings) {
		float[,] noiseMap = new float[mapWidth, mapLength];

		//If noise has not been seeded, seed it.
		System.Random prng = SeedNoise(noiseSettings);

		//Generate offsets.
		Vector2 offsets = noiseSettings.offset;
		offsets += new Vector2(prng.Next(-100000, 100000), prng.Next(-100000, 100000));
		float lastFalloffValue = 0.0f;

		//Populate the noise map.
		for (int z = 0; z < mapLength; z++) {
			for (int x = 0; x < mapWidth; x++) {
				Vector2 samplePoint = CalculateWorldSpacePos(x, z, mapWidth, mapLength, sampleSize, chunkPos);
				float sampleX = (samplePoint.x + offsets.x) / noiseSettings.scale;
				float sampleZ = (samplePoint.y + offsets.y) / noiseSettings.scale;

				float perlinValue = CalculateNoiseValue(sampleX, sampleZ);
				if (useFalloff) {
					//Initialise Value.
					float falloffValue = 0.0f;

					//Calculate position in falloff map of current node.
					lock (falloffMap) {
						//Check the key exists.
						if (falloffMap.ContainsKey(samplePoint)) {
							falloffValue = falloffMap[samplePoint].falloffValue;
							lastFalloffValue = falloffValue;
						} else {
							falloffValue = lastFalloffValue;
						}
					}

					//Add it to the height map.
					noiseMap[x, z] = Mathf.Clamp01(perlinValue + falloffValue);
				} else {
					noiseMap[x, z] = perlinValue;
				}
			}
		}

		return noiseMap;
	}

	public static float[,,] Generate3DNoiseMap(int mapWidth, int mapHeight, int mapLength, float sampleSize, Vector3 chunkPos, bool useFalloff, NoiseSettings noiseSettings) {
		float[,,] noiseMap = new float[mapWidth, mapHeight, mapLength];

		//If noise has not been seeded, seed it.
		System.Random prng = SeedNoise(noiseSettings);

		//Generate offsets.
		Vector2 offsets = noiseSettings.offset;
		offsets += new Vector2(prng.Next(-100000, 100000), prng.Next(-100000, 100000));
		bool lastFalloffValue = false;

		//Populate the noise map.
		for (int z = 0; z < mapLength; z++) {
			for (int y = 0; y < mapHeight; y++) {
				for (int x = 0; x < mapWidth; x++) {
					Vector3 samplePoint = CalculateWorldSpacePos3D(x, y, z, mapWidth, mapHeight, mapLength, sampleSize, chunkPos);
					Vector2 samplePoint2D = CalculateWorldSpacePos(x, z, mapWidth, mapLength, sampleSize, chunkPos);
					float sampleX = (samplePoint.x + offsets.x) / noiseSettings.scale;
					float sampleY = (samplePoint.y / noiseSettings.scale);
					float sampleZ = (samplePoint.z + offsets.y) / noiseSettings.scale;

					float perlinValue = Perlin3D(sampleX, sampleY, sampleZ);
					float weightedValue = perlinValue;
					if (y <= 0) {
						weightedValue = 1.0f;
					} else if (y <= 3) {
						weightedValue = (weightedValue * 1.5f);
						weightedValue = Mathf.Clamp(weightedValue, 0.0f, 1.0f);
					}
					if (useFalloff) {
						//Initialise Value.
						bool falloffValue = false;

						//Calculate position in falloff map of current node.
						lock (falloffMap) {
							//Check the key exists.
							if (falloffMap.ContainsKey(samplePoint2D)) {
								falloffValue = falloffMap[samplePoint2D].edgePoint;
							} else {
								falloffValue = true;
							}
						}

						//Add it to the height map.
						if (falloffValue) {
							weightedValue = 1.0f;
						}
					}
					noiseMap[x, y, z] = weightedValue;

				}
			}
		}

		return noiseMap;
	}

	public static void InitialiseFalloffMap(Vector2Int levelSize, Vector2 chunkSize, float cubeSize) {
		falloffMap = new Dictionary<Vector2, FallOffData>();
		lock (falloffMap) {
			falloffMap = GenerateLevelFalloffMap(levelSize, chunkSize, cubeSize);
		}
	}
	#endregion

	#region Class Private Functions (Utility functions).
	private static System.Random SeedNoise(NoiseSettings noiseSettings) {
		System.Random prng = new System.Random(noiseSettings.seed);
		return prng;
	}

	private static float CalculateNoiseValue(float a_x, float a_y) {
		return Mathf.PerlinNoise(a_x, a_y);
	}

	private static float Perlin3D(float x, float y, float z) {
		float ab = Mathf.PerlinNoise(x, y);
		float bc = Mathf.PerlinNoise(y, z);
		float ac = Mathf.PerlinNoise(x, z);

		float ba = Mathf.PerlinNoise(y, x);
		float cb = Mathf.PerlinNoise(z, y);
		float ca = Mathf.PerlinNoise(z, x);

		float abc = ab + bc + ac + ba + cb + ca;
		return abc / 6.0f;
	}
	private static Vector2 CalculateWorldSpacePos(int a_x, int a_z, int a_width, int a_length, float a_sampleSize, Vector3 a_chunkPos) {

		//Initialise return var.
		Vector2 worldSpacePos = new Vector2(a_chunkPos.x, a_chunkPos.z);

		//Calculate pos relative to chunk.
		float x = (float)a_x;
		float z = (float)a_z;
		Vector2 localPoint = new Vector2(x, z);
		localPoint = localPoint * a_sampleSize;
		float width = (float)a_width;
		float length = (float)a_length;

		//Calculate center point.
		float cenX = (width * 0.5f) + 0.5f;
		float cenZ = (length * 0.5f) + 0.5f;
		Vector2 chunkPosLocal = new Vector2(cenX - 1.0f, cenZ - 1.0f);
		chunkPosLocal = chunkPosLocal * a_sampleSize;

		//Calculate direction from center point to localPoint.
		Vector2 dirToLocalPoint = localPoint - chunkPosLocal;

		//Now we have direction we add that vector onto the world space chunk pos.
		worldSpacePos += dirToLocalPoint;

		return worldSpacePos;
	}

	private static Vector3 CalculateWorldSpacePos3D(int a_x, int a_y, int a_z, int a_width, int a_height, int a_length, float a_sampleSize, Vector3 a_chunkPos) {

		//Initialise return var.
		Vector3 worldSpacePos = new Vector3(a_chunkPos.x, a_chunkPos.y, a_chunkPos.z);

		//Calculate pos relative to chunk.
		Vector3 localPoint = new Vector3(a_x, a_y, a_z);
		localPoint = localPoint * a_sampleSize;

		//Calculate center point.
		Vector3 chunkPosLocal = new Vector3(((a_width * 0.5f) + 0.5f) - 1.0f, ((a_height * 0.5f) + 0.5f) - 1.0f, ((a_length * 0.5f) + 0.5f) - 1.0f);
		chunkPosLocal = chunkPosLocal * a_sampleSize;

		//Calculate direction from center point to localPoint.
		Vector3 dirToLocalPoint = localPoint - chunkPosLocal;

		//Now we have direction we add that vector onto the world space chunk pos.
		worldSpacePos += dirToLocalPoint;

		return worldSpacePos;
	}

	private static Dictionary<Vector2, FallOffData> GenerateLevelFalloffMap(Vector2Int levelSize, Vector2 chunkSize, float cubeSize) {
		//Calculate how many nodes are needed.
		int sizeX = levelSize.x * LevelGeneratorScript.CalculateNumberOfControlNodesInGrid(chunkSize.x, cubeSize);
		int sizeZ = levelSize.y * LevelGeneratorScript.CalculateNumberOfControlNodesInGrid(chunkSize.y, cubeSize);

		//Initialise falloff map.
		Dictionary<Vector2, FallOffData> map = new Dictionary<Vector2, FallOffData>();

		//Loop through all the nodes and generate the correct value.
		for (int j = 0; j < sizeZ; j++) {
			for (int i = 0; i < sizeX; i++) {
				float x = i / (float)sizeX * 2 - 1;
				float z = j / (float)sizeZ * 2 - 1;

				float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
				Vector2 position = CalculateWorldSpacePos(i, j, sizeX, sizeZ, cubeSize, Vector3.zero);
				float fallOffValue = Evaluate(value);
				bool edgePoint = (i >= (sizeX - 2)) || (j >= (sizeZ - 2)) || (i <= 1) || (j <= 1);
				FallOffData data = new FallOffData(fallOffValue, edgePoint);
				map.Add(position, data);
			}
		}
		return map;
	}

	private static float Evaluate(float value) {
		float a = 3.0f;
		float b = 2.2f;
		return Mathf.Pow(value, a) / (Mathf.Pow(value, a) + Mathf.Pow(b - b * value, a));
	}

	private struct FallOffData {
		public readonly float falloffValue;
		public readonly bool edgePoint;

		public FallOffData(float value, bool edge) {
			falloffValue = value;
			edgePoint = edge;
		}
	}
	#endregion
}
