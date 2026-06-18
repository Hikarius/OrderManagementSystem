using Shared.Infrastructure.Data;

namespace NotificationService.Domain.Entities
{
    public class Notification : IAggregateRoot
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
