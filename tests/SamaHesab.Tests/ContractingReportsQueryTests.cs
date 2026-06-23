using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Contracting.Queries;
using SamaHesab.Domain.Entities.Contracting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>CON-C2-6 — کوئریِ گزارش‌های پیمانکاری: سه جدولِ آماده با ترکیبِ کوئری‌های داشبورد/ضمانت‌نامه.</summary>
public class ContractingReportsQueryTests
{
    /// <summary>IMediatorِ ساختگی که به سه کوئریِ موردِنیاز پاسخِ آماده می‌دهد.</summary>
    private sealed class StubMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object? r = request switch
            {
                GetContractProjectsQuery => new List<ContractProjectListDto>
                {
                    new(1, "P-100", "پروژهٔ الف", 9, "کارفرما الف", 1_000_000, 10, 10, 5, 0, 200_000, "فعال"),
                    new(2, "P-200", "پروژهٔ ب", 9, "کارفرما ب", 2_000_000, 10, 10, 5, 0, 0, "فعال"),
                },
                GetProjectDashboardQuery q => q.ContractProjectId == 1
                    ? new ProjectDashboardDto(1, "P-100", "پروژهٔ الف", 1_000_000, 600_000, 600_000, 500_000,
                        60_000, 30_000, 20_000, 100_000, 200_000, 60m, 80_000, 2)
                    : new ProjectDashboardDto(2, "P-200", "پروژهٔ ب", 2_000_000, 500_000, 500_000, 450_000,
                        50_000, 25_000, 15_000, 0, 0, 25m, 40_000, 1),
                GetGuaranteesQuery => new List<GuaranteeDto>
                {
                    new(11, 1, GuaranteeType.Performance, "ملت", 60_000, "1404/01/01", "1404/12/29",
                        GuaranteeStatus.Active, 20, true),
                },
                _ => null
            };
            return Task.FromResult((TResponse)r!);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    [Fact]
    public async Task Builds_Three_Tables_From_Dashboard_And_Guarantees()
    {
        var handler = new GetContractingReportsQueryHandler(new StubMediator());
        var dto = await handler.Handle(new GetContractingReportsQuery(), default);

        // ۱) خلاصهٔ مالی: دو پروژه + ردیفِ جمع. جمعِ مبلغِ پیمان = ۳٬۰۰۰٬۰۰۰.
        Assert.Equal(3, dto.FinancialSummary.Rows.Count);
        var sumRow = dto.FinancialSummary.Rows[^1];
        Assert.Equal("جمع", sumRow[1]);
        Assert.Equal("3,000,000", sumRow[2]);              // ستونِ مبلغِ پیمان (اندیس ۲)

        // ۲) سپرده‌های نگه‌داشته: دو پروژه + جمع. جمعِ سپرده = (۶۰+۳۰)+(۵۰+۲۵) هزار = ۱۶۵٬۰۰۰.
        Assert.Equal(3, dto.DepositsHeld.Rows.Count);
        var depTotal = dto.DepositsHeld.Rows[^1];
        Assert.Equal("جمع", depTotal[0]);
        Assert.Equal("165,000", depTotal[^1]);

        // ۳) دفترِ ضمانت‌نامه‌ها: یک ردیف، با عنوانِ پروژه و برچسبِ فارسیِ نوع/وضعیت.
        var gRow = Assert.Single(dto.Guarantees.Rows);
        Assert.Equal("پروژهٔ الف", gRow[0]);
        Assert.Equal("حسن‌انجام‌کار", gRow[1]);
        Assert.Equal("فعال", gRow[^1]);
    }
}
