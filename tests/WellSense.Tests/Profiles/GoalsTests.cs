using FluentAssertions;
using WellSense.Application.Profiles.Goals.AddGoal;
using WellSense.Application.Profiles.Goals.DeleteGoal;
using WellSense.Application.Profiles.Goals.ListMyGoals;
using WellSense.Domain.Profiles;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Profiles;

public class GoalsTests
{
    [Fact]
    public async Task AddGoal_creates_profile_lazily_when_missing()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userId = Guid.NewGuid();

        var handler = new AddGoalCommandHandler(db, clock);
        var goalId = await handler.Handle(new AddGoalCommand(userId, "steps", 10000), default);

        db.Profiles.Should().ContainSingle(p => p.UserId == userId);
        db.Goals.Should().ContainSingle(g => g.Id == goalId && g.Type == "steps" && g.TargetValue == 10000);
    }

    [Fact]
    public async Task ListMyGoals_only_returns_goals_of_the_current_user()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var handler = new AddGoalCommandHandler(db, clock);
        await handler.Handle(new AddGoalCommand(userA, "steps", 8000), default);
        await handler.Handle(new AddGoalCommand(userB, "sleep_hours", 8), default);

        var listHandler = new ListMyGoalsQueryHandler(db);
        var goalsOfA = await listHandler.Handle(new ListMyGoalsQuery(userA), default);

        goalsOfA.Should().ContainSingle(g => g.Type == "steps");
    }

    [Fact]
    public async Task DeleteGoal_of_another_user_throws_not_found()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();

        var addHandler = new AddGoalCommandHandler(db, clock);
        var goalId = await addHandler.Handle(new AddGoalCommand(owner, "steps", 8000), default);

        var deleteHandler = new DeleteGoalCommandHandler(db);
        var act = () => deleteHandler.Handle(new DeleteGoalCommand(attacker, goalId), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        db.Goals.Should().ContainSingle(g => g.Id == goalId); // sigue existiendo, no se borró
    }

    [Fact]
    public async Task DeleteGoal_of_own_goal_succeeds()
    {
        using var db = InMemoryDbContextFactory.Create();
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var owner = Guid.NewGuid();

        var addHandler = new AddGoalCommandHandler(db, clock);
        var goalId = await addHandler.Handle(new AddGoalCommand(owner, "steps", 8000), default);

        var deleteHandler = new DeleteGoalCommandHandler(db);
        await deleteHandler.Handle(new DeleteGoalCommand(owner, goalId), default);

        db.Goals.Should().BeEmpty();
    }
}
