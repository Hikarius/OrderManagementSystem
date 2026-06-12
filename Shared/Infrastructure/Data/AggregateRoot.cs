namespace Shared.Infrastructure.Data
{

    public interface IAggregateRoot
    {
    }

    public class AggregateRoot : IAggregateRoot
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
