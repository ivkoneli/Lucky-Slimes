using UnityEngine;
using UnityEngine.UI;

namespace SlimeRPG
{
    /// <summary>
    /// Opens/closes the Inventory and Skill Tree overlay panels from the bottom nav buttons.
    /// Only one panel is shown at a time; tapping a nav button toggles its panel.
    /// </summary>
    public class ScreenNav : MonoBehaviour
    {
        public Button inventoryButton, skillsButton, collectionButton;
        public Button inventoryClose, skillsClose, collectionClose;
        public GameObject inventoryPanel, skillsPanel, collectionPanel;

        void Start()
        {
            if (inventoryButton != null) inventoryButton.onClick.AddListener(() => Toggle(inventoryPanel));
            if (skillsButton != null) skillsButton.onClick.AddListener(() => Toggle(skillsPanel));
            if (collectionButton != null) collectionButton.onClick.AddListener(() => Toggle(collectionPanel));
            if (inventoryClose != null) inventoryClose.onClick.AddListener(() => Hide(inventoryPanel));
            if (skillsClose != null) skillsClose.onClick.AddListener(() => Hide(skillsPanel));
            if (collectionClose != null) collectionClose.onClick.AddListener(() => Hide(collectionPanel));
            Hide(inventoryPanel);
            Hide(skillsPanel);
            Hide(collectionPanel);
        }

        void Toggle(GameObject panel)
        {
            if (panel == null) return;
            bool show = !panel.activeSelf;
            Hide(inventoryPanel);
            Hide(skillsPanel);
            Hide(collectionPanel);
            panel.SetActive(show);
        }

        void Hide(GameObject panel) { if (panel != null) panel.SetActive(false); }
    }
}
