using System;
using System.Reflection;

namespace SubnauticaSpeedrunningRanked.Runtime.Seeds
{
    internal sealed class RankedSeedLifepodPlacementRuntime
    {
        private const int MaxAttempts = 360;
        private const float PositionTolerance = 0.5f;
        private const int StableTicksRequired = 8;

        private readonly Type _escapePodType;
        private readonly Type _componentType;
        private readonly Type _transformType;
        private readonly Type _vector3Type;
        private readonly Type _rigidbodyType;

        private readonly FieldInfo _escapePodMainField;
        private readonly PropertyInfo _escapePodMainProperty;
        private readonly PropertyInfo _componentTransformProperty;
        private readonly PropertyInfo _transformPositionProperty;
        private readonly FieldInfo _vector3XField;
        private readonly FieldInfo _vector3YField;
        private readonly FieldInfo _vector3ZField;
        private readonly MethodInfo _escapePodStartAtPositionMethod;
        private readonly FieldInfo _escapePodRigidBodyField;
        private readonly PropertyInfo _rigidbodyVelocityProperty;
        private readonly PropertyInfo _rigidbodyAngularVelocityProperty;
        private readonly MethodInfo _rigidbodySleepMethod;
        private readonly bool _supported;

        private bool _pending;
        private float _targetX;
        private float _targetZ;
        private int _remainingAttempts;
        private bool _appliedForCurrentSchedule;
        private string _lastAppliedTargetKey = string.Empty;
        private int _stableTicksAtTarget;
        private string _description = string.Empty;

        public RankedSeedLifepodPlacementRuntime()
        {
            _escapePodType = Type.GetType("EscapePod, Assembly-CSharp");
            _componentType = Type.GetType("UnityEngine.Component, UnityEngine");
            _transformType = Type.GetType("UnityEngine.Transform, UnityEngine");
            _vector3Type = Type.GetType("UnityEngine.Vector3, UnityEngine");
            _rigidbodyType = Type.GetType("UnityEngine.Rigidbody, UnityEngine");

            _escapePodMainField = FindStaticField(_escapePodType, "main", "_main");
            _escapePodMainProperty = FindStaticProperty(_escapePodType, "main");
            _componentTransformProperty = FindInstanceProperty(_componentType, "transform");
            _transformPositionProperty = FindInstanceProperty(_transformType, "position");
            _vector3XField = FindInstanceField(_vector3Type, "x", "X");
            _vector3YField = FindInstanceField(_vector3Type, "y", "Y");
            _vector3ZField = FindInstanceField(_vector3Type, "z", "Z");
            _escapePodStartAtPositionMethod = FindInstanceMethod(_escapePodType, "StartAtPosition", new[] { _vector3Type });
            _escapePodRigidBodyField = FindInstanceField(_escapePodType, "rigidbodyComponent", "rigidbody");
            _rigidbodyVelocityProperty = FindInstanceProperty(_rigidbodyType, "velocity");
            _rigidbodyAngularVelocityProperty = FindInstanceProperty(_rigidbodyType, "angularVelocity");
            _rigidbodySleepMethod = FindInstanceMethod(_rigidbodyType, "Sleep", Type.EmptyTypes);

            _supported =
                _escapePodType != null &&
                (_escapePodMainField != null || _escapePodMainProperty != null) &&
                (_escapePodStartAtPositionMethod != null || (_componentTransformProperty != null && _transformPositionProperty != null)) &&
                _vector3Type != null &&
                _vector3XField != null &&
                _vector3YField != null &&
                _vector3ZField != null;
        }

        public void Reset()
        {
            _pending = false;
            _remainingAttempts = 0;
            _appliedForCurrentSchedule = false;
            _stableTicksAtTarget = 0;
            _description = string.Empty;
        }

