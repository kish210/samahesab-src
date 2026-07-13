using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Commands;

/// <summary>
/// POS-CUSTOMER — پیش‌تر وقتی صندوق/رستوران مشتری انتخاب نمی‌شد، کدها بی‌صدا CustomerId=۱ می‌فرستادند
/// (نه یک مشتریِ «متفرقه»ی واقعی) — یعنی هر فروشِ بی‌نام به هر طرف‌حسابی که تصادفاً Id=۱ داشت متصل
/// می‌شد. برایِ فروشِ نسیه/ترکیبیِ بی‌نام این حتی بدهیِ واقعی به آن مشتریِ ناشناخته اضافه و سقفِ
/// اعتبارش چک می‌کرد (U-PARTY-BAL این را واقعاً مؤثر کرد). این کوئری یک طرف‌حسابِ اختصاصیِ «متفرقه»
/// (کدِ ثابتِ WALKIN، به‌ازایِ هر شرکت) پیدا یا (بارِ اول) می‌سازد تا فروشِ بی‌نام همیشه به یک مقصدِ
/// امن و مشخص برود، نه یک مشتریِ واقعیِ ناشناخته.
/// </summary>
public record GetOrCreateWalkInCustomerCommand : IRequest<int>;

public class GetOrCreateWalkInCustomerCommandHandler : IRequestHandler<GetOrCreateWalkInCustomerCommand, int>
{
    public const string WalkInCode = "WALKIN";
    private readonly IRepository<Party> _parties;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public GetOrCreateWalkInCustomerCommandHandler(IRepository<Party> parties, IUnitOfWork uow, ICurrentUserService currentUser)
    { _parties = parties; _uow = uow; _currentUser = currentUser; }

    public async Task<int> Handle(GetOrCreateWalkInCustomerCommand req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var existing = await _parties.FindSingleAsync(p => p.CompanyId == companyId && p.Code == WalkInCode, ct);
        if (existing != null) return existing.Id;

        var party = Party.Create(companyId, WalkInCode, "حقیقی", firstName: "مشتری", lastName: "متفرقه (نقدی)", isCustomer: true);
        await _parties.AddAsync(party, ct);
        await _uow.SaveChangesAsync(ct);
        return party.Id;
    }
}
