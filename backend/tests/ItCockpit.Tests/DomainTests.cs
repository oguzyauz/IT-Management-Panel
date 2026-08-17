using FluentAssertions;
using ItCockpit.Domain;
using Xunit;

namespace ItCockpit.Tests;

public sealed class TicketNumberTests
{
    [Theory]
    [InlineData("I260729_000144", TicketType.Incident)]
    [InlineData("S260801_000210", TicketType.ServiceRequest)]
    [InlineData("i260729_000144", TicketType.Incident)]
    [InlineData("  S260801_000210  ", TicketType.ServiceRequest)]
    public void Accepts_canonical_numbers(string raw, TicketType expected)
    {
        TicketNumber.TryParse(raw, out var number).Should().BeTrue();
        number.Type.Should().Be(expected);
        number.Value.Should().MatchRegex(@"^[IS]\d{6}_\d{6}$");
    }

    [Theory]
    [InlineData("C260729_000144")]   // desteklenmeyen önek
    [InlineData("IS260729_000144")]  // iki harf
    [InlineData("I26729_000144")]    // 5 hane
    [InlineData("I260729-000144")]   // yanlış ayırıcı
    [InlineData("I260729_00014")]    // 5 hane sayaç
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_non_canonical_numbers(string? raw)
    {
        TicketNumber.TryParse(raw, out _).Should().BeFalse();
    }
}

public sealed class PersonNameNormalizerTests
{
    [Theory]
    [InlineData("Turcan, Merve", "Merve Turcan")]
    [InlineData("Demir, Ali Can", "Ali Can Demir")]
    [InlineData("TURCAN, MERVE", "Merve Turcan")]
    [InlineData("  Turcan ,  Merve  ", "Merve Turcan")]
    [InlineData("Merve Turcan", "Merve Turcan")]
    public void Swaps_surname_first_format(string raw, string expected)
    {
        PersonNameNormalizer.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void Uses_turkish_casing_rules()
    {
        // Türkçe kültürde 'I' -> 'ı', 'i' -> 'İ'. Invariant kültür kullanılsaydı "Isik" çıkardı.
        PersonNameNormalizer.Normalize("IŞIK, İLKNUR").Should().Be("İlknur Işık");
    }

    [Fact]
    public void Leaves_multi_comma_values_untouched_in_order()
    {
        PersonNameNormalizer.Normalize("Turcan, Merve, Dr.").Should().Be("Turcan, Merve, Dr.");
    }

    [Fact]
    public void Handles_empty_input()
    {
        PersonNameNormalizer.Normalize(null).Should().BeEmpty();
        PersonNameNormalizer.Normalize("   ").Should().BeEmpty();
    }
}

public sealed class TicketStatusTransitionTests
{
    [Theory]
    [InlineData(TicketStatus.Unassigned, TicketStatus.Assigned)]
    [InlineData(TicketStatus.Assigned, TicketStatus.InProgress)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Completed)]
    [InlineData(TicketStatus.Completed, TicketStatus.Archived)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Unassigned)]
    public void Allows_expected_transitions(TicketStatus from, TicketStatus to)
    {
        TicketStatusTransitions.IsAllowed(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(TicketStatus.Unassigned, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Unassigned, TicketStatus.Completed)]
    [InlineData(TicketStatus.Archived, TicketStatus.Unassigned)]
    [InlineData(TicketStatus.Completed, TicketStatus.Unassigned)]
    public void Blocks_unexpected_transitions(TicketStatus from, TicketStatus to)
    {
        TicketStatusTransitions.IsAllowed(from, to).Should().BeFalse();
    }

    [Theory]
    [InlineData(TicketStatus.Unassigned, true)]
    [InlineData(TicketStatus.Assigned, true)]
    [InlineData(TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.Completed, false)]
    [InlineData(TicketStatus.Archived, false)]
    public void Open_definition_excludes_completed_and_archived(TicketStatus status, bool expected)
    {
        TicketStatusTransitions.IsOpen(status).Should().Be(expected);
    }
}
