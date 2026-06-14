using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Commands;

// ── افزودنِ پیوست (فایل از قبل توسط لایهٔ WPF در StoredPath کپی شده) ──
public record AddCustomerAttachmentCommand(int CustomerId, string FileName, string StoredPath,
    string? ContentType, long FileSize, string UploadedAt, string? Description = null) : IRequest<Result<int>>;

public class AddCustomerAttachmentCommandHandler : IRequestHandler<AddCustomerAttachmentCommand, Result<int>>
{
    private readonly IRepository<CustomerAttachment> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public AddCustomerAttachmentCommandHandler(IRepository<CustomerAttachment> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(AddCustomerAttachmentCommand req, CancellationToken ct)
    {
        if (req.CustomerId <= 0) return Result<int>.Failure("مشتری نامعتبر است.");
        var att = CustomerAttachment.Create(_user.CompanyId ?? 1, req.CustomerId, req.FileName,
            req.StoredPath, req.ContentType, req.FileSize, req.UploadedAt, req.Description);
        await _repo.AddAsync(att, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(att.Id);
    }
}

// ── حذفِ پیوست (فایلِ فیزیکی را لایهٔ WPF پاک می‌کند) ──
public record DeleteCustomerAttachmentCommand(int Id) : IRequest<Result<string?>>;

public class DeleteCustomerAttachmentCommandHandler : IRequestHandler<DeleteCustomerAttachmentCommand, Result<string?>>
{
    private readonly IRepository<CustomerAttachment> _repo;
    private readonly IUnitOfWork _uow;

    public DeleteCustomerAttachmentCommandHandler(IRepository<CustomerAttachment> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result<string?>> Handle(DeleteCustomerAttachmentCommand req, CancellationToken ct)
    {
        var att = await _repo.GetByIdAsync(req.Id, ct);
        if (att is null) return Result<string?>.Failure("پیوست یافت نشد.");
        var path = att.StoredPath;
        _repo.Remove(att);
        await _uow.SaveChangesAsync(ct);
        return Result<string?>.Success(path);   // مسیر برگردانده می‌شود تا WPF فایل را پاک کند
    }
}
