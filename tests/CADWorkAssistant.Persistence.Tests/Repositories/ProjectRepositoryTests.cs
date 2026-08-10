using System;
using System.Linq;
using System.Threading.Tasks;
using CADWorkAssistant.Core.Models;
using CADWorkAssistant.Persistence.Repositories;

namespace CADWorkAssistant.Persistence.Tests.Repositories;

public sealed class ProjectRepositoryTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;
    private readonly IProjectRepository _repository = new SqliteProjectRepository();

    public ProjectRepositoryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InsertAsync_ThenFindByIdAsync_RoundTripsAllFields()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var now = DateTimeOffset.UtcNow;
        var project = new Project(
            id: Guid.NewGuid().ToString("N"),
            name: "OO학교 신축",
            createdAt: now,
            updatedAt: now,
            lastOpenedAt: now,
            client: "OO교육청",
            site: "서울시 강남구",
            description: "테스트 프로젝트");

        await _repository.InsertAsync(project, connection);
        var found = await _repository.FindByIdAsync(project.Id, connection);

        Assert.NotNull(found);
        Assert.Equal(project.Name, found!.Name);
        Assert.Equal(project.Client, found.Client);
        Assert.Equal(project.Site, found.Site);
        Assert.Equal(project.Description, found.Description);
    }

    [Fact]
    public async Task FindByIdAsync_UnknownId_ReturnsNull()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var found = await _repository.FindByIdAsync("does-not-exist", connection);

        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields_ButNotIdOrCreatedAt()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);
        var project = new Project(Guid.NewGuid().ToString("N"), "원래 이름", createdAt, createdAt, createdAt);
        await _repository.InsertAsync(project, connection);

        project.Name = "바뀐 이름";
        project.Client = "새 발주처";
        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(project, connection);

        var found = await _repository.FindByIdAsync(project.Id, connection);
        Assert.NotNull(found);
        Assert.Equal("바뀐 이름", found!.Name);
        Assert.Equal("새 발주처", found.Client);
        Assert.Equal(project.Id, found.Id);
        Assert.Equal(createdAt, found.CreatedAt);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersByLastOpenedAtDescending()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var baseline = DateTimeOffset.UtcNow;
        var oldest = new Project(Guid.NewGuid().ToString("N"), "가장 오래됨", baseline, baseline, baseline.AddHours(-2));
        var newest = new Project(Guid.NewGuid().ToString("N"), "가장 최근", baseline, baseline, baseline);
        var middle = new Project(Guid.NewGuid().ToString("N"), "중간", baseline, baseline, baseline.AddHours(-1));

        await _repository.InsertAsync(oldest, connection);
        await _repository.InsertAsync(newest, connection);
        await _repository.InsertAsync(middle, connection);

        var recent = await _repository.GetRecentAsync(10, connection);

        var ids = recent.Select(p => p.Id).ToArray();
        var expectedOrder = new[] { newest.Id, middle.Id, oldest.Id };
        Assert.Equal(expectedOrder, ids.Where(id => expectedOrder.Contains(id)).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEveryProject_OrderedByNameCaseInsensitive()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        // 고유 접두어로 이 테스트가 만든 프로젝트만 골라낸다 - TestDatabaseFixture는 같은 테스트
        // 클래스의 모든 테스트가 파일을 공유하므로(GetRecentAsync 테스트와 같은 이유), GetAllAsync는
        // 다른 테스트가 넣은 프로젝트도 함께 반환한다.
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var baseline = DateTimeOffset.UtcNow;
        var b = new Project(Guid.NewGuid().ToString("N"), $"{prefix} banana 프로젝트", baseline, baseline, baseline);
        var a = new Project(Guid.NewGuid().ToString("N"), $"{prefix} Apple 프로젝트", baseline, baseline, baseline);
        var c = new Project(Guid.NewGuid().ToString("N"), $"{prefix} cherry 프로젝트", baseline, baseline, baseline);

        await _repository.InsertAsync(b, connection);
        await _repository.InsertAsync(a, connection);
        await _repository.InsertAsync(c, connection);

        var all = await _repository.GetAllAsync(connection);

        var ids = all.Where(p => p.Name.StartsWith(prefix, StringComparison.Ordinal)).Select(p => p.Id).ToArray();
        Assert.Equal(new[] { a.Id, b.Id, c.Id }, ids);
    }

    [Fact]
    public async Task TouchLastOpenedAsync_UpdatesOnlyLastOpenedAt()
    {
        var database = _fixture.CreateDatabase();
        using var connection = database.OpenConnection();

        var original = DateTimeOffset.UtcNow.AddDays(-1);
        var project = new Project(Guid.NewGuid().ToString("N"), "이름 유지됨", original, original, original);
        await _repository.InsertAsync(project, connection);

        var newOpenedAt = DateTimeOffset.UtcNow;
        await _repository.TouchLastOpenedAsync(project.Id, newOpenedAt, connection);

        var found = await _repository.FindByIdAsync(project.Id, connection);
        Assert.NotNull(found);
        Assert.Equal("이름 유지됨", found!.Name);
        Assert.True(Math.Abs((found.LastOpenedAt - newOpenedAt).TotalSeconds) < 1);
    }
}
