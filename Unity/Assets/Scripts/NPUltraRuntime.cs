using BrainAtlas;
using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using Urchin.Behaviors;
using Urchin.Managers;
using Random = UnityEngine.Random;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class NPUltraRuntime : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void FinishedLoading();
    
    [DllImport("__Internal")]
    private static extern void UpdateSelection(int idx);
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
        _cosmosNodes = new List<OntologyNode>();

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

    private bool _searchActive;
    private int[] _searched;

    public void Search(string state)
    {
        if (!_loaded) return;

        State curState = JsonUtility.FromJson<State>(state);

        bool activeArea = curState.search != "";
        bool disabledDur = !curState.duration_long || !curState.duration_short;
        bool disabledWave = !curState.footprint_large || !curState.footprint_small;

        // Set neurons to active/inactive state
        if (activeArea || disabledDur || disabledWave)
        {
            _searched = new int[_data.Areas.Length];

            for (int i = 0; i < _searched.Length; i++)
            {
                // For each unit, check if:
                // we are searching for areas AND this is in that area, or we aren't searching for areas
                // we are searching for short AND this is short
                // we are searching for long AND this is long
                // etc...

                bool inArea = !activeArea || (activeArea && _data.Areas[i].Contains(curState.search));
                bool isShort = curState.duration_short && _data.ShortDuration[i];
                bool isLong = curState.duration_long && !_data.ShortDuration[i];
                bool isSmall = curState.footprint_small && _data.SmallFootprint[i];
                bool isLarge = curState.footprint_large && !_data.SmallFootprint[i];

                bool active = inArea && (isShort || isLong) && (isSmall || isLarge);

                _searched[i] = active ? 1 : -1;
            }

            _meshManager.SetSearched(_data.Names, _searched);
            _searchActive = true;
        }
        else
        {
            _meshManager.SetSearched(_data.Names, 0);
            _searchActive = false;
        }

        // Handle showing and hiding 3D areas
        if (activeArea)
        {
            // Hide cosmos areas
            foreach (OntologyNode node in _cosmosNodes)
                node.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);

            // Hide the previous searched node
            if (_searchedNode != null)
                _searchedNode.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);

            // Make the searched area visible
            _rootNode.SetVisibility(true, OntologyNode.OntologyNodeSide.Full);
            _searchedNode = BrainAtlasManager.ActiveReferenceAtlas.Ontology.ID2Node(
                BrainAtlasManager.ActiveReferenceAtlas.Ontology.Acronym2ID(curState.search));
            LoadNode(_searchedNode, 0.15f);
        }
        else
        {
            // Show cosmos areas
            foreach (OntologyNode node in _cosmosNodes)
                node.SetVisibility(true, OntologyNode.OntologyNodeSide.Full);

            // Make the searched area visible
            _rootNode.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);
            if (_searchedNode != null)
                _searchedNode.SetVisibility(false, OntologyNode.OntologyNodeSide.Full);
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

        // Select the new neuron
        _selected = selectedName;

        MeshBehavior meshBehavior = _meshManager.GetMesh(_selected);
        meshBehavior.Select(true);

        // If the selected neuron is outside the area we are in, re-select
        CheckSelection();
    }

    private int SelectedIndex()
    {
        return Array.IndexOf(_data.Names, _selected);
    }

    private int _selDirection;
    public void SelectDirection(int direction)
    {
        _selDirection = direction;

        int selIdx = SelectedIndex(); // -1 to switch to 0 indexing
        selIdx += direction;

        if (selIdx >= _data.Names.Length)
            selIdx = 0;
        if (selIdx < 0)
            selIdx = _data.Names.Length - 1;

        // tell the javascript page to make this the new selection
#if UNITY_WEBGL && !UNITY_EDITOR
        UpdateSelection(int.Parse(_data.Names[selIdx], System.Globalization.NumberStyles.Any));
#endif
    }

    public void CheckSelection()
    {
        if (_searchActive)
        {
            int selIdx = SelectedIndex(); // -1 to switch to 0 indexing

            // Check if this is an inactive unit
            if (_searched[selIdx] == -1)
            {
                // Unit is inactive
                // We need to re-select the next neuron, loop around until we find it
                int newIdx = 0;
                int count = 0;

                if (_selDirection > 0)
                {
                    // We incremented, search up, then loop around if needed
                    newIdx = selIdx;
                    while (_searched[newIdx] == -1)
                    {
                        newIdx++;
                        if (newIdx >= _searched.Length)
                            newIdx = 0;

                        count++;
                        if (count > _searched.Length)
                        {
                            Debug.LogWarning("No neurons available");
                            break;
                        }
                    }
                }
                else if (_selDirection < 0)
                {
                    // We decremented, search down, then loop around if needed
                    newIdx = selIdx;
                    while (_searched[newIdx] == -1)
                    {
                        newIdx--;
                        if (newIdx <= 0)
                            newIdx = _searched.Length - 1;

                        count++;
                        if (count > _searched.Length)
                        {
                            Debug.LogWarning("No neurons available");
                            break;
                        }
                    }
                }

                // tell the javascript page to make this the new selection
#if UNITY_WEBGL && !UNITY_EDITOR
                    UpdateSelection(int.Parse(_data.Names[newIdx], System.Globalization.NumberStyles.Any));
#endif
            }
        }
    }
}

public struct State
{
    //var state = {
    //    selected: -1, // 1 to N
    //    search: '', // area acronym
    //    footprint_small: true, // -1 short, 1 long, 0 both
    //    footprint_large: true, // -1 small, 1 big, 0 both
    //    duration_short: true,
    //    duration_long: true,
    //};
    public int selected;
    public string search;
    public bool footprint_small;
    public bool footprint_large;
    public bool duration_short;
    public bool duration_long;
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
            List<bool> durationList = new();
            List<bool> footprintList = new();

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

                durationList.Add(bool.Parse(columns[6]));
                footprintList.Add(bool.Parse(columns[7]));
            }

            dataSO.Names = namesList.ToArray();
            dataSO.Coords = coords.ToArray();
            dataSO.Colors = colors.ToArray();
            dataSO.Areas = areaList.ToArray();
            dataSO.ShortDuration = durationList.ToArray();
            dataSO.SmallFootprint = footprintList.ToArray();
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