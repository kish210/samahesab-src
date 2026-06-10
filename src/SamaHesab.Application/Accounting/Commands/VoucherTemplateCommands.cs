using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

// ── ذخیره‌ی الگوی سند ─────────────────────────────────────────────────────────
public record VoucherTemplateLineDto(int AccountId, decimal Debit, decimal Credit, string? Description);
public record SaveVoucherTemplateCommand(string Name, int VoucherTypeId, string? Description,
    List<VoucherTemplateLineDto> Lines) : IRequest<Result<int>>;

public class SaveVoucherTemplateCommandValidator : AbstractValidator<SaveVoucherTemplateCommand>
{
    public SaveVoucherTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام الگو الزامی است.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("الگو باید حداقل یک ردیف داشته باشد.");
    }
}

public class SaveVoucherTemplateCommandHandler : IRequestHandler<SaveVoucherTemplateCommand, Result<int>>
{
    private readonly IVoucherTemplateRepository _templates;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveVoucherTemplateCommandHandler(IVoucherTemplateRepository templates, IUnitOfWork uow, ICurrentUserService user)
    { _templates = templates; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveVoucherTemplateCommand req, CancellationToken ct)
    {
        try
        {
            var template = VoucherTemplate.Create(_user.CompanyId ?? 1, _user.BranchId ?? 1,
                req.Name, req.VoucherTypeId, req.Description);
            int row = 1;
            foreach (var l in req.Lines)
                template.AddLine(VoucherTemplateLine.Create(0, row++, l.AccountId, l.Debit, l.Credit, l.Description));

            await _templates.AddAsync(template, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(template.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── ساخت سند پیش‌نویس از روی الگو ────────────────────────────────────────────
public record CreateVoucherFromTemplateCommand(int TemplateId, string Date, string? Description = null)
    : IRequest<Result<int>>;

public class CreateVoucherFromTemplateCommandHandler : IRequestHandler<CreateVoucherFromTemplateCommand, Result<int>>
{
    private readonly IVoucherTemplateRepository _templates;
    private readonly IVoucherRepository _vouchers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CreateVoucherFromTemplateCommandHandler(IVoucherTemplateRepository templates,
        IVoucherRepository vouchers, IUnitOfWork uow, ICurrentUserService user)
    { _templates = templates; _vouchers = vouchers; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CreateVoucherFromTemplateCommand req, CancellationToken ct)
    {
        var tpl = await _templates.GetWithLinesAsync(req.TemplateId, ct);
        if (tpl is null) return Result<int>.Failure("الگو یافت نشد.");
        if (!tpl.Lines.Any()) return Result<int>.Failure("الگو فاقد ردیف است.");

        try
        {
            var companyId = _user.CompanyId ?? tpl.CompanyId;
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var voucher = Voucher.Create(companyId, tpl.BranchId, 1, number, req.Date,
                tpl.VoucherTypeId, req.Description ?? tpl.Name);

            int row = 1;
            foreach (var l in tpl.Lines.OrderBy(l => l.RowNumber))
                voucher.AddItem(VoucherItem.Create(0, row++, l.AccountId, l.Debit, l.Credit, l.Description));

            await _vouchers.AddAsync(voucher, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(voucher.Id);   // پیش‌نویس — کاربر مبلغ/تاریخ را تنظیم و ثبت می‌کند
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
