using GuildArena.Core.Combat.Abstractions;
using GuildArena.Domain.Definitions;
using GuildArena.Domain.Entities;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Services;

public class HitChanceService : IHitChanceService
{
    private readonly IStatCalculationService _statService;
        
    private const float BaseChance = 1.0f;       // 100% base
    private const float OffenseFactor = 0.005f;  // +0.5% por ponto de ataque
    private const float DefenseFactor = 0.01f;   // -1.0% por ponto de defesa
    private const float LevelDeltaFactor = 0.02f; //2% de bónus/penalidade por cada nível de diferença
    private const float MinChance = 0.05f;       // Mínimo 5% sempre garantido
    private const float MaxChance = 1.0f;        // 100% Máximo

    public HitChanceService(IStatCalculationService statService)
    {
        _statService = statService;
    }

    public float CalculateHitChance(Combatant source, Combatant target, EffectDefinition effect)
    {
        //  (ex: Buffs, dano passivo, healing não podem ser evitados)        
        if (!effect.CanBeEvaded)
        {
            return 1.0f;
        }

        //Stats do duelo
        float sourceStatValue = 0;
        float targetStatValue = 0;

        switch (effect.Delivery)
        {
            case DeliveryMethod.Melee:                
                sourceStatValue = _statService.GetStatValue(source, StatType.Attack);
                targetStatValue = _statService.GetStatValue(target, StatType.Agility);
                break;

            case DeliveryMethod.Ranged:                
                sourceStatValue = _statService.GetStatValue(source, StatType.Agility);
                targetStatValue = _statService.GetStatValue(target, StatType.Agility);
                break;

            case DeliveryMethod.Spell:                
                sourceStatValue = _statService.GetStatValue(source, StatType.Magic);
                targetStatValue = _statService.GetStatValue(target, StatType.MagicDefense);
                break;

            default:                
                return 1.0f;
        }

        // calcular factor por diff de level
        int levelDiff = source.Level - target.Level;
        float levelCorrection = levelDiff * LevelDeltaFactor;

        
        float chance = BaseChance
                       + (sourceStatValue * OffenseFactor)
                       - (targetStatValue * DefenseFactor)
                       + levelCorrection;

        // 4. Clamping  5% e 100%
        return Math.Clamp(chance, MinChance, MaxChance);
    }
}