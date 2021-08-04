using System.Diagnostics.CodeAnalysis;

using Cat;

using CatsAreOnline.Shared;
using CatsAreOnline.Shared.StateTypes;

using Lidgren.Network;

using UnityEngine;

namespace CatsAreOnline.SyncedObjects {
    public abstract class SyncedObjectState {
        public Client client { get; set; }

        protected State movementCatState { get; set; }

        public virtual Vector2 position {
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

        public virtual Color color {
            get => _color;
            set {
                if(_color != value) {
                    _colorChanged = true;
                    anythingChanged = true;
                }
                _color = value;
            }
        }

        public virtual float scale {
            get => _scale;
            set {
                if(_scale != value) {
                    _scaleChanged = true;
                    anythingChanged = true;
                }
                _scale = value;
            }
        }

        public virtual float rotation {
            get => _rotation;
            set {
                if(_rotation != value) {
                    _rotationChanged = true;
                    anythingChanged = true;
                }
                _rotation = value;
            }
        }
        
        public bool anythingChanged { get; protected set; }

        public NetDeliveryMethod deliveryMethod {
            get => _deliveryMethod;
            protected set {
                if(value > _deliveryMethod) _deliveryMethod = value;
            }
        }
        
        private Vector2 _position;
        private Color _color;
        private float _scale;
        private float _rotation;
        
        private bool _positionChanged;
        private bool _colorChanged;
        private bool _scaleChanged;
        private bool _rotationChanged;
        private NetDeliveryMethod _deliveryMethod;

        private static Vector2 _prevPosition;
        private static bool _update;
        private static bool _moving;
        private static float _lastMovingUpdate;

        public abstract void Update();

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

        public virtual void Write(NetBuffer message) {
            message.Write(position);
            message.Write(color);
            message.Write(scale);
            message.Write(rotation);
        }
        
        [SuppressMessage("ReSharper", "InvertIf")]
        public virtual void WriteDeltaToMessage(NetOutgoingMessage message) {
            if(_positionChanged) {
                message.Write((byte)SyncedObjectStateType.Position);
                message.Write(position);
                _positionChanged = false;
                anythingChanged = false;
                deliveryMethod = DeliveryMethods.Global;
            }
            if(_colorChanged) {
                message.Write((byte)SyncedObjectStateType.Color);
                message.Write(color);
                _colorChanged = false;
                anythingChanged = false;
                deliveryMethod = DeliveryMethods.LessReliable;
            }
            if(_scaleChanged) {
                message.Write((byte)SyncedObjectStateType.Scale);
                message.Write(scale);
                _scaleChanged = false;
                anythingChanged = false;
                deliveryMethod = DeliveryMethods.LessReliable;
            }
            if(_rotationChanged) {
                message.Write((byte)SyncedObjectStateType.Rotation);
                message.Write(rotation);
                _rotationChanged = false;
                anythingChanged = false;
                deliveryMethod = DeliveryMethods.Global;
            }
        }
    }
}
