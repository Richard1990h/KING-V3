// Project Repository
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.Data.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(string id, string userId);
    Task<IEnumerable<Project>> GetByUserIdAsync(string userId);
    Task<string> CreateAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(string id, string userId);
}

public class ProjectRepository : IProjectRepository
{
    private readonly IDbContext _db;

    public ProjectRepository(IDbContext db)
    {
        _db = db;
    }

    public async Task<Project?> GetByIdAsync(string id, string userId)
    {
        return await _db.QueryFirstOrDefaultAsync<Project>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId AND status != 'deleted'",
            new { Id = id, UserId = userId });
    }

    public async Task<IEnumerable<Project>> GetByUserIdAsync(string userId)
    {
        return await _db.QueryAsync<Project>(
            "SELECT * FROM projects WHERE user_id = @UserId AND status != 'deleted' ORDER BY updated_at DESC",
            new { UserId = userId });
    }

    public async Task<string> CreateAsync(Project project)
    {
        project.Id = Guid.NewGuid().ToString();
        await _db.ExecuteAsync(@"
            INSERT INTO projects (id, user_id, name, description, language, status, created_at, updated_at)
            VALUES (@Id, @UserId, @Name, @Description, @Language, @Status, NOW(), NOW())",
            project);
        return project.Id;
    }

    public async Task UpdateAsync(Project project)
    {
        await _db.ExecuteAsync(@"
            UPDATE projects SET name = @Name, description = @Description, updated_at = NOW()
            WHERE id = @Id", project);
    }

    public async Task DeleteAsync(string id, string userId)
    {
        await _db.ExecuteAsync(
            "UPDATE projects SET status = 'deleted' WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
    }
}
