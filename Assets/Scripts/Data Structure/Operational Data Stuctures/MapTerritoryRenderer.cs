using System.Collections.Generic;
using TheaterStrategy;
using UnityEngine;

public class MapTerritoryRenderer : MonoBehaviour
{
    public static MapTerritoryRenderer Instance { get; private set; }

    [Header("Rendering Setup")]
    [SerializeField] private Renderer mapRenderer;
    [SerializeField] private Texture2D baseMapTexture;
    [SerializeField] private Texture2D provinceMapTexture;

    [Header("Faction Tint Colors")]
    [SerializeField] private Color natoColor = new Color(0.2f, 0.5f, 1.0f, 1.0f);  
    [SerializeField] private Color acmfColor = new Color(0.9f, 0.2f, 0.2f, 1.0f);   
    [SerializeField] private Color satoColor = new Color(0.1f, 0.85f, 0.65f, 1.0f); 
    [SerializeField] private Color neutralColor = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    private Material _mapMaterial;
    private Texture2D _provincePalette;       
    private Texture2D _encodedProvinceIDTex;  
    private bool _isInitialized = false;

    private void Awake()
    {
        Instance = this;
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        Instance = this;

        if (_isInitialized && _mapMaterial != null && _provincePalette != null) return;

        if (mapRenderer == null) mapRenderer = GetComponent<Renderer>();
        if (mapRenderer != null) _mapMaterial = mapRenderer.material;

        if (_provincePalette == null)
        {
            _provincePalette = new Texture2D(256, 256, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] empty = new Color32[256 * 256];
            for (int i = 0; i < empty.Length; i++) empty[i] = new Color32(255, 255, 255, 0);
            _provincePalette.SetPixels32(empty);
            _provincePalette.Apply();
        }

        if (_mapMaterial != null)
        {
            if (baseMapTexture != null) _mapMaterial.SetTexture("_MainTex", baseMapTexture);
            _mapMaterial.SetTexture("_LookupTex", _provincePalette);
            if (_encodedProvinceIDTex != null) _mapMaterial.SetTexture("_ProvinceIDTex", _encodedProvinceIDTex);
        }

        _isInitialized = true;
    }

    public void BakeProvinceIDTexture(int width, int height, Color32[] primaryPixels, System.Func<Color32, int> colorToIDFunc)
    {
        EnsureInitialized();

        Color32[] encoded = new Color32[primaryPixels.Length];

        for (int i = 0; i < primaryPixels.Length; i++)
        {
            int provID = colorToIDFunc(primaryPixels[i]);

            if (provID > 0)
            {
                byte lowByte = (byte)(provID & 255);
                byte highByte = (byte)((provID >> 8) & 255);
                encoded[i] = new Color32(lowByte, highByte, 0, 255); 
            }
            else
            {
                encoded[i] = new Color32(0, 0, 0, 0); 
            }
        }

        _encodedProvinceIDTex = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        _encodedProvinceIDTex.SetPixels32(encoded);
        _encodedProvinceIDTex.Apply();

        if (_mapMaterial != null)
        {
            _mapMaterial.SetTexture("_ProvinceIDTex", _encodedProvinceIDTex);
        }

        Debug.Log($"<color=cyan>[MapTerritoryRenderer] 16-Bit Province ID Texture baked ({width}x{height}) and bound to material!</color>");
    }

    public void SetProvinceFaction(int provinceID, Faction faction)
    {
        EnsureInitialized();
        if (_provincePalette == null || provinceID <= 0 || provinceID >= 65536) return;

        Color tint = GetFactionTintColor(faction);
        int u = provinceID & 255;
        int v = (provinceID >> 8) & 255;

        _provincePalette.SetPixel(u, v, tint);
        _provincePalette.Apply();
    }

    public void RefreshAllProvinces(IEnumerable<HOI4MapManager.RuntimeProvince> allProvinces)
    {
        EnsureInitialized();
        if (_provincePalette == null || allProvinces == null) return;

        int activeCount = 0;
        foreach (var prov in allProvinces)
        {
            Color tint = GetFactionTintColor(prov.controllingFaction);
            if (prov.controllingFaction != Faction.Neutral) activeCount++;

            int u = prov.id & 255;
            int v = (prov.id >> 8) & 255;
            _provincePalette.SetPixel(u, v, tint);
        }

        _provincePalette.Apply();
        Debug.Log($"<color=lime>[MapTerritoryRenderer] Palette updated! {activeCount} provinces tinted with active factions.</color>");
    }

    [ContextMenu("Force Refresh From MapManager")]
    public void RefreshFromMapManager()
    {
        if (HOI4MapManager.Instance == null) return;

        var prop = typeof(HOI4MapManager).GetField("_provincesByID",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (prop != null)
        {
            var dict = prop.GetValue(HOI4MapManager.Instance) as System.Collections.IDictionary;
            if (dict != null)
            {
                var list = new List<HOI4MapManager.RuntimeProvince>();
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (entry.Value is HOI4MapManager.RuntimeProvince p) list.Add(p);
                }
                RefreshAllProvinces(list);
            }
        }
    }

    private Color GetFactionTintColor(Faction faction)
    {
        return faction switch
        {
            Faction.NATO => natoColor,
            Faction.ACMF => acmfColor,
            Faction.SATO => satoColor,
            _ => neutralColor
        };
    }
}