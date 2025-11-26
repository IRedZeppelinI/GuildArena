using GuildArena.Domain.Enums;

namespace GuildArena.Domain.Extensions;

public static class StatusEffectRules
{
    /// <summary>
    /// Returns true if the status effect prevents ANY action.
    /// </summary>
    public static bool BlocksAllActions(this StatusEffectType status)
    {
        return status == StatusEffectType.Stun;
    }

    /// <summary>
    /// Returns true if the status effect prevents using specialized Skills (Non-Basic Attacks).
    /// </summary>
    public static bool BlocksSkills(this StatusEffectType status)
    {
        // Stun também bloqueia skills implicitamente, mas a regra específica é Silence
        return status == StatusEffectType.Stun || status == StatusEffectType.Silence;
    }

    /// <summary>
    /// Returns true if the status effect prevents using Basic Attacks.
    /// </summary>
    public static bool BlocksBasicAttack(this StatusEffectType status)
    {
        // Stun também bloqueia ataques, além do Disarm
        return status == StatusEffectType.Stun || status == StatusEffectType.Disarm;
    }
}