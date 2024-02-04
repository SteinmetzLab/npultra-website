using BrainAtlas;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using Urchin.Behaviors;

public class NPUltraRuntime : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void UpdateSelection(int idx);
#endif

    [SerializeField] CameraBehavior _mainCameraBehav;


    private void Start()
    {
        _mainCameraBehav.SetCameraMode(false);
        _mainCameraBehav.SetCameraRotation(new Vector3(-45f, 0f, -135f));
    }

    /// <summary>
    /// Start after BAM Meta is loaded
    /// </summary>
    public async void DelayedStart()
    {
        var taskVar = BrainAtlasManager.LoadAtlas("allen_mouse_25um");

        await taskVar;

        foreach (int ID in BrainAtlasManager.ActiveReferenceAtlas.DefaultAreas)
        {
            OntologyNode node = BrainAtlasManager.ActiveReferenceAtlas.Ontology.ID2Node(ID);
            _ = node.LoadMesh(OntologyNode.OntologyNodeSide.Full);

            await node.FullLoaded;
            node.SetMaterial(BrainAtlasManager.BrainRegionMaterials["transparent-unlit"], OntologyNode.OntologyNodeSide.Full);
            node.ResetColor();
            node.SetShaderProperty("_Alpha", 0.15f, OntologyNode.OntologyNodeSide.Full);
        }

        ClickCallback(610);
    }

    public void ClickCallback(int index)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateSelection(index);
#endif
    }
}
