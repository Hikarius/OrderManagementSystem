using NotificationService.ReadModel.Dtos;
using NotificationService.ReadModel.Queries;
using NotificationService.ReadModel.Repositories;

namespace NotificationService.ReadModel.Handlers
{
    public class GetNotificationsByUserHandler
    {
        private readonly InMemoryNotificationReadRepository _repo;

        public GetNotificationsByUserHandler(InMemoryNotificationReadRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<NotificationDto>> Handle(GetNotificationsByUserQuery query)
        {
            return await _repo.GetByUserAsync(query.UserId);
        }
    }
}
