using FluentValidation;

namespace GameServer.Application.Features.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("Account ID is required.")
            .MaximumLength(32).WithMessage("Account ID must be at most 32 characters.");

        RuleFor(x => x.PasswordHash)
            .NotEmpty().WithMessage("Password hash is required.");
    }
}
