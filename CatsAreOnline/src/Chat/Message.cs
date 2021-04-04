﻿using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline.Chat {
    public class Message {
        public static Font font { get; set; }
        public Text text { get; private set; }
        public float time { get; private set; }

        //private static GameObject _template;

        public Message(Transform parent, string text) {
            /*if(_template) {
                GameObject obj = Object.Instantiate(_template, parent);
                Object.DontDestroyOnLoad(obj);
                this.text = obj.GetComponent<Text>();
            }
            else {*/
            GameObject obj = new GameObject("Message") { layer = LayerMask.NameToLayer("UI") };
            Object.DontDestroyOnLoad(obj);

            RectTransform transform = obj.AddComponent<RectTransform>();
            transform.SetParent(parent);
            transform.pivot = Vector2.zero;
            transform.localScale = Vector3.one;
            transform.sizeDelta = new Vector2(0f, 30f);

            this.text = obj.AddComponent<Text>();
            this.text.font = font;
            this.text.alignment = TextAnchor.LowerLeft;
            this.text.fontSize = 28;
            this.text.supportRichText = true;
            this.text.resizeTextForBestFit = true;
            this.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            this.text.verticalOverflow = VerticalWrapMode.Overflow;

            /*    _template = obj;
            }*/

            this.text.text = text;
            time = Time.time;
        }

        public void Destroy() => Object.Destroy(text.transform.gameObject);
    }
}
