using MediatR;
using SamaHesab.Domain.Common;

namespace SamaHesab.Application.Common.Events;

/// <summary>
/// پوشش‌دهنده‌ی MediatR برای رویدادهای دامنه. لایه‌ی Domain به MediatR وابسته نیست؛
/// این Wrapper اجازه می‌دهد رویدادهای دامنه از طریق IPublisher منتشر شوند و
/// هندلرها به‌صورت INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt; ثبت شوند.
/// </summary>
public sealed class DomainEventNotification<TDomainEvent> : INotification
    where TDomainEvent : DomainEvent
{
    public TDomainEvent Event { get; }
    public DomainEventNotification(TDomainEvent domainEvent) => Event = domainEvent;
}
