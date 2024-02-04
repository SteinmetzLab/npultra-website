using BrainAtlas;
using UnityEngine;
using Urchin.Behaviors;
using Urchin.Managers;

public class NPUltraRuntime : MonoBehaviour
{

    [SerializeField] PrimitiveMeshManager _meshManager;

    [SerializeField] CameraBehavior _mainCameraBehav;
    [SerializeField] TextAsset _dataAsset;

    private void Start()
    {
    }

    /// <summary>
    /// Start after BAM Meta is loaded
    /// </summary>
    public async void DelayedStart()
    {
        _mainCameraBehav.SetCameraMode(false);
        _mainCameraBehav.SetCameraZoom(-11.5f);

        var taskVar = BrainAtlasManager.LoadAtlas("allen_mouse_25um");

        await taskVar;

        LoadData();

        foreach (int ID in BrainAtlasManager.ActiveReferenceAtlas.DefaultAreas)
        {
            OntologyNode node = BrainAtlasManager.ActiveReferenceAtlas.Ontology.ID2Node(ID);
            _ = node.LoadMesh(OntologyNode.OntologyNodeSide.Full);

            await node.FullLoaded;
            node.SetMaterial(BrainAtlasManager.BrainRegionMaterials["transparent-unlit"], OntologyNode.OntologyNodeSide.Full);
            node.ResetColor();
            node.SetShaderProperty("_Alpha", 0.15f, OntologyNode.OntologyNodeSide.Full);
        }
    }

    void LoadData()
    {
        if (_dataAsset != null)
        {
            string[] rows = _dataAsset.text.Split('\n');

            Vector3[] coords = new Vector3[rows.Length];
            Color[] colors = new Color[rows.Length];
            string[] names = new string[rows.Length];

            for (int i = 0; i < rows.Length; i++)
            {
                string[] columns = rows[i].Trim().Split(',');

                float dv = int.Parse(columns[0], System.Globalization.NumberStyles.Any);
                float ml = int.Parse(columns[1], System.Globalization.NumberStyles.Any);
                float ap = int.Parse(columns[2], System.Globalization.NumberStyles.Any);

                // ap/ml/dv are in index coordinates, convert to um here
                coords[i] = new Vector3(ap * BrainAtlasManager.ActiveReferenceAtlas.Dimensions.x / 1000f,
                    ml * BrainAtlasManager.ActiveReferenceAtlas.Dimensions.y / 1000f,
                    dv * BrainAtlasManager.ActiveReferenceAtlas.Dimensions.z / 1000f);
                colors[i] = HexToColor(columns[3]);
                names[i] = i.ToString();
            }

            _meshManager.CreateMesh(names);
            _meshManager.SetPositions(names, coords);
            _meshManager.SetColor(names, colors);
        }
        else
        {
            Debug.LogError("CSV file not assigned!");
        }
    }

    bool IsHexColor(string value)
    {
        return value.Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9A-Fa-f]*$");
    }

    Color HexToColor(string hex)
    {
        float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

        return new Color(r, g, b);
    }
}