        public void Schedule(float targetX, float targetZ, string description)
        {
            if (!_supported)
            {
                return;
            }

            string targetKey = BuildTargetKey(targetX, targetZ);
            if (_pending &&
                ApproximatelyEquals(_targetX, targetX) &&
                ApproximatelyEquals(_targetZ, targetZ))
            {
                _description = description ?? string.Empty;
                return;
            }

            _targetX = targetX;
            _targetZ = targetZ;
            _description = description ?? string.Empty;
            _pending = true;
            _remainingAttempts = MaxAttempts;
            _appliedForCurrentSchedule = false;
            _stableTicksAtTarget = 0;

            RankedLog.Info("Scheduled seeded lifepod spawn placement for target " + targetKey + ": " + _description + ".");
        }

        public void Update(bool canApplyPlacement)
        {
            if (!_pending || !_supported)
            {
                return;
            }

            if (!canApplyPlacement)
            {
                return;
            }

            if (TryApply())
            {
                if (!_appliedForCurrentSchedule)
                {
                    _appliedForCurrentSchedule = true;
                    string targetKey = BuildTargetKey(_targetX, _targetZ);
                    if (!string.Equals(targetKey, _lastAppliedTargetKey, StringComparison.Ordinal))
                    {
                        _lastAppliedTargetKey = targetKey;
                        RankedLog.Info("Applied seeded lifepod spawn: " + _description + ".");
                    }
                }

                if (IsAtTarget())
                {
                    _stableTicksAtTarget++;
                }
                else
                {
                    _stableTicksAtTarget = 0;
                }

                if (_stableTicksAtTarget >= StableTicksRequired)
                {
                    RankedLog.Info("Confirmed seeded lifepod spawn at target " + BuildTargetKey(_targetX, _targetZ) + ".");
                    _pending = false;
                }

                return;
            }

            _remainingAttempts--;
            if (_remainingAttempts <= 0)
            {
                RankedLog.Warn("Timed out waiting for lifepod instance while applying seeded lifepod spawn: " + _description + ".");
                Reset();
            }
        }

