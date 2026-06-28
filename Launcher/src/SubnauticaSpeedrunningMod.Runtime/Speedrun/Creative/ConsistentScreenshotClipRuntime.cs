using System;
using System.Reflection;
using UnityEngine;

namespace SubnauticaSpeedrunningMod.Runtime.RunTracking
{
    internal sealed class ConsistentScreenshotClipRuntime
    {
        private const float ClipWindowSeconds = 0.25f;

        private readonly Type _playerType;
        private readonly Type _underwaterMotorType;
        private readonly FieldInfo _playerMainField;
        private readonly FieldInfo _playerMainColliderField;
        private readonly FieldInfo _playerRigidBodyField;
        private readonly FieldInfo _underwaterMotorCapsuleField;
        private readonly MethodInfo _playerIsUnderwaterForSwimmingMethod;
        private readonly MethodInfo _playerGetCurrentSubMethod;
        private readonly MethodInfo _playerGetModeMethod;
        private readonly Type _componentType;
        private readonly PropertyInfo _colliderEnabledProperty;
        private readonly PropertyInfo _rigidBodyDetectCollisionsProperty;
        private readonly MethodInfo _componentGetComponentMethod;

        private bool _clipWindowActive;
        private float _restoreAt;
        private bool _savedMainColliderEnabled;
        private bool _savedCapsuleColliderEnabled;
        private bool _savedRigidBodyDetectCollisions;
        private object _activeMainCollider;
        private object _activeCapsuleCollider;
        private object _activeRigidBody;

