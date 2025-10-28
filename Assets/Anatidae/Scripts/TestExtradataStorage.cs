/*
 Utilitaire pour lire et écrire des extradatas.
*/

using UnityEngine;
using UnityEditor;
using Anatidae;
using System.Collections;

public class TestExtradataStorage : MonoBehaviour
{
    [SerializeField] bool fetchOnAwake;
    [SerializeField] string key;
    [SerializeField] string value;

    void Awake()
    {
        if (fetchOnAwake)
            FetchData();
    }

    public void SetData()
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
        {
            Debug.LogWarning("La clé ou la valeur sont vides.");
            return;
        }

        StartCoroutine(ExtradataManager.SetExtraData(key, value));
        Debug.Log($"Set data: [{key}] = \"{value}\"");
    }

    public void FetchData()
    {
        StartCoroutine(FetchExtraDataCoroutine());
    }

    public IEnumerator FetchExtraDataCoroutine()
    {
        yield return ExtradataManager.FetchExtraData();
        if (ExtradataManager.HasFetchedExtraData)
        {
            if (ExtradataManager.ExtraData.Count == 0)
                Debug.Log("Aucune extradata.");
            else
                Debug.Log($"Extradata récupéré, nombre de clés : {ExtradataManager.ExtraData.Count}");
        }
        else
            Debug.LogWarning("Extradata indisponibles (le server est hors-ligne).");
    }

    public void GetDataWithKey()
    {
        if (ExtradataManager.HasFetchedExtraData)
        {
            var element = ExtradataManager.ExtraData.Find(e => e.key == key);
            if (element.key != null)
                Debug.Log($"Clé [{key}]: \"{element.value}\"");
            else
                Debug.LogWarning($"Aucune donnée stockée sur la clé [{key}].");
        }
        else
        {
            Debug.LogWarning("Les extradata n'ont pas encore été récupérées. Lancez d'abord la coroutine ExtradataManager.FetchExtraData()");
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TestExtradataStorage))]
public class TestExtradataStorageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TestExtradataStorage script = (TestExtradataStorage)target;
        GUILayout.Label("Fetch :");
        if (GUILayout.Button("Récupérer les datas"))
        {
            script.FetchData();
        }
        GUILayout.Space(10f);
        DrawDefaultInspector();

        if (GUILayout.Button("Enregistrer la paire key-value"))
        {
            script.SetData();
        }
        if (GUILayout.Button("Lire les données de key"))
        {
            script.GetDataWithKey();
        }
    }
}
#endif
