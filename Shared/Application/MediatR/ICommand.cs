using MediatR;
using Shared.Application.Result;

namespace Shared.Application.MediatR
{
    public interface ICommand : IRequest<Unit>
    {
    }

    public interface ICommand<out TResponse> : IRequest<TResponse>
        where TResponse : IResult
    {
    }

    public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
        where TCommand : ICommand
    {
    }

    public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
        where TResponse : IResult
    {
    }

    public interface IQuery<out TResponse> : IRequest<TResponse>
    {
    }

    public interface IEvent : INotification
    {
    }
}
