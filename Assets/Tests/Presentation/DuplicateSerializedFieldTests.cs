using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Reflection guard against the Unity serialization error
    /// "The same field name is serialized multiple times in the class or its parent class".
    ///
    /// This compiles fine — it only fails at runtime when Unity deserializes the component — so a
    /// unit test is the only cheap way to catch it. It was added after a derived NPC behaviour
    /// declared a `config` field that shadowed its base's `config`.
    ///
    /// Unity setup: none. Runs in EditMode with no scene.
    /// </summary>
    public class DuplicateSerializedFieldTests
    {
        [Test]
        public void No_Serialized_Type_Shadows_A_Base_Class_Field()
        {
            Assembly assembly = typeof(GameSessionHost).Assembly;

            var problems = new List<string>();
            foreach (Type type in assembly.GetTypes())
            {
                if (!IsUnitySerialized(type))
                    continue;

                problems.AddRange(DuplicateFieldProblems(type));
            }

            Assert.IsEmpty(
                problems,
                "Fields serialized in both a derived and a base class break Unity deserialization:\n" +
                string.Join("\n", problems));
        }

        private static bool IsUnitySerialized(Type type)
        {
            if (type.IsGenericTypeDefinition || type.IsEnum || type.IsPrimitive || type.IsInterface)
                return false;
            if (type.Name.Contains('<') || type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
                return false;

            return typeof(MonoBehaviour).IsAssignableFrom(type)
                || typeof(ScriptableObject).IsAssignableFrom(type)
                || type.IsDefined(typeof(SerializableAttribute), false);
        }

        private static IEnumerable<string> DuplicateFieldProblems(Type type)
        {
            // Only the type's own inheritance chain inside this assembly is serialized together.
            var declared = new List<FieldInfo>();
            for (Type current = type; current != null && current.Assembly == type.Assembly; current = current.BaseType)
                declared.AddRange(SerializedFields(current));

            var duplicates = declared
                .GroupBy(f => f.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (string name in duplicates)
            {
                yield return $"{type.FullName}: '{name}' is serialized in both a derived and a base class.";
            }
        }

        private static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.IsStatic || field.IsLiteral || field.IsInitOnly)
                    continue;
                if (field.IsDefined(typeof(NonSerializedAttribute), false))
                    continue;

                bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), false);
                if (serialized)
                    yield return field;
            }
        }
    }
}
