using Nexustock.Modules.TaskInterleaving.Services;

namespace Nexustock.TaskInterleaving.UnitTests;

public class TaskInterleavingScorerTests
{
    private static Guid LocA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static Guid LocB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static Guid Zone1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static Guid Zone2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Distance_SameLocation_Returns45()
    {
        var score = TaskInterleavingScorer.Score(new TaskInterleavingScorer.ScoreInput
        {
            CurrentLocationId = LocA,
            CandidateLocationId = LocA,
            CurrentZoneId = Zone1,
            CandidateZoneId = Zone1
        });
        Assert.Equal(45m, score.DistanceScore);
    }

    [Fact]
    public void Distance_SameZone_Returns35()
    {
        var score = TaskInterleavingScorer.Score(new TaskInterleavingScorer.ScoreInput
        {
            CurrentLocationId = LocA,
            CandidateLocationId = LocB,
            CurrentZoneId = Zone1,
            CandidateZoneId = Zone1
        });
        Assert.Equal(35m, score.DistanceScore);
    }

    [Fact]
    public void Distance_DifferentZone_Returns10()
    {
        var score = TaskInterleavingScorer.Score(new TaskInterleavingScorer.ScoreInput
        {
            CurrentLocationId = LocA,
            CandidateLocationId = LocB,
            CurrentZoneId = Zone1,
            CandidateZoneId = Zone2
        });
        Assert.Equal(10m, score.DistanceScore);
    }

    [Fact]
    public void Distance_MissingCoords_Returns20()
    {
        var score = TaskInterleavingScorer.Score(new TaskInterleavingScorer.ScoreInput());
        Assert.Equal(20m, score.DistanceScore);
    }

    [Fact]
    public void Age_30Minutes_Returns10()
    {
        Assert.Equal(10m, TaskInterleavingScorer.ComputeAgeScore(30 * 60));
    }

    [Fact]
    public void Age_90Minutes_CapsAt20()
    {
        Assert.Equal(20m, TaskInterleavingScorer.ComputeAgeScore(90 * 60));
    }

    [Theory]
    [InlineData("HIGH", 20)]
    [InlineData("MEDIUM", 10)]
    [InlineData("LOW", 5)]
    [InlineData("OTHER", 0)]
    public void Priority_MapsCorrectly(string step, int expected)
    {
        Assert.Equal(expected, TaskInterleavingScorer.ComputePriorityScore(step));
    }

    [Fact]
    public void Continuity_FullStack_Returns15()
    {
        var score = TaskInterleavingScorer.ComputeContinuityScore(new TaskInterleavingScorer.ScoreInput
        {
            SameOperation = true,
            HasActiveSession = true,
            SameZoneAsContext = true
        });
        Assert.Equal(15m, score);
    }

    [Fact]
    public void Penalty_StaleAndMissingLocation()
    {
        var score = TaskInterleavingScorer.ComputePenaltyScore(new TaskInterleavingScorer.ScoreInput
        {
            AgeSeconds = TaskInterleavingScorer.StaleAgeSeconds + 1,
            CandidateLocationId = null
        });
        Assert.Equal(25m, score);
    }

    [Fact]
    public void Penalty_ConflictRisk_Adds50()
    {
        var score = TaskInterleavingScorer.ComputePenaltyScore(new TaskInterleavingScorer.ScoreInput
        {
            IsConflictRisk = true
        });
        Assert.Equal(50m, score);
    }

    [Fact]
    public void TieBreak_EqualScore_PrefersHigherPriorityThenOlderThenSmallerTaskId()
    {
        var taskLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var taskHigh = Guid.Parse("00000000-0000-0000-0000-000000000002");

        // Same total, higher priority wins (A better than B when priorityA > priorityB)
        var cmp = TaskInterleavingScorer.CompareTieBreak(
            50, 20, 100, taskHigh,
            50, 10, 100, taskLow);
        Assert.True(cmp < 0); // A sorts before B

        // Same total+priority, older age wins
        cmp = TaskInterleavingScorer.CompareTieBreak(
            50, 10, 200, taskHigh,
            50, 10, 100, taskLow);
        Assert.True(cmp < 0);

        // Same all, smaller TaskId wins
        cmp = TaskInterleavingScorer.CompareTieBreak(
            50, 10, 100, taskLow,
            50, 10, 100, taskHigh);
        Assert.True(cmp < 0);
    }
}

public class TaskInterleavingValidationTests
{
    [Theory]
    [InlineData("TOO_FAR", true)]
    [InlineData("BLOCKED_LOCATION", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("UNKNOWN", false)]
    public void RejectReason_Allowlist(string? reason, bool expected)
    {
        Assert.Equal(expected, TaskInterleavingScorer.IsValidRejectReason(reason));
    }
}
