using System;
using System.Diagnostics;

using UnityEngine;
using UnityEngine.UI;

namespace CatsAreOnline {
    public class Player : MonoBehaviour {
        public readonly struct InterpolationSettings {
            public enum InterpolationMode { Lerp, LerpUnclamped, Velocity }
            
            public InterpolationMode mode { get; }
            public double time { get; }
            public int packetsToAverage { get; }
            public double maxTime { get; }

            public InterpolationSettings(InterpolationMode mode, double time, int packetsToAverage, double maxTime) {
                this.mode = mode;
                this.time = time;
                this.packetsToAverage = packetsToAverage;
                this.maxTime = maxTime;
            }
        }
        
        public string username { get; set; }
        public string displayName { get; set; }
        public Text nameTag { get; set; }
        public SpriteRenderer renderer { get; set; }
        public Rigidbody2D rigidbody { get; set; }
        public CircleCollider2D collider { get; set; }
        public PlayerState state { get; } = new PlayerState();
        
        public bool restoreFollowPlayerHead { get; set; }
        public Transform restoreFollowTarget { get; set; }

        public static InterpolationSettings interpolationSettings { get; set; }

        private double _setPositionTime = Time.fixedDeltaTime;
        private double _toAverage;
        private int _toAverageCount;
        private Stopwatch _interpolateStopwatch;
        private Vector3 _fromPosition;

        private void FixedUpdate() {
            switch(interpolationSettings.mode) {
                case InterpolationSettings.InterpolationMode.Lerp:
                    rigidbody.MovePosition(Vector3.Lerp(_fromPosition, state.position,
                        (float)(_interpolateStopwatch.Elapsed.TotalSeconds /
                                (_setPositionTime * interpolationSettings.time))));
                    break;
                case InterpolationSettings.InterpolationMode.LerpUnclamped:
                    rigidbody.MovePosition(Vector3.LerpUnclamped(_fromPosition, state.position,
                        (float)(_interpolateStopwatch.Elapsed.TotalSeconds /
                                (_setPositionTime * interpolationSettings.time))));
                    break;
                case InterpolationSettings.InterpolationMode.Velocity:
                    rigidbody.velocity = (state.position - (Vector2)_fromPosition) /
                                         (float)(_setPositionTime * interpolationSettings.time);
                    break;
            }
        }

        public void SetPosition(Vector2 position) {
            state.position = position;
            _fromPosition = transform.position;
            _toAverage += Math.Min(_interpolateStopwatch?.Elapsed.TotalSeconds ?? Time.fixedDeltaTime,
                Time.fixedDeltaTime * interpolationSettings.maxTime);
            _toAverageCount++;
            if(_toAverageCount >= interpolationSettings.packetsToAverage) {
                _setPositionTime = _toAverage / _toAverageCount;
                _toAverage = 0d;
                _toAverageCount = 0;
            }
            if(_interpolateStopwatch == null) _interpolateStopwatch = Stopwatch.StartNew();
            else _interpolateStopwatch.Restart();
        }

        public void SetRoom(string room, string currentClientRoom) {
            bool sameRoom = currentClientRoom == room && !string.IsNullOrEmpty(currentClientRoom);
            bool own = !state.client.displayOwnCat && username == state.client.username;
            state.room = room;
            gameObject.SetActive(!own && sameRoom);
            nameTag.gameObject.SetActive(sameRoom);
            collider.enabled = username != state.client.username && state.client.playerCollisions;
            
            if(sameRoom || FollowPlayer.customFollowTarget != transform) return;
            FollowPlayer.followPlayerHead = restoreFollowPlayerHead;
            FollowPlayer.customFollowTarget = restoreFollowTarget;
            state.client.spectating = null;
            Chat.Chat.AddMessage($"Stopped spectating <b>{username}</b> (room changed)");
        }

        public void SetColor(Color color) {
            state.color = color;
            renderer.color = color;
        }

        public void SetScale(float scale) {
            state.scale = scale;
            transform.localScale = Vector3.one * scale;
        }

        public void SetIce(bool ice) {
            state.ice = ice;
            renderer.sprite = ice ? state.client.iceSprite : state.client.catSprite;
            if(!ice) transform.eulerAngles = Vector3.zero;
        }

        public void SetIceRotation(float iceRotation) {
            if(!state.ice) return;

            state.iceRotation = iceRotation;
            rigidbody.MoveRotation(iceRotation);
        }
    }
}