        public ConsistentScreenshotClipRuntime()
        {
            _playerType = ResolveType("Player");
            _underwaterMotorType = ResolveType("UnderwaterMotor");
            _playerMainField = FindStaticField(_playerType, "main");
            _playerMainColliderField = FindStaticField(_playerType, "mainCollider");
            _playerRigidBodyField = FindInstanceField(_playerType, "rigidBody");
            _underwaterMotorCapsuleField = FindInstanceField(_underwaterMotorType, "capsulecollider");
            _playerIsUnderwaterForSwimmingMethod = FindMethod(_playerType, "IsUnderwaterForSwimming");
            _playerGetCurrentSubMethod = FindMethod(_playerType, "GetCurrentSub");
            _playerGetModeMethod = FindMethod(_playerType, "GetMode");
            _componentType = Type.GetType("UnityEngine.Component, UnityEngine", false);
            Type colliderType = Type.GetType("UnityEngine.Collider, UnityEngine", false);
            Type rigidBodyType = Type.GetType("UnityEngine.Rigidbody, UnityEngine", false);
            _colliderEnabledProperty = colliderType == null ? null : colliderType.GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
            _rigidBodyDetectCollisionsProperty = rigidBodyType == null ? null : rigidBodyType.GetProperty("detectCollisions", BindingFlags.Instance | BindingFlags.Public);
            _componentGetComponentMethod = _componentType == null
                ? null
                : _componentType.GetMethod("GetComponent", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Type) }, null);
        }

        public void Update(bool enabled)
        {
            if (_clipWindowActive && Time.unscaledTime >= _restoreAt)
            {
                RestoreCollisionState();
            }

            if (!enabled || _clipWindowActive || !GameInput.GetButtonDown(GameInput.Button.TakePicture))
            {
                return;
            }

            TryOpenClipWindow();
        }

        public void Reset()
        {
            RestoreCollisionState();
        }

        private void TryOpenClipWindow()
        {
            object player = _playerMainField == null ? null : _playerMainField.GetValue(null);
            if (player == null || !IsSwimmingPlayer(player))
            {
                return;
            }

            _activeMainCollider = _playerMainColliderField == null ? null : _playerMainColliderField.GetValue(null);
            _activeRigidBody = _playerRigidBodyField == null ? null : _playerRigidBodyField.GetValue(player);
            _activeCapsuleCollider = GetUnderwaterCapsuleCollider(player);

            _savedMainColliderEnabled = GetBoolProperty(_activeMainCollider, _colliderEnabledProperty, true);
            _savedCapsuleColliderEnabled = GetBoolProperty(_activeCapsuleCollider, _colliderEnabledProperty, true);
            _savedRigidBodyDetectCollisions = GetBoolProperty(_activeRigidBody, _rigidBodyDetectCollisionsProperty, true);

            if (_activeMainCollider == null && _activeCapsuleCollider == null && _activeRigidBody == null)
            {
                return;
            }

            _clipWindowActive = true;
            _restoreAt = Time.unscaledTime + ClipWindowSeconds;
            SetBoolProperty(_activeMainCollider, _colliderEnabledProperty, false);
            SetBoolProperty(_activeCapsuleCollider, _colliderEnabledProperty, false);
            SetBoolProperty(_activeRigidBody, _rigidBodyDetectCollisionsProperty, false);
        }

        private void RestoreCollisionState()
        {
            if (!_clipWindowActive)
            {
                return;
            }

            try
            {
                SetBoolProperty(_activeMainCollider, _colliderEnabledProperty, _savedMainColliderEnabled);
                SetBoolProperty(_activeCapsuleCollider, _colliderEnabledProperty, _savedCapsuleColliderEnabled);
                SetBoolProperty(_activeRigidBody, _rigidBodyDetectCollisionsProperty, _savedRigidBodyDetectCollisions);

                object player = _playerMainField == null ? null : _playerMainField.GetValue(null);
                object currentMainCollider = _playerMainColliderField == null ? null : _playerMainColliderField.GetValue(null);
                object currentRigidBody = player == null || _playerRigidBodyField == null ? null : _playerRigidBodyField.GetValue(player);
                object currentCapsuleCollider = GetUnderwaterCapsuleCollider(player);

                SetBoolProperty(currentMainCollider, _colliderEnabledProperty, true);
                SetBoolProperty(currentCapsuleCollider, _colliderEnabledProperty, true);
                SetBoolProperty(currentRigidBody, _rigidBodyDetectCollisionsProperty, true);
            }
            catch
            {
            }
            finally
            {
                _clipWindowActive = false;
                _restoreAt = 0f;
                _activeMainCollider = null;
                _activeCapsuleCollider = null;
                _activeRigidBody = null;
            }
        }

        private object GetUnderwaterCapsuleCollider(object player)
        {
            if (player == null || _underwaterMotorCapsuleField == null || _componentGetComponentMethod == null || _underwaterMotorType == null)
            {
                return null;
            }

            try
            {
                object underwaterMotor = _componentGetComponentMethod.Invoke(player, new object[] { _underwaterMotorType });
                return underwaterMotor == null ? null : _underwaterMotorCapsuleField.GetValue(underwaterMotor);
            }
            catch
            {
                return null;
            }
        }

        private bool IsSwimmingPlayer(object player)
        {
            try
            {
                if (_playerIsUnderwaterForSwimmingMethod == null)
                {
                    return false;
                }

                object isSwimmingResult = _playerIsUnderwaterForSwimmingMethod.Invoke(player, null);
                if (!(isSwimmingResult is bool) || !(bool)isSwimmingResult)
                {
                    return false;
                }

                if (_playerGetCurrentSubMethod != null && _playerGetCurrentSubMethod.Invoke(player, null) != null)
                {
                    return false;
                }

                if (_playerGetModeMethod != null)
                {
                    object mode = _playerGetModeMethod.Invoke(player, null);
                    if (mode != null && !string.Equals(mode.ToString(), "Normal", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Type ResolveType(string typeName)
        {
            return Type.GetType(typeName + ", Assembly-CSharp", false) ?? Type.GetType(typeName, false);
        }

        private static FieldInfo FindStaticField(Type type, string name)
        {
            return type == null ? null : type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static FieldInfo FindInstanceField(Type type, string name)
        {
            return type == null ? null : type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            return type == null ? null : type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static bool GetBoolProperty(object instance, PropertyInfo property, bool defaultValue)
        {
            if (instance == null || property == null)
            {
                return defaultValue;
            }

            try
            {
                object value = property.GetValue(instance, null);
                return value is bool ? (bool)value : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private static void SetBoolProperty(object instance, PropertyInfo property, bool value)
        {
            if (instance == null || property == null)
            {
                return;
            }

            try
            {
                property.SetValue(instance, value, null);
            }
            catch
            {
            }
        }
    }
}
