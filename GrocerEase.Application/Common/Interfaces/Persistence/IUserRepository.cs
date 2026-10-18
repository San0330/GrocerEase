using GrocerEase.Domain.Entities;

namespace GrocerEase.Application.Common.Interfaces.Persistence;

public interface IUserRepository
{
    User? GetUserByEmail(string email);
    void Add(User user);
}
