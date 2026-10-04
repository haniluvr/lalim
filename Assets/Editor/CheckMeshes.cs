using UnityEngine;
using UnityEditor;

public class CheckMeshes
{
    public static void Check()
    {
        GameObject oceanFloor = GameObject.Find("OceanfloorFixedFinal");
        if (oceanFloor != null)
        {
            foreach (Transform child in oceanFloor.GetComponentsInChildren<Transform>())
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Debug.Log($"Mesh found on {child.name}: {mf.sharedMesh.name} (Vertices: {mf.sharedMesh.vertexCount})");
                }
            }
        }
    }
}
