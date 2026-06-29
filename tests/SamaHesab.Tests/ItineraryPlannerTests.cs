using System.Collections.Generic;
using System.Linq;
using SamaHesab.Modules.Tourism.Application.Itinerary;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>الگوریتمِ پیشنهادِ برنامهٔ اقامتی: اولویتِ سود، عدمِ تداخلِ سانس، تنوع.</summary>
public class ItineraryPlannerTests
{
    // C(product, session, day, start, end, sale, cost)
    private static PlanCandidate C(int prod, int sess, int day, int start, int end, decimal sale, decimal cost)
        => new(prod, $"P{prod}", sess, day, start, end, sale, cost);

    [Fact]
    public void Picks_Higher_Profit_When_Sessions_Conflict()
    {
        // دو سانسِ هم‌زمان در روز ۱؛ باید سودبیشتر انتخاب شود و دیگری (تداخل) کنار برود.
        var cands = new[]
        {
            C(1, 10, 1, 540, 660, 1000, 200),   // سود ۸۰۰
            C(2, 20, 1, 540, 660, 1000, 900),   // هم‌زمان، سود ۱۰۰
        };
        var plan = ItineraryPlanner.Plan(cands, new PlanOptions(Days: 1));
        var stop = Assert.Single(plan.Stops);
        Assert.Equal(1, stop.ProductId);        // سودبیشتر
        Assert.Equal(800, plan.TotalProfit);
    }

    [Fact]
    public void No_Time_Overlap_Within_A_Day()
    {
        // سه سانس: دوتا هم‌زمان، یکی جدا. باید دو موردِ بدونِ تداخل انتخاب شوند.
        var cands = new[]
        {
            C(1, 10, 1, 540, 660, 500, 0),    // ۹–۱۱
            C(2, 20, 1, 600, 720, 900, 0),    // ۱۰–۱۲ (با اولی تداخل)
            C(3, 30, 1, 780, 900, 400, 0),    // ۱۳–۱۵ (بدونِ تداخل)
        };
        var plan = ItineraryPlanner.Plan(cands, new PlanOptions(Days: 1));
        Assert.Equal(2, plan.Stops.Count);
        // هیچ دو قلمِ انتخاب‌شده‌ای نباید تداخلِ زمانی داشته باشند.
        var picks = plan.Stops.OrderBy(s => s.StartMinute).ToList();
        for (int i = 1; i < picks.Count; i++)
            Assert.True(picks[i].StartMinute >= picks[i - 1].EndMinute, "اقلامِ انتخاب‌شده نباید تداخلِ زمانی داشته باشند.");
    }

    [Fact]
    public void Prefers_Variety_Across_Days_Over_Repeating_Same_Product()
    {
        // محصول ۱ پرسودترین است و در هر دو روز سانس دارد؛ محصول ۲ کم‌سودتر ولی فقط روز ۲.
        // با اولویتِ تنوع، روز ۱ محصول ۱ و روز ۲ محصول ۲ (نه تکرارِ ۱) انتخاب می‌شود.
        var cands = new[]
        {
            C(1, 10, 1, 540, 660, 1000, 0),   // روز۱ محصول۱
            C(1, 11, 2, 540, 660, 1000, 0),   // روز۲ محصول۱ (تکرار)
            C(2, 20, 2, 540, 660, 600, 0),    // روز۲ محصول۲ (کم‌سودتر، ولی متنوع)
        };
        var plan = ItineraryPlanner.Plan(cands, new PlanOptions(Days: 2, PreferVariety: true));
        Assert.Equal(2, plan.Stops.Count);
        Assert.Equal(1, plan.Stops.Single(s => s.Day == 1).ProductId);
        Assert.Equal(2, plan.Stops.Single(s => s.Day == 2).ProductId);   // تنوع بر سود مقدم شد
    }

    [Fact]
    public void Without_Variety_Repeats_Highest_Profit_Product()
    {
        var cands = new[]
        {
            C(1, 10, 1, 540, 660, 1000, 0),
            C(1, 11, 2, 540, 660, 1000, 0),
            C(2, 20, 2, 540, 660, 600, 0),
        };
        var plan = ItineraryPlanner.Plan(cands, new PlanOptions(Days: 2, PreferVariety: false));
        Assert.All(plan.Stops, s => Assert.Equal(1, s.ProductId));   // بدونِ تنوع: همان پرسود تکرار می‌شود
        Assert.Equal(2000, plan.TotalSale);
    }

    [Fact]
    public void Respects_MaxPerDay()
    {
        var cands = new[]
        {
            C(1, 10, 1, 540, 600, 100, 0),
            C(2, 20, 1, 660, 720, 100, 0),
            C(3, 30, 1, 780, 840, 100, 0),
        };
        var plan = ItineraryPlanner.Plan(cands, new PlanOptions(Days: 1, MaxPerDay: 2));
        Assert.Equal(2, plan.Stops.Count);
    }
}