        private bool TryApply()
        {
            object escapePod = GetEscapePodMain();
            if (escapePod == null)
            {
                return false;
            }

            if (_escapePodStartAtPositionMethod != null)
            {
                try
                {
                    object startPosition = CreateVector3(_targetX, 0f, _targetZ);
                    _escapePodStartAtPositionMethod.Invoke(escapePod, new[] { startPosition });
                    StabilizeRigidbody(escapePod);
                    return true;
                }
                catch
                {
                }
            }

            object transform;
            try
            {
                transform = _componentTransformProperty.GetValue(escapePod, null);
            }
            catch
            {
                transform = null;
            }

            if (transform == null)
            {
                return false;
            }

            object currentPosition;
            try
            {
                currentPosition = _transformPositionProperty.GetValue(transform, null);
            }
            catch
            {
                currentPosition = null;
            }

            if (currentPosition == null)
            {
                return false;
            }

            float y = ReadVectorAxis(_vector3YField, currentPosition);

            try
            {
                object nextPosition = CreateVector3(_targetX, y, _targetZ);
                _transformPositionProperty.SetValue(transform, nextPosition, null);
                StabilizeRigidbody(escapePod);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsAtTarget()
        {
            object escapePod = GetEscapePodMain();
            if (escapePod == null || _componentTransformProperty == null || _transformPositionProperty == null)
            {
                return false;
            }

            object transform;
            try
            {
                transform = _componentTransformProperty.GetValue(escapePod, null);
            }
            catch
            {
                transform = null;
            }

            if (transform == null)
            {
                return false;
            }

            object currentPosition;
            try
            {
                currentPosition = _transformPositionProperty.GetValue(transform, null);
            }
            catch
            {
                currentPosition = null;
            }

            if (currentPosition == null)
            {
                return false;
            }

            float x = ReadVectorAxis(_vector3XField, currentPosition);
            float z = ReadVectorAxis(_vector3ZField, currentPosition);
            return Math.Abs(x - _targetX) <= PositionTolerance &&
                   Math.Abs(z - _targetZ) <= PositionTolerance;
        }

        private object GetEscapePodMain()
        {
            if (_escapePodMainField != null)
            {
                try
                {
                    object fieldValue = _escapePodMainField.GetValue(null);
                    if (fieldValue != null)
                    {
                        return fieldValue;
                    }
                }
                catch
                {
                }
            }

            if (_escapePodMainProperty != null)
            {
                try
                {
                    return _escapePodMainProperty.GetValue(null, null);
                }
                catch
                {
                }
            }

            return null;
        }

        private object CreateVector3(float x, float y, float z)
        {
            object vector = Activator.CreateInstance(_vector3Type);
            _vector3XField.SetValue(vector, x);
            _vector3YField.SetValue(vector, y);
            _vector3ZField.SetValue(vector, z);
            return vector;
        }

        private void StabilizeRigidbody(object escapePod)
        {
            if (escapePod == null || _escapePodRigidBodyField == null)
            {
                return;
            }

            object rigidbody;
            try
            {
                rigidbody = _escapePodRigidBodyField.GetValue(escapePod);
            }
            catch
            {
                rigidbody = null;
            }

            if (rigidbody == null)
            {
                return;
            }

            if (_rigidbodyVelocityProperty != null)
            {
                try
                {
                    _rigidbodyVelocityProperty.SetValue(rigidbody, CreateVector3(0f, 0f, 0f), null);
                }
                catch
                {
                }
            }

            if (_rigidbodyAngularVelocityProperty != null)
            {
                try
                {
                    _rigidbodyAngularVelocityProperty.SetValue(rigidbody, CreateVector3(0f, 0f, 0f), null);
                }
                catch
                {
                }
            }

            if (_rigidbodySleepMethod != null)
            {
                try
                {
                    _rigidbodySleepMethod.Invoke(rigidbody, null);
                }
                catch
                {
                }
            }
        }

        private static float ReadVectorAxis(FieldInfo axisField, object vectorValue)
        {
            if (axisField == null || vectorValue == null)
            {
                return 0f;
            }

            try
            {
                object value = axisField.GetValue(vectorValue);
                return Convert.ToSingle(value);
            }
            catch
            {
                return 0f;
            }
        }

        private static string BuildTargetKey(float x, float z)
        {
            return x.ToString("0.###") + "|" + z.ToString("0.###");
        }

        private static bool ApproximatelyEquals(float left, float right)
        {
            return Math.Abs(left - right) <= 0.01f;
        }

        private static FieldInfo FindInstanceField(Type type, params string[] names)
        {
            return FindField(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, names);
        }

        private static FieldInfo FindStaticField(Type type, params string[] names)
        {
            return FindField(type, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, names);
        }

        private static FieldInfo FindField(Type type, BindingFlags flags, params string[] names)
        {
            if (type == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (IsBlank(name))
                {
                    continue;
                }

                Type current = type;
                while (current != null)
                {
                    FieldInfo field = current.GetField(name, flags | BindingFlags.DeclaredOnly);
                    if (field != null)
                    {
                        return field;
                    }

                    current = current.BaseType;
                }
            }

            return null;
        }

        private static PropertyInfo FindInstanceProperty(Type type, params string[] names)
        {
            return FindProperty(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, names);
        }

        private static PropertyInfo FindStaticProperty(Type type, params string[] names)
        {
            return FindProperty(type, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, names);
        }

        private static PropertyInfo FindProperty(Type type, BindingFlags flags, params string[] names)
        {
            if (type == null || names == null)
            {
                return null;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (IsBlank(name))
                {
                    continue;
                }

                Type current = type;
                while (current != null)
                {
                    PropertyInfo property = current.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                    if (property != null)
                    {
                        return property;
                    }

                    current = current.BaseType;
                }
            }

            return null;
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, Type[] parameterTypes)
        {
            if (type == null || IsBlank(name))
            {
                return null;
            }

            Type current = type;
            while (current != null)
            {
                MethodInfo method = current.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null,
                    parameterTypes,
                    null);
                if (method != null)
                {
                    return method;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static bool IsBlank(string value)
        {
            return value == null || value.Trim().Length == 0;
        }
    }
}
