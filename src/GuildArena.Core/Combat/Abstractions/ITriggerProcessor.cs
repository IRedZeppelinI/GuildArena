using GuildArena.Core.Combat.ValueObjects;
using GuildArena.Domain.Enums;

namespace GuildArena.Core.Combat.Abstractions;

/// <summary>
/// Defines a service responsible for evaluating active modifiers against a specific event trigger
/// and orchestrating the execution of any resulting effects.
/// </summary>
public interface ITriggerProcessor
{
    /// <summary>
    /// Iterates through relevant combatants and their modifiers to find matches for the specified trigger.
    /// If a match is found and conditions are met, the associated ability is executed.
    /// </summary>
    /// <param name="trigger">The type of event that occurred (e.g., ON_RECEIVE_DAMAGE).</param>
    /// <param name="context">The data snapshot of the event.</param>
    void ProcessTriggers(ModifierTrigger trigger, TriggerContext context);
}