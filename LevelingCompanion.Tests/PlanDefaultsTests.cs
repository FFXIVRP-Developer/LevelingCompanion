namespace LevelingCompanion.Tests;

/// <summary>
///     The default plan (2026-10-06: one slave's plan to every character: "sync the settings to Me, that way all slaves get it
///     too"; plans are per character, so a copied file alone reached nobody).
/// </summary>
public sealed class PlanDefaultsTests
{
    private static Dictionary<int, List<SkillRef>> Plan() => new()
    {
        [1] = [new SkillRef(Tree.Healer, 1)],
        [9] = [new SkillRef(Tree.Attacker, 1), new SkillRef(Tree.Attacker, 2)],
    };

    [Fact]
    public void GivenACharacterWithNothingPlanned_ExpectTheDefaultCopiedIn()
    {
        var plan = new CharacterPlan();
        Assert.True(PlanDefaults.FillFromDefault(plan, Plan()));
        Assert.Equal(2, plan.Ranks.Count);
        Assert.Equal(2, plan.Ranks[9].Count);
    }

    [Fact]
    public void GivenACharacterWithItsOwnPlan_ExpectItNeverTouched()
    {
        var plan = new CharacterPlan { Ranks = new() { [3] = [new SkillRef(Tree.Defender, 1)] } };
        Assert.False(PlanDefaults.FillFromDefault(plan, Plan()));
        Assert.Equal([3], plan.Ranks.Keys);
    }

    [Fact]
    public void GivenNoDefault_ExpectNothingFilled() =>
        Assert.False(PlanDefaults.FillFromDefault(new CharacterPlan(), new()));

    [Fact]
    public void GivenACopiedDefault_ExpectEditsToTheCharactersPlanToLeaveTheDefaultAlone()
    {
        var defaults = Plan();
        var plan = new CharacterPlan();
        PlanDefaults.FillFromDefault(plan, defaults);
        plan.Ranks[9].Clear();
        Assert.Equal(2, defaults[9].Count);
    }

    [Fact]
    public void GivenUseAsDefault_ExpectTheDefaultACopyOfThePlan()
    {
        var config = new Configuration();
        var plan = new CharacterPlan { Ranks = Plan() };
        PlanDefaults.UseAsDefault(config, plan);
        plan.Ranks.Remove(1);
        Assert.Equal(2, config.DefaultRanks.Count);
    }

    [Fact]
    public void GivenTheDefault_ExpectItDescribedBySkillsAndRanks() =>
        Assert.Equal("default plan: 3 skill(s) over 2 rank(s)", PlanDefaults.Describe(Plan()));
}