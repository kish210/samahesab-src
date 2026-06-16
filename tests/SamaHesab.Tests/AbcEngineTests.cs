using SamaHesab.Application.Reports;
using System.Linq;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ (پولیش) — موتورِ تحلیلِ ABC.</summary>
public class AbcEngineTests
{
    [Fact]
    public void Classifies_by_cumulative_share()
    {
        var input = new[]
        {
            new AbcInput(1, 50), new AbcInput(2, 30), new AbcInput(3, 15), new AbcInput(4, 5),
        };
        var r = AbcEngine.Classify(input).ToDictionary(x => x.Id, x => x.Class);
        // cum: 50→A، 80→A، 95→B، 100→C
        Assert.Equal('A', r[1]);
        Assert.Equal('A', r[2]);
        Assert.Equal('B', r[3]);
        Assert.Equal('C', r[4]);
    }

    [Fact]
    public void Sorted_descending_with_cumulative_reaching_100()
    {
        var r = AbcEngine.Classify(new[] { new AbcInput(1, 10), new AbcInput(2, 90) });
        Assert.Equal(2, r[0].Id);                       // بزرگ‌ترین اول
        Assert.Equal(100m, r[^1].CumulativePercent);    // تجمعیِ آخر = ۱۰۰٪
    }

    [Fact]
    public void Skips_zero_and_negative_values()
    {
        var r = AbcEngine.Classify(new[] { new AbcInput(1, 100), new AbcInput(2, 0), new AbcInput(3, -5) });
        Assert.Single(r);
        Assert.Equal(1, r[0].Id);
    }

    [Fact]
    public void Empty_input_yields_empty()
        => Assert.Empty(AbcEngine.Classify(System.Array.Empty<AbcInput>()));
}
