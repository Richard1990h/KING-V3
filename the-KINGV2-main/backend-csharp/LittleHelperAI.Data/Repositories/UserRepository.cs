// User Repository
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.Data.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id);
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllAsync();
    Task<string> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(string id);
    Task UpdateCreditsAsync(string id, decimal delta);
}

public class UserRepository : IUserRepository
{
    private readonly IDbContext _db;

    public UserRepository(IDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id", new { Id = id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email", new { Email = email });
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _db.QueryAsync<User>("SELECT * FROM users ORDER BY created_at DESC");
    }

    public async Task<string> CreateAsync(User user)
    {
        user.Id = Guid.NewGuid().ToString();
        await _db.ExecuteAsync(@"
            INSERT INTO users (id, email, name, password_hash, role, credits, credits_enabled, plan, language, created_at)
            VALUES (@Id, @Email, @Name, @PasswordHash, @Role, @Credits, @CreditsEnabled, @Plan, @Language, NOW())",
            user);
        return user.Id;
    }

    public async Task UpdateAsync(User user)
    {
        await _db.ExecuteAsync(@"
            UPDATE users SET 
                name = @Name, role = @Role, credits = @Credits, 
                credits_enabled = @CreditsEnabled, plan = @Plan, language = @Language
            WHERE id = @Id", user);
    }

    public async Task DeleteAsync(string id)
    {
        await _db.ExecuteAsync("DELETE FROM users WHERE id = @Id", new { Id = id });
    }

    public async Task UpdateCreditsAsync(string id, decimal delta)
    {
        await _db.ExecuteAsync(
            "UPDATE users SET credits = credits + @Delta WHERE id = @Id",
            new { Id = id, Delta = delta });
    }
}
