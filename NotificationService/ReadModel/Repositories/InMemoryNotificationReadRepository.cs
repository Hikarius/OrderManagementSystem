using System.Collections.Concurrent;
using NotificationService.ReadModel.Dtos;

namespace NotificationService.ReadModel.Repositories
{
    public class InMemoryNotificationReadRepository
    {
        private readonly ConcurrentDictionary<Guid, NotificationDto> _store = new();

        public InMemoryNotificationReadRepository()
        {
            var id = Guid.NewGuid();
            _store[id] = new NotificationDto { Id = id, Message = "Welcome", CreatedAt = DateTime.UtcNow };
        }

        public Task<IEnumerable<NotificationDto>> GetByUserAsync(Guid userId)
        {
            var results = _store.Values.Where(n => n.UserId == userId);
            return Task.FromResult(results);
        }
    }
}
