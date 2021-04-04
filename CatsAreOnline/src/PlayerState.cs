using System.Diagnostics.CodeAnalysis;

using Cat;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline {
    public class PlayerState {
        public enum Type : byte {
            Position,
            Room,
            Color,
            Scale,
            Ice,
            IceRotation
        }
        
        public State movementCatState { get; set; }

        public Vector2 position {
            get => _position;
            set {
                _position = value;
                UpdateMovementStatus(movementCatState, 0.2f, 3f);
                if(!_moving) return;
                _positionChanged = true;
                anythingChanged = true;
                _prevPosition = _position;
            }
        }

        public string room {
            get => _room;
            set {
                if(_room != value) {
                    _roomChanged = true;
                    anythingChanged = true;
                    Client.RoomChanged();
                }
                _room = value;
            }
        }

        public Color color {
            get => _color;
            set {
                if(_color != value) {
                    _colorChanged = true;
                    anythingChanged = true;
                }
                _color = value;
            }
        }

        public float scale {
            get => _scale;
            set {
                if(_scale != value) {
                    _scaleChanged = true;
                    anythingChanged = true;
                }
                _scale = value;
            }
        }

        public bool ice {
            get => _ice;
            set {
                if(_ice != value) {
                    _iceChanged = true;
                    anythingChanged = true;
                }
                _ice = value;
            }
        }

        public float iceRotation {
            get => _iceRotation;
            set {
                if(!ice) return;
                if(_iceRotation != value) {
                    _iceRotationChanged = true;
                    anythingChanged = true;
                }
                _iceRotation = value;
            }
        }
        
        public bool anythingChanged { get; private set; }

        public NetDeliveryMethod deliveryMethod {
            get => _deliveryMethod;
            private set {
                if(value > _deliveryMethod) _deliveryMethod = value;
            }
        }

        private Vector2 _position;
        private string _room;
        private Color _color;
        private float _scale;
        private bool _ice;
        private float _iceRotation;

        private bool _positionChanged;
        private bool _roomChanged;
        private bool _colorChanged;
        private bool _scaleChanged;
        private bool _iceChanged;
        private bool _iceRotationChanged;
        private NetDeliveryMethod _deliveryMethod;

        private static Vector2 _prevPosition;
        private static bool _update;
        private static bool _moving;
        private static float _lastMovingUpdate;

        private void UpdateMovementStatus(State state, float deltaThreshold, float stayTime) {
            if(!_update && state == State.Normal) {
                Vector2 posDelta = _prevPosition - _position;
                bool posCheck = Mathf.Abs(posDelta.y) >= deltaThreshold || Mathf.Abs(posDelta.x) >= deltaThreshold;
                if(posCheck) {
                    _update = true;
                    _lastMovingUpdate = Time.unscaledTime;
                }
            }
            
            _moving = (state != State.Normal || _update) && _prevPosition != _position;

            if(_update && Time.unscaledTime - _lastMovingUpdate > stayTime) _update = false;
        }

        [SuppressMessage("ReSharper", "InvertIf")]
        public void WriteDeltaToMessage(NetOutgoingMessage message) {
            if(_positionChanged) {
                message.Write((byte)Type.Position);
                message.Write(position);
                _positionChanged = false;
                anythingChanged = false;
                deliveryMethod = Client.GlobalDeliveryMethod;
            }
            if(_roomChanged) {
                message.Write((byte)Type.Room);
                message.Write(room);
                _roomChanged = false;
                anythingChanged = false;
                deliveryMethod = Client.LessReliableDeliveryMethod;
            }
            if(_colorChanged) {
                message.Write((byte)Type.Color);
                message.Write(color);
                _colorChanged = false;
                anythingChanged = false;
                deliveryMethod = Client.LessReliableDeliveryMethod;
            }
            if(_scaleChanged) {
                message.Write((byte)Type.Scale);
                message.Write(scale);
                _scaleChanged = false;
                anythingChanged = false;
                deliveryMethod = Client.LessReliableDeliveryMethod;
            }
            if(_iceChanged) {
                message.Write((byte)Type.Ice);
                message.Write(ice);
                _iceChanged = false;
                anythingChanged = false;
                deliveryMethod = Client.LessReliableDeliveryMethod;
            }
            if(_iceRotationChanged) {
                message.Write((byte)Type.IceRotation);
                message.Write(iceRotation);
                _iceRotationChanged = false;
                anythingChanged = false;
                deliveryMethod = Client.GlobalDeliveryMethod;
            }
        }
    }
}
