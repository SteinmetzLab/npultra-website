using BrainAtlas;
using System.Collections.Generic;
using UnityEngine;
using Urchin.Behaviors;
using Urchin.Managers;

public class NPUltraRuntime : MonoBehaviour
{

    [SerializeField] PrimitiveMeshManager _meshManager;

    [SerializeField] CameraBehavior _mainCameraBehav;
    [SerializeField] TextAsset _dataAsset;

    private string[] _namesList;
    private string[] _areaList;
    private bool _loaded;

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

    public void Search(string searchStr)
    {
        if (!_loaded) return;

        float[] scales = new float[_namesList.Length];

        for (int i = 0; i < _namesList.Length; i++)
        {
            if (_areaList[i].Contains(searchStr))
                scales[i] = 0.05f;
            else
                scales[i] = 0.01f; 
        }

        _meshManager.SetScale(_namesList, scales);
    }

    private void LoadData()
    {
        if (_dataAsset != null)
        {
            string[] rows = _dataAsset.text.Split('\n');

            Vector3[] coords = new Vector3[rows.Length-2];
            Color[] colors = new Color[rows.Length - 2];
            _areaList = new string[rows.Length - 2];
            _namesList = new string[rows.Length - 2];
            float[] scales = new float[rows.Length - 2];

            // skip the first row (header) and last row (blank)
            for (int i = 1; i < (rows.Length - 1); i++)
            {
                string[] columns = rows[i].Trim().Split(',');

                float ap = int.Parse(columns[0], System.Globalization.NumberStyles.Any);
                float dv = int.Parse(columns[1], System.Globalization.NumberStyles.Any);
                float ml = int.Parse(columns[2], System.Globalization.NumberStyles.Any);

                // ap/ml/dv are in index coordinates, convert to um here
                coords[i-1] = new Vector3(ap / 1000f,
                    ml / 1000f,
                    dv / 1000f);
                _areaList[i - 1] = columns[3];
                colors[i - 1] = HexToColor(columns[4]);
                _namesList[i - 1] = i.ToString();
                scales[i - 1] = 0.05f;
            }

            _meshManager.CreateMesh(_namesList);
            _meshManager.SetPositions(_namesList, coords);
            _meshManager.SetColor(_namesList, colors);
            _meshManager.SetScale(_namesList, scales);
        }
        else
        {
            Debug.LogError("CSV file not assigned!");
        }

        _loaded = true;
    }

    private Color HexToColor(string hex)
    {
        float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
        float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;

        return new Color(r, g, b);
    }
}
