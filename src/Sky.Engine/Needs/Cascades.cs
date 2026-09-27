namespace Sky.Engine.Needs;

/// <summary>One cascade: a source need strictly above its threshold adds a Cascade-class multiplier to a target need's rate.</summary>
/// <param name="Source">The need whose value is tested.</param>
/// <param name="Target">The need the modifier lands on.</param>
/// <param name="Threshold">The value the source must be strictly above for the rule to fire, in [0, 100).</param>
/// <param name="Factor">The multiplier the rule adds to the target's rate, finite and above 0.</param>
/// <param name="Condition">The cabin condition that must also hold, beyond the source's value.</param>
public sealed record CascadeRule(Need Source, Need Target, double Threshold, double Factor, CascadeCondition Condition);

/// <summary>
/// The flight's cascade rules, validated once at construction and copied. Each tick a caller asks which modifiers one
/// target need takes; every rule whose source stands strictly above its threshold and whose condition holds contributes
/// one <see cref="SourceClass.Cascade"/> factor to that target, in rule order.
/// </summary>
public sealed class Cascades
{
    private const double MaxThreshold = 100.0;

    private readonly CascadeRule[] rules;

    /// <summary>Validates <paramref name="rules"/> and copies them.</summary>
    /// <param name="rules">The cascade rules, in the order their modifiers are written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
    /// <exception cref="ArgumentException">A rule is invalid or null; the message names the rule's index.</exception>
    public Cascades(IReadOnlyList<CascadeRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Validate(rules, nameof(rules));
        this.rules = [.. rules];
    }

    /// <summary>Gets how many rules target a need, for sizing a caller's buffer.</summary>
    /// <param name="target">The need the modifiers land on.</param>
    /// <returns>The number of rules whose target is <paramref name="target"/>, fired or not.</returns>
    public int MaxModifiersPerTarget(Need target)
    {
        int count = 0;
        foreach (CascadeRule rule in rules)
        {
            if (rule.Target == target)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Writes the modifiers <paramref name="target"/> takes this tick into <paramref name="buffer"/> from index 0, in
    /// rule order, and returns how many were written.
    /// </summary>
    /// <param name="needs">The passenger's needs, read but not changed.</param>
    /// <param name="target">The need the modifiers land on.</param>
    /// <param name="lavUnreachable">Whether the passenger cannot reach a lav this tick, from the caller's seating and queue state.</param>
    /// <param name="buffer">
    /// The buffer to write into. It is enforced to be at least as long as the number of rules that fire this tick, which
    /// <see cref="MaxModifiersPerTarget"/> always covers.
    /// </param>
    /// <returns>How many modifiers were written; 0 leaves the buffer's contents alone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="needs"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too short for the modifiers that fire.</exception>
    public int AppendModifiers(NeedSet needs, Need target, bool lavUnreachable, Span<RateModifier> buffer)
    {
        ArgumentNullException.ThrowIfNull(needs);
        int count = CountFiring(needs, target, lavUnreachable);
        if (buffer.Length < count)
        {
            throw new ArgumentException($"The buffer holds {buffer.Length} modifiers; {count} fire for {target}.", nameof(buffer));
        }

        int written = 0;
        foreach (CascadeRule rule in rules)
        {
            if (FiresFor(needs, rule, target, lavUnreachable))
            {
                buffer[written++] = new RateModifier(SourceClass.Cascade, rule.Factor);
            }
        }

        return written;
    }

    private static void Validate(IReadOnlyList<CascadeRule> rules, string paramName)
    {
        for (int index = 0; index < rules.Count; index++)
        {
            CascadeRule rule = rules[index] ?? throw new ArgumentException($"Rule {index} is null.", paramName);
            Require(
                IsDefined(rule.Source) && IsDefined(rule.Target),
                $"Rule {index} names need {rule.Source} driving {rule.Target}; both must be defined needs.",
                paramName
            );
            Require(
                rule.Source != rule.Target,
                $"Rule {index} has {rule.Source} driving itself; a cascade's source and target must differ.",
                paramName
            );
            Require(
                rule.Threshold >= 0.0 && rule.Threshold < MaxThreshold,
                $"Rule {index} has a threshold of {rule.Threshold}; it must be in [0, 100).",
                paramName
            );
            Require(
                double.IsFinite(rule.Factor) && rule.Factor > 0.0,
                $"Rule {index} has a factor of {rule.Factor}; it must be finite and above 0.",
                paramName
            );
            Require(
                Enum.IsDefined(rule.Condition),
                $"Rule {index} has condition {rule.Condition}, which is not a defined CascadeCondition.",
                paramName
            );
        }
    }

    private static void Require(bool holds, string message, string paramName)
    {
        if (!holds)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    private static bool IsDefined(Need need) => (uint)need < NeedSet.NeedCount;

    private int CountFiring(NeedSet needs, Need target, bool lavUnreachable)
    {
        int count = 0;
        foreach (CascadeRule rule in rules)
        {
            if (FiresFor(needs, rule, target, lavUnreachable))
            {
                count++;
            }
        }

        return count;
    }

    private static bool FiresFor(NeedSet needs, CascadeRule rule, Need target, bool lavUnreachable) =>
        rule.Target == target && Fires(needs, rule, lavUnreachable);

    private static bool Fires(NeedSet needs, CascadeRule rule, bool lavUnreachable)
    {
        if (!(needs[rule.Source] > rule.Threshold))
        {
            return false;
        }

        return rule.Condition switch
        {
            CascadeCondition.None => true,
            CascadeCondition.LavUnreachable => lavUnreachable,
            _ => throw new InvalidOperationException($"Cascade condition {rule.Condition} is not defined; construction should have refused it."),
        };
    }
}
