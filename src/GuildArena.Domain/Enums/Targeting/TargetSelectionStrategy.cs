namespace GuildArena.Domain.Enums.Targeting;

/// <summary>
/// Defines the strategy used to select targets automatically if manual selection is not used.
/// </summary>
public enum TargetSelectionStrategy
{
    Manual,          
    Random,          // entre os válidos
    LowestHP,        
    HighestHP,       
    LowestHPPercent, 
    HighestHPPercent 
}
