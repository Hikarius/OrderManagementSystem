using MediatR;

namespace Shared.Application.MediatR
{
    public interface ICommand : IRequest<Unit>
    {
    }

    public interface ICommand<out TResponse> : IRequest<TResponse>
    {
    }

    public interface IQuery<out TResponse> : IRequest<TResponse>
    {
    }

    public interface IEvent : INotification
    {
    }
}
