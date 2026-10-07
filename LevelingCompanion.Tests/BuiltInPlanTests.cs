namespace LevelingCompanion.Tests;

/// <summary>
///     2026-10-07: "make what [a slave] has for leveling companion leveling plan, the default plan and have a button to apply the default
///     plan whenever the user wants. if it doesnt match how the chocobo is leveled, try to level it back on track"; "not reset, just
///     try your best to catch up with the plan. whats done is done".
/// </summary>
public sealed class BuiltInPlanTests
{
    [Fact]
    public void GivenTheBuiltInPlan_ExpectEveryTreeToTenOverTwentyRanksWithNoProblem()
    {
        var plan = new CharacterPlan { Ranks = PlanDefaults.BuiltIn };
        Assert.Equal(20, plan.Ranks.Count);
        Assert.Equal(30, plan.Ranks.Values.Sum(s => s.Count));
        foreach (var tree in new[] { Tree.Defender, Tree.Attacker, Tree.Healer })
            Assert.Equal(Enumerable.Range(1, 10), plan.Ranks.Values.SelectMany(s => s).Where(s => s.Tree == tree).Select(s => s.Level).Order());
        Assert.All(Planner.Check(plan).Values, c => Assert.Empty(c.Problems));
    }

    [Fact]
    public void GivenTheBuiltInPlan_ExpectHealerFirst() =>
        Assert.Equal(new SkillRef(Tree.Healer, 1), PlanDefaults.BuiltIn[1].Single());

    [Fact]
    public void GivenNoDefaultSavedInTheProfile_ExpectTheBuiltInPlan() =>
        Assert.Equal(PlanDefaults.BuiltIn.Count, PlanDefaults.Of(new Configuration()).Count);

    [Fact]
    public void GivenADefaultSavedInTheProfile_ExpectThatOne()
    {
        var config = new Configuration { DefaultRanks = new() { [3] = [new SkillRef(Tree.Defender, 1)] } };
        Assert.Equal([3], PlanDefaults.Of(config).Keys);
    }

    [Fact]
    public void GivenApplyTheDefault_ExpectThePlanReplacedAndTheHistoryKept()
    {
        var plan = new CharacterPlan
        {
            Ranks = new() { [5] = [new SkillRef(Tree.Attacker, 1)] },
            History = [new LearnRecord(new SkillRef(Tree.Attacker, 1), 5, 5, DateTime.Now)],
        };
        PlanDefaults.Apply(plan, PlanDefaults.BuiltIn);
        Assert.Equal(PlanDefaults.BuiltIn.Count, plan.Ranks.Count);
        Assert.Single(plan.History);
        plan.Ranks[1].Clear();
        Assert.Single(PlanDefaults.BuiltIn[1]); // a copy: the built-in plan stays as it is
    }

    [Fact]
    public void GivenTheChocoboLevelledOffThePlan_ExpectWhatThePlanWantedByNowStillToLearn()
    {
        // Rank 10, but the points went to Attacker 1-3 instead of Healer 4-8 (whats done is done: no reset).
        var state = new CompanionState(true, true, true, Rank: 10, SkillPoints: 0, Defender: 0, Attacker: 3, Healer: 3);
        var behind = Planner.Behind(new CharacterPlan { Ranks = PlanDefaults.BuiltIn }, state);
        Assert.Equal(new SkillRef(Tree.Healer, 4), behind[0]);
        Assert.Contains(new SkillRef(Tree.Defender, 1), behind);
        Assert.DoesNotContain(new SkillRef(Tree.Attacker, 2), behind); // learned already, off plan or not
    }

    [Fact]
    public void GivenItIsOnThePlan_ExpectNothingBehind()
    {
        var state = new CompanionState(true, true, true, Rank: 8, SkillPoints: 0, Defender: 0, Attacker: 0, Healer: 8);
        Assert.Empty(Planner.Behind(new CharacterPlan { Ranks = PlanDefaults.BuiltIn }, state));
    }
}
