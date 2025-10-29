using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Nécessaire pour Button
using UnityEngine.EventSystems; // Nécessaire pour gérer la sélection

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject mapSelectionPanel;

    [Header("First Selected Buttons")]
    [SerializeField] private GameObject mainMenuFirstButton; // Bouton "Choisir Carte"
    [SerializeField] private GameObject mapSelectFirstButton; // Premier bouton de carte

    // --- Pas besoin de firstLevelSceneName ici ---

    void Start()
    {
        // S'assurer que seul le menu principal est visible au début
        ShowMainMenu();
    }

    // Fonction appelée par le bouton "Choisir Carte"
    public void ShowMapSelection()
    {
        mainMenuPanel.SetActive(false);
        mapSelectionPanel.SetActive(true);

        // Sélectionner automatiquement le premier bouton de carte
        EventSystem.current.SetSelectedGameObject(mapSelectFirstButton);
    }

    // Fonction appelée par le bouton "Retour"
    public void ShowMainMenu()
    {
        mapSelectionPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        // Sélectionner automatiquement le bouton "Choisir Carte"
        EventSystem.current.SetSelectedGameObject(mainMenuFirstButton);
    }

    // Fonction appelée par les boutons de carte (Map1Button, Map2Button, etc.)
    public void LoadLevel(string sceneName)
    {
         if (string.IsNullOrEmpty(sceneName))
         {
             Debug.LogError("Nom de scène vide ! Assurez-vous de l'avoir défini dans l'inspecteur du bouton.");
             return;
         }
         Debug.Log("Chargement de la scène : " + sceneName);
         SceneManager.LoadScene(sceneName);
    }
}