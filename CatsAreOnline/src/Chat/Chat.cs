using System.Collections.Generic;

using CaLAPI.API;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Object = UnityEngine.Object;

namespace CatsAreOnline.Chat {
    public static class Chat {
        public static int messagesCapacity { get; set; } = 10;
        public static int historyCapacity { get; set; } = 100;
        public static readonly List<Message> messages = new List<Message>(messagesCapacity);
        public static readonly List<string> history = new List<string>(historyCapacity);

        public static float fadeOutDelay { get; set; }

        public static float fadeOutSpeed { get; set; }

        public static bool chatFocused {
            get => _fieldBackground && _fieldBackground.activeSelf;
            set {
                if(!_inputField || !_fieldBackground) return;
                _inputField.interactable = value;
                _fieldBackground.SetActive(value);
                _inputField.ActivateInputField();
            }
        }

        private static RectTransform _messagesContainer;
        private static InputField _inputField;
        private static GameObject _fieldBackground;

        private static Client _client;

        public static void Initialize(Client client) {
            _client = client;
            
            #region Canvas creation

            GameObject canvasObject = new GameObject("Chat UI Canvas") { layer = LayerMask.NameToLayer("UI") };
            Object.DontDestroyOnLoad(canvasObject);

            RectTransform canvasTransform = canvasObject.AddComponent<RectTransform>();

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;

            //canvasObject.AddComponent<GraphicRaycaster>();

            #endregion

            #region Layout group creation

            GameObject layoutGroupObject = new GameObject("Chat UI Layout Group") {
                layer = LayerMask.NameToLayer("UI")
            };
            Object.DontDestroyOnLoad(layoutGroupObject);

            RectTransform layoutGroupTransform = layoutGroupObject.AddComponent<RectTransform>();
            layoutGroupTransform.SetParent(canvasTransform);
            layoutGroupTransform.anchoredPosition = Vector2.zero;
            layoutGroupTransform.pivot = Vector2.zero;
            layoutGroupTransform.anchorMin = Vector2.zero;
            layoutGroupTransform.anchorMax = new Vector2(0.25f, 1f);

            VerticalLayoutGroup canvasLayoutGroup = layoutGroupObject.AddComponent<VerticalLayoutGroup>();
            canvasLayoutGroup.padding = new RectOffset(0, 0, 0, 0);
            canvasLayoutGroup.spacing = 0f;
            canvasLayoutGroup.childAlignment = TextAnchor.LowerLeft;
            canvasLayoutGroup.childControlHeight = true;
            canvasLayoutGroup.childForceExpandHeight = false;

            #endregion

            #region Messages container creation

            GameObject containerObject = new GameObject("Messages Container") { layer = LayerMask.NameToLayer("UI") };
            Object.DontDestroyOnLoad(containerObject);
            
            _messagesContainer = containerObject.AddComponent<RectTransform>();
            _messagesContainer.SetParent(layoutGroupTransform);
            _messagesContainer.pivot = Vector2.zero;
            _messagesContainer.sizeDelta = Vector2.zero;
            _messagesContainer.anchorMin = Vector2.zero;
            _messagesContainer.anchorMax = Vector2.right;

            VerticalLayoutGroup messagesLayoutGroup = containerObject.AddComponent<VerticalLayoutGroup>();
            messagesLayoutGroup.padding = new RectOffset(16, 0, 0, 8);
            messagesLayoutGroup.spacing = 5f;
            messagesLayoutGroup.childAlignment = TextAnchor.LowerLeft;
            messagesLayoutGroup.childControlWidth = false;
            messagesLayoutGroup.childControlHeight = false;
            messagesLayoutGroup.childForceExpandWidth = false;
            messagesLayoutGroup.childForceExpandHeight = false;

            #endregion
            
            #region Input field background creation

            _fieldBackground = new GameObject("Input Field") { layer = LayerMask.NameToLayer("UI") };
            Object.DontDestroyOnLoad(_fieldBackground);

            RectTransform fieldBackgroundTransform = _fieldBackground.AddComponent<RectTransform>();
            fieldBackgroundTransform.SetParent(layoutGroupTransform);
            fieldBackgroundTransform.pivot = Vector2.zero;
            fieldBackgroundTransform.sizeDelta = new Vector2(0f, 60f);
            fieldBackgroundTransform.anchorMin = Vector2.zero;
            fieldBackgroundTransform.anchorMax = Vector2.right;

            _fieldBackground.AddComponent<Image>();

            LayoutElement fieldLayoutElement = _fieldBackground.AddComponent<LayoutElement>();
            fieldLayoutElement.minHeight = 60f;
            fieldLayoutElement.preferredHeight = 60f;
            fieldLayoutElement.flexibleHeight = -1f;

            #endregion
            
            #region Input field creation
            
            _inputField = UI.CreateInputField(fieldBackgroundTransform, "Type message...");
            _inputField.interactable = false;
            
            /*Object.Destroy(_inputField.GetComponent<DisableEditingOnMouseOver>());
            Object.Destroy(_inputField.placeholder.GetComponent<DisableEditingOnMouseOver>());
            Object.Destroy(_inputField.textComponent.GetComponent<DisableEditingOnMouseOver>());*/

            _inputField.GetComponent<DisableEditingOnMouseOver>().enabled = false;
            _inputField.placeholder.GetComponent<DisableEditingOnMouseOver>().enabled = false;
            _inputField.textComponent.GetComponent<DisableEditingOnMouseOver>().enabled = false;
            
            RectTransform fieldTransform = _inputField.GetComponent<RectTransform>();
            fieldTransform.pivot = Vector2.zero;
            fieldTransform.anchorMin = Vector2.zero;
            fieldTransform.anchorMax = Vector2.one;
            fieldTransform.sizeDelta = Vector2.zero;

            float x = _inputField.placeholder.rectTransform.sizeDelta.x;
            _inputField.placeholder.rectTransform.sizeDelta = new Vector2(x, -24f);
            x = _inputField.textComponent.rectTransform.sizeDelta.x;
            _inputField.textComponent.rectTransform.sizeDelta = new Vector2(x, -24f);
                
            _inputField.onEndEdit.AddListener(MessageSent);

            #endregion

            chatFocused = false;
        }

        private static void MessageSent(string text) {
            if(EventSystem.current.alreadySelecting) return;
            if(!string.IsNullOrWhiteSpace(text)) {
                // remove the current message so that if it's already in the history
                // it's moved to the end of the history
                history.Remove(text);
                history.Add(text);
                RemoveOldHistory();
                
                if(text[0] == '/') {
                    string command = text.Substring(1);
                    _client.ExecuteCommand(command);
                }
                else _client.SendChatMessage(text);
            }
            _inputField.text = null;
            chatFocused = false;
        }

        public static void AddMessage(string text) => AddMessage(new Message(_messagesContainer, text));
        public static void AddDebugMessage(string text) => AddMessage($"<color=grey><b>DEBUG:</b> {text}</color>");
        public static void AddWarningMessage(string text) => AddMessage($"<color=yellow><b>WARN:</b> {text}</color>");
        public static void AddErrorMessage(string text) => AddMessage($"<color=red><b>ERROR:</b> {text}</color>");

        private static void AddMessage(Message message) {
            messages.Add(message);
            RemoveOldMessages();
        }

        private static void RemoveOldMessages() {
            for(int i = 0; i < messages.Count - messagesCapacity; i++) {
                messages[i].Destroy();
                messages.RemoveAt(i);
            }
        }
        
        private static void RemoveOldHistory() {
            for(int i = 0; i < history.Count - historyCapacity; i++) history.RemoveAt(i);
        }

        public static void UpdateMessageHistory(bool up, bool down) {
            if(!chatFocused || !up && !down) return;

            int index = history.IndexOf(_inputField.text) + (up ? -1 : 1);

            while(index >= history.Count) index -= history.Count + 1;
            while(index < -1) index += history.Count + 1;
            
            _inputField.text = index >= 0 && index < history.Count ? history[index] : null;
            _inputField.caretPosition = _inputField.text?.Length ?? 0;
        }

        public static void UpdateMessagesFadeOut() {
            foreach(Message message in messages) {
                message.text.canvasRenderer.SetAlpha(chatFocused ? 1f :
                    Mathf.Lerp(1f, 0f, (Time.time - message.time - fadeOutDelay) * fadeOutSpeed));
            }
        }
    }
}
