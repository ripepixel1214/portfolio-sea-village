using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public abstract class SpecialShopDetailPanelBase : UIPanel, IContextualPanel
    {
        [SerializeField] protected TextMeshProUGUI _headerText;
        [SerializeField] protected Button _closeButton;
        [SerializeField] protected Transform _content;
        [SerializeField] protected GameObject _entryPrefab;

        private readonly List<SpecialShopActionEntryView> _entries = new();
        private readonly List<GameObject> _entryObjects = new();

        protected IReadOnlyList<SpecialShopActionEntryView> Entries => _entries;

        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }

        public override void OnClose()
        {
            ClearEntries();
            base.OnClose();
        }

        protected SpecialShopActionEntryView CreateEntry(string objectName)
        {
            GameObject entryObject = CreateEntryObject(objectName);
            if (entryObject == null)
                return null;

            SpecialShopActionEntryView entry = entryObject.GetComponent<SpecialShopActionEntryView>();
            if (entry == null)
            {
                _entryObjects.Remove(entryObject);
                Destroy(entryObject);
                return null;
            }

            _entries.Add(entry);
            return entry;
        }

        /// <summary>공통 Entry 컴포넌트가 없는 전용 항목 생성</summary>
        protected GameObject CreateEntryObject(string objectName)
        {
            if (_content == null || _entryPrefab == null)
                return null;

            GameObject entryObject = Instantiate(_entryPrefab, _content);
            entryObject.name = objectName;
            entryObject.SetActive(true);
            _entryObjects.Add(entryObject);
            return entryObject;
        }

        protected void ClearEntries()
        {
            for (int i = 0; i < _entryObjects.Count; i++)
            {
                GameObject entryObject = _entryObjects[i];
                if (entryObject == null)
                    continue;

                SpecialShopActionEntryView entry = entryObject.GetComponent<SpecialShopActionEntryView>();
                if (entry != null && entry.ActionButton != null)
                    entry.ActionButton.onClick.RemoveAllListeners();

                Destroy(entryObject);
            }

            _entries.Clear();
            _entryObjects.Clear();
            navigableButtons.Clear();
        }

        protected void RefreshNavigation()
        {
            RefreshNavigation(null);
        }

        /// <summary>공통 항목과 전용 항목의 버튼 네비게이션 갱신</summary>
        protected void RefreshNavigation(IReadOnlyList<Button> additionalButtons)
        {
            navigableButtons.Clear();
            if (_closeButton != null)
                navigableButtons.Add(_closeButton);

            for (int i = 0; i < _entries.Count; i++)
            {
                Button button = _entries[i]?.ActionButton;
                if (button != null)
                    navigableButtons.Add(button);
            }

            if (additionalButtons != null)
            {
                for (int i = 0; i < additionalButtons.Count; i++)
                {
                    Button button = additionalButtons[i];
                    if (button != null)
                        navigableButtons.Add(button);
                }
            }

            int defaultIndex = navigableButtons.Count > 1 ? 1 : 0;
            defaultSelectedButtonIndex = defaultIndex;
            currentSelectedButtonIndex = defaultIndex;
            EnsureValidSelection();
        }

        protected void ShowMessageAfterCurrentDialog(string message, Action onConfirm = null, string title = "알림")
        {
            if (!UIManager.HasInstance)
                return;

            UIManager manager = UIManager.Instance;
            void Handler()
            {
                manager.OnPanelClosed -= Handler;
                manager.ShowAlertMessage(message, onConfirm, title);
            }

            manager.OnPanelClosed += Handler;
        }
    }
}
