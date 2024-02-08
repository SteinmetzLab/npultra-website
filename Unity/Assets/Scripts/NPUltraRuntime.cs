using BrainAtlas;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using Urchin.Behaviors;
using Urchin.Managers;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class NPUltraRuntime : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void FinishedLoading();
#endif

    private const float UNIT_SIZE = 0.075f;
    private const float UNIT_SIZE_DISABLED = 0.01f;

    [SerializeField] PrimitiveMeshManager _meshManager;

    [SerializeField] CameraBehavior _mainCameraBehav;
    [SerializeField] NPUltraData _data;

    private int[] _areaIDs = {313, 315, 512, 549, 623, 698, 703, 1065, 1069, 1097 };

    private bool _loaded;
    private string _selected;

    private OntologyNode _rootNode;
    private OntologyNode _searchedNode;
    private List<OntologyNode> _cosmosNodes;

    private void Start()
    {

#if !UNITY_EDITOR && UNITY_WEBGL
        WebGLInput.captureAllKeyboardInput = false;
#endif
    }

    /// <summary>
    /// Start after BAM Meta is loaded
    /// </summary>
    public async void DelayedStart()
    {
        _mainCameraBehav.SetCameraMode(false);
        _mainCameraBehav.SetCameraZoom(-15f);

        var taskVar = BrainAtlasManager.LoadAtlas("allen_mouse_25um");

        await taskVar;

        LoadData();

        foreach (int ID in _areaIDs)
        {
            OntologyNode node = BrainAtlasManager.ActiveReferenceAtlas.Ontology.ID2Node(ID);
            _cosmosNodes.Add(node);
            LoadNode(node);
        }


#if UNITY_WEBGL && !UNITY_EDITOR
        FinishedLoading();
#endif

        _rootNode = BrainAtlasManager.ActiveReferenceAtlas.Ontology.ID2Node(8);
        _ = _rootNode.LoadMesh(OntologyNode.OntologyNodeSide.Full);

        await _rootNode.FullLoaded;
        _rootNode.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);
        _rootNode.SetMaterial(BrainAtlasManager.BrainRegionMaterials["transparent-unlit"], OntologyNode.OntologyNodeSide.Full);
        _rootNode.SetColor(Color.black, OntologyNode.OntologyNodeSide.Full);
        _rootNode.SetShaderProperty("_Alpha", 0.1f, OntologyNode.OntologyNodeSide.Full);
    }

    public void LoadData()
    {
        _meshManager.CreateMesh(_data.Names);
        _meshManager.SetPositions(_data.Names, _data.Coords);
        _meshManager.SetColor(_data.Names, _data.Colors);
        _meshManager.SetScale(_data.Names, UNIT_SIZE);

        foreach (string name in _data.Names)
            _meshManager.GetMesh(name).Save();

        _loaded = true;
    }

    public void Search(string searchStr)
    {
        if (!_loaded) return;

        if (searchStr != "")
        {
            // Hide cosmos areas
            foreach (OntologyNode node in _cosmosNodes)
                node.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);

            // Make the searched area visible
            _rootNode.SetVisibility(true, OntologyNode.OntologyNodeSide.Full);
            _searchedNode = BrainAtlasManager.ActiveReferenceAtlas.Ontology.ID2Node(
                BrainAtlasManager.ActiveReferenceAtlas.Ontology.Acronym2ID(searchStr));
            LoadNode(_searchedNode, 0.25f);

            // Resize neurons
            float[] scales = new float[_data.Names.Length];

            for (int i = 0; i < _data.Names.Length; i++)
            {
                if (_data.Names[i].Contains(searchStr))
                    scales[i] = UNIT_SIZE;
                else
                    scales[i] = UNIT_SIZE_DISABLED;
            }

            _meshManager.SetScale(_data.Names, scales);
        }
        else
        {
            // Show cosmos areas
            foreach (OntologyNode node in _cosmosNodes)
                node.SetVisibility(true, OntologyNode.OntologyNodeSide.Full);

            // Make the searched area visible
            _rootNode.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);
            _searchedNode.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);

            _meshManager.SetScale(_data.Names, UNIT_SIZE);
        }
    }

    private async void LoadNode(OntologyNode node, float alpha = 0.1f)
    {
        _ = node.LoadMesh(OntologyNode.OntologyNodeSide.Full);

        await node.FullLoaded;
        node.SetVisibility(true, OntologyNode.OntologyNodeSide.Full);
        node.SetMaterial(BrainAtlasManager.BrainRegionMaterials["transparent-unlit"], OntologyNode.OntologyNodeSide.Full);
        node.ResetColor();
        node.SetShaderProperty("_Alpha", alpha, OntologyNode.OntologyNodeSide.Full);
    }

    public void Select(string selectedName)
    {
        // un-select the previous selection
        if (_selected != null)
        {
            MeshBehavior unMesh = _meshManager.GetMesh(_selected);
            unMesh.Select(false);
        }

        _selected = selectedName;

        MeshBehavior meshBehavior = _meshManager.GetMesh(_selected);
        meshBehavior.Select(true);
    }
}

#if UNITY_EDITOR
public class DataProcessing
{
    public const float RAND_SCALE = 0.075f;

    [MenuItem("Tools/Process CSV")]
    public static void ProcessCSV()
    {
        TextAsset dataAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/output.csv");
        NPUltraData dataSO = ScriptableObject.CreateInstance<NPUltraData>();

        if (dataAsset != null)
        {
            string[] rows = dataAsset.text.Split('\n');

            List<Vector3> coords = new();
            List<Color> colors = new();
            List<string> namesList = new();
            List<string> areaList = new();

            // skip the first row (header) and last row (blank)
            for (int i = 1; i < (rows.Length - 1); i++)
            {
                string[] columns = rows[i].Trim().Split(',');

                bool artifact = int.Parse(columns[5], System.Globalization.NumberStyles.Any) == 1;
                if (artifact)
                    continue;

                float ap = int.Parse(columns[0], System.Globalization.NumberStyles.Any);
                float dv = int.Parse(columns[1], System.Globalization.NumberStyles.Any);
                float ml = int.Parse(columns[2], System.Globalization.NumberStyles.Any);

                // ap/ml/dv are in index coordinates, convert to um here
                coords.Add(new Vector3(ap / 1000f + RandN(),
                    ml / 1000f + RandN(),
                    dv / 1000f + RandN()));
                areaList.Add(columns[3]);
                colors.Add(HexToColor(columns[4]));
                namesList.Add(i.ToString());
            }

            dataSO.Names = namesList.ToArray();
            dataSO.Coords = coords.ToArray();
            dataSO.Colors = colors.ToArray();
            dataSO.Areas = areaList.ToArray();
        }
        else
        {
            Debug.LogError("CSV file not assigned!");
        }


        AssetDatabase.CreateAsset(dataSO, "Assets/Data/dataSO.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static float RandN()
    {
        return RAND_SCALE * BoxMullerTransform(); // Scale the value by standard deviation (10)
    }

    private static float BoxMullerTransform()
    {
        float u1 = 1f - Random.value; // Ensure u1 is in the range (0, 1] to avoid log(0)
        float u2 = 1f - Random.value;
        float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        return z;
    }

    private static Color HexToColor(string hex)
    {
        float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

        return new Color(r, g, b);
    }
}
#endif