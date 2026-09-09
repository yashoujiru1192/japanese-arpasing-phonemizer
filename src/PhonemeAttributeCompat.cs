using System.Reflection;
using OpenUtau.Api;

namespace JAtoKOPhonemizer;

/// <summary>
/// Reads phoneme attributes without binding the plugin IL to a specific field
/// signature. OpenUtau 0.1.568 uses int toneShift, while 0.1.569 uses int?.
/// </summary>
internal static class PhonemeAttributeCompat {
    private const BindingFlags MemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static int GetIndex(Phonemizer.PhonemeAttributes attributes) =>
        ReadInt(attributes, "index") ?? 0;

    internal static int GetToneShift(
            Phonemizer.PhonemeAttributes attributes,
            Phonemizer owner) {
        var value = ReadInt(attributes, "toneShift");
        if (value.HasValue) {
            return value.Value;
        }
        return InvokeParentInt(owner, "GetParentToneShift") ?? 0;
    }

    internal static int? GetAlternate(
            Phonemizer.PhonemeAttributes attributes,
            Phonemizer owner) =>
        ReadInt(attributes, "alternate")
            ?? InvokeParentInt(owner, "GetParentAlternate");

    internal static string GetVoiceColor(
            Phonemizer.PhonemeAttributes attributes,
            Phonemizer owner) {
        if (TryReadMember(attributes, "voiceColor", out var value) && value is string color) {
            return color;
        }
        return InvokeParent<string>(owner, "GetParentVoiceColor") ?? string.Empty;
    }

    private static int? ReadInt<T>(T attributes, string memberName) where T : struct {
        if (!TryReadMember(attributes, memberName, out var value) || value is null) {
            return null;
        }
        return value switch {
            int intValue => intValue,
            IConvertible convertible => Convert.ToInt32(convertible),
            _ => null,
        };
    }

    private static bool TryReadMember<T>(
            T attributes,
            string memberName,
            out object? value) where T : struct {
        object boxed = attributes;
        var type = boxed.GetType();
        var field = type.GetField(memberName, MemberFlags);
        if (field is not null) {
            value = field.GetValue(boxed);
            return true;
        }
        var property = type.GetProperty(memberName, MemberFlags);
        if (property is not null) {
            value = property.GetValue(boxed);
            return true;
        }
        value = null;
        return false;
    }

    private static T? InvokeParent<T>(Phonemizer owner, string methodName) {
        try {
            var method = owner.GetType().GetMethod(methodName, MemberFlags);
            if (method?.GetParameters().Length != 0) {
                return default;
            }
            var value = method.Invoke(owner, null);
            if (value is T typed) {
                return typed;
            }
            if (value is null) {
                return default;
            }
            return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
        } catch {
            return default;
        }
    }

    private static int? InvokeParentInt(Phonemizer owner, string methodName) {
        try {
            var method = owner.GetType().GetMethod(methodName, MemberFlags);
            if (method?.GetParameters().Length != 0) {
                return null;
            }
            var value = method.Invoke(owner, null);
            return value switch {
                int intValue => intValue,
                IConvertible convertible => Convert.ToInt32(convertible),
                _ => null,
            };
        } catch {
            return null;
        }
    }
}
