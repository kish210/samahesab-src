using System.Reflection;
using FluentValidation;
using MediatR;

namespace SamaHesab.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count > 0)
        {
            // U-SEC-2: پیش‌تر این‌جا throw می‌شد؛ چون BaseViewModel.ExecuteAsync هیچ catchای ندارد
            // (فقط finally) و [RelayCommand]ِ CommunityToolkit.Mvvm استثنایِ فرمانِ async را در
            // Task.Exceptionِرصدنشده دفن می‌کند، خطاهایِ اعتبارسنجیِ سمتِ سرور (که سمتِ کلاینت پوشش
            // ندارند، مثلِ درصدِ سهمِ منفیِ سهامدار) کاملاً بی‌صدا شکست می‌خوردند — نه پیام خطا، نه
            // موفقیت، هیچ اثری برایِ کاربر. حالا (هم‌راستا با AuditBehavior.Deny) اگر TResponse از نوعِ
            // Result/Result<T> باشد یک Failure برمی‌گردد تا UI پیامِ اعتبارسنجی را عادی نشان دهد.
            var messages = failures.Select(f => f.ErrorMessage).ToArray();
            var failureMethod = typeof(TResponse).GetMethod("Failure",
                BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string[]) }, modifiers: null);
            if (failureMethod is not null)
                return (TResponse)failureMethod.Invoke(null, new object[] { messages })!;
            throw new ValidationException(failures);
        }

        return await next();
    }
}
