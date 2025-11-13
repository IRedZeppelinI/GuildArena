using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Handlers;

public class DamageEffectHandler : IEffectHandler
{
    private readonly IStatCalculationService _statService;

    // 1. Rigoroso: Usamos Injeção de Dependência (DI) para obter os nossos serviços.
    public DamageEffectHandler(IStatCalculationService statService)
    {
        _statService = statService;
    }

    public EffectType SupportedType => EffectType.DAMAGE;


    /// <summary>
    /// Applies a DAMAGE effect by calculating scaled damage and applying it to the target.
    /// </summary>
    public void Apply(EffectDefinition def, Combatant source, Combatant target)
    {
        // 2. Rigoroso: Usamos o serviço para obter os valores
        float sourceStatValue = _statService.GetStatValue(source, def.ScalingStat);
        float targetDefense = _statService.GetStatValue(target, StatType.Defense);

        // 3. A lógica de negócio (responsabilidade única deste handler)
        float rawDamage = (sourceStatValue * def.ScalingFactor) + def.BaseAmount;
        float finalDamage = rawDamage - targetDefense;

        if (finalDamage < 1) finalDamage = 1;

        target.CurrentHP -= (int)finalDamage;
    }
}