using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

public record CustomerAttachmentDto(int Id, string FileName, string StoredPath,
    string? ContentType, long FileSize, string UploadedAt, string? Description);

public record GetCustomerAttachmentsQuery(int CustomerId) : IRequest<List<CustomerAttachmentDto>>;

public class GetCustomerAttachmentsQueryHandler
    : IRequestHandler<GetCustomerAttachmentsQuery, List<CustomerAttachmentDto>>
{
    private readonly IRepository<CustomerAttachment> _repo;
    private readonly ICurrentUserService _user;

    public GetCustomerAttachmentsQueryHandler(IRepository<CustomerAttachment> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<List<CustomerAttachmentDto>> Handle(GetCustomerAttachmentsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _repo.FindAsync(a => a.CompanyId == companyId && a.CustomerId == req.CustomerId, ct);
        return list
            .OrderByDescending(a => a.Id)
            .Select(a => new CustomerAttachmentDto(a.Id, a.FileName, a.StoredPath,
                a.ContentType, a.FileSize, a.UploadedAt, a.Description))
            .ToList();
    }
}
