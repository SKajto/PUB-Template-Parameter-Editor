using PubTemplateEditor.Core.Editing;
using PubTemplateEditor.Core.Format;
using PubTemplateEditor.Core.Model;

namespace PubTemplateEditor.Core.Tests;

/// <summary>
/// S3 fixtures: parsed templates held only in memory. No file is involved, so a bug in the
/// staging layer cannot touch a template — that property is itself part of the requirement.
/// </summary>
internal static class EditingFixtures
{
    internal static string Template(params string[] variables) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<template><metadata><property name=\"version\" value=\"2\"></property></metadata>" +
        "<dataset><variables>" +
        string.Concat(variables) +
        "</variables></dataset></template>";

    /// <summary>One declaration; <paramref name="defaultValue"/> null omits the default property entirely.</summary>
    internal static string Variable(
        string name,
        string? defaultValue,
        string access = "internal",
        string? description = "some description",
        string? type = "user")
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<variable><feature tag=\"definition\">");
        sb.Append($"<property name=\"name\" value=\"{name}\"></property>");
        if (description is not null)
        {
            sb.Append($"<property name=\"description\"><expression type=\"constant\">{description}</expression></property>");
        }

        if (type is not null)
        {
            sb.Append($"<property name=\"type\" value=\"{type}\"></property>");
        }

        sb.Append($"<property name=\"access\" value=\"{access}\"></property>");
        if (defaultValue is not null)
        {
            sb.Append($"<property name=\"default\" value=\"{defaultValue}\"></property>");
        }

        sb.Append("</feature></variable>");
        return sb.ToString();
    }

    internal static TemplateRecord Record(string path, string xml, TemplateReadState state = TemplateReadState.Loaded)
    {
        if (state != TemplateReadState.Loaded)
        {
            return new TemplateRecord
            {
                FilePath = path,
                ReadState = state,
                ReadError = state == TemplateReadState.Error ? "container entry could not be decoded" : null,
            };
        }

        ParseResult parsed = XmlDeclarationParser.Parse(Fixtures.Source(xml));
        return new TemplateRecord
        {
            FilePath = path,
            Format = TemplateFormat.PlainXmlTemplate,
            ReadState = TemplateReadState.Loaded,
            Declarations = parsed.Declarations,
            Warnings = parsed.Warnings,
            BaselineFingerprint = "BASELINE-" + Path.GetFileName(path),
        };
    }

    internal static VariableDeclaration Decl(TemplateRecord record, string name) =>
        record.Declarations.First(d => d.ExactName == name);
}

public class DraftStoreTests
{
    private const string FileA = @"C:\templates\a.dta";
    private const string FileB = @"C:\templates\b.dta";

    private static TemplateRecord AlphaTemplate() => EditingFixtures.Record(
        FileA,
        EditingFixtures.Template(
            EditingFixtures.Variable("alpha", "HELLO", description: "First parameter"),
            EditingFixtures.Variable("beta", ""),                       // declared but empty
            EditingFixtures.Variable("no_default", null),                // missing property
            EditingFixtures.Variable("multiline_default", "one\ntwo"))); // unobserved shape

    private static TemplateRecord OtherTemplate() => EditingFixtures.Record(
        FileB,
        EditingFixtures.Template(EditingFixtures.Variable("alpha", "OTHER", access: "external")));

    [Fact]
    public void StagingHoldsTheProposalInMemoryWithoutTouchingTheBaseline()
    {
        TemplateRecord record = AlphaTemplate();
        VariableDeclaration alpha = EditingFixtures.Decl(record, "alpha");
        var drafts = new DraftStore();

        StageResult result = drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "WORLD");

        Assert.Equal(StageStatus.Staged, result.Status);
        Assert.Equal(1, drafts.Count);
        Assert.Equal("WORLD", drafts.EffectiveValue(FileA, alpha, FieldKind.DefaultValue));

        // The parsed declaration is the read model: it keeps the stored value.
        Assert.Equal("HELLO", alpha.DefaultValue.LogicalValue);
        Assert.Equal("HELLO", drafts.Find(FileA, alpha.DeclarationId, FieldKind.DefaultValue)!.BaselineValue);
        Assert.Equal("alpha", drafts.Find(FileA, alpha.DeclarationId, FieldKind.DefaultValue)!.DeclarationName);
    }

    [Fact]
    public void ProposingTheStoredValueAgainRemovesTheDraftInsteadOfKeepingAnEmptyChange()
    {
        var drafts = new DraftStore();
        VariableDeclaration alpha = EditingFixtures.Decl(AlphaTemplate(), "alpha");

        Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "WORLD").Status);
        Assert.Equal(1, drafts.Count);

        StageResult again = drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "HELLO");

        Assert.Equal(StageStatus.ClearedAsNoOp, again.Status);
        Assert.Equal(0, drafts.Count);
        Assert.Null(drafts.Find(FileA, alpha.DeclarationId, FieldKind.DefaultValue));
        Assert.Equal("HELLO", drafts.EffectiveValue(FileA, alpha, FieldKind.DefaultValue));
    }

    [Fact]
    public void WhitespaceAndLetterCaseArePartOfTheValueComparison()
    {
        var drafts = new DraftStore();
        VariableDeclaration alpha = EditingFixtures.Decl(AlphaTemplate(), "alpha"); // stored default: HELLO

        // No trimming, no case folding: these are all different default values.
        Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, alpha, FieldKind.DefaultValue, " hello ").Status);
        Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "hello").Status);
        Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "HELLO ").Status);
        Assert.Equal(1, drafts.Count);

        // Only the exact stored value counts as "no change".
        Assert.Equal(StageStatus.ClearedAsNoOp, drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "HELLO").Status);
        Assert.Equal(0, drafts.Count);
    }

    [Fact]
    public void TheSameProposalStagedRepeatedlyCountsOnce()
    {
        var drafts = new DraftStore();
        VariableDeclaration alpha = EditingFixtures.Decl(AlphaTemplate(), "alpha");

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, alpha, FieldKind.DefaultValue, "WORLD").Status);
        }

        Assert.Equal(1, drafts.Count);
        Assert.Equal(1, drafts.CountForDeclaration(FileA, alpha.DeclarationId));
    }

    [Fact]
    public void NamesCannotBeEditedAndSayWhy()
    {
        var drafts = new DraftStore();
        VariableDeclaration alpha = EditingFixtures.Decl(AlphaTemplate(), "alpha");

        StageResult result = drafts.Stage(FileA, alpha, FieldKind.Name, "renamed");

        Assert.Equal(StageStatus.Rejected, result.Status);
        Assert.Contains("read-only", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(drafts.HasAny);
    }

    [Fact]
    public void AccessAcceptsOnlyTheObservedVocabulary()
    {
        var drafts = new DraftStore();
        VariableDeclaration alpha = EditingFixtures.Decl(AlphaTemplate(), "alpha");

        Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, alpha, FieldKind.Access, "external").Status);

        foreach (string invalid in new[] { "Internal", "EXTERNAL", " external", "internal ", "", "public" })
        {
            StageResult rejected = drafts.Stage(FileA, alpha, FieldKind.Access, invalid);
            Assert.Equal(StageStatus.Rejected, rejected.Status);
            Assert.Contains("internal / external", rejected.Reason, StringComparison.Ordinal);
        }

        Assert.Equal(StageStatus.Rejected, drafts.Stage(FileA, alpha, FieldKind.Access, null).Status);

        // The last valid value stands; invalid attempts left nothing behind.
        Assert.Equal("external", drafts.EffectiveValue(FileA, alpha, FieldKind.Access));
        Assert.Equal(1, drafts.Count);
    }

    [Fact]
    public void MissingFieldsAndUnobservedShapesAreRefusedWithAnExplanation()
    {
        TemplateRecord record = AlphaTemplate();
        var drafts = new DraftStore();

        VariableDeclaration noDefault = EditingFixtures.Decl(record, "no_default");
        StageResult missing = drafts.Stage(FileA, noDefault, FieldKind.DefaultValue, "x");
        Assert.Equal(StageStatus.Rejected, missing.Status);
        Assert.False(string.IsNullOrWhiteSpace(missing.Reason));

        VariableDeclaration multiline = EditingFixtures.Decl(record, "multiline_default");
        Assert.Equal(FieldCapability.ReadOnlyUnsupported, multiline.DefaultValue.Capability);
        StageResult unsupported = drafts.Stage(FileA, multiline, FieldKind.DefaultValue, "x");
        Assert.Equal(StageStatus.Rejected, unsupported.Status);
        Assert.Contains("newline", unsupported.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(drafts.HasAny);
    }

    [Fact]
    public void EmptyAndMissingValuesStayDistinctWhileStaging()
    {
        TemplateRecord record = AlphaTemplate();
        var drafts = new DraftStore();
        VariableDeclaration beta = EditingFixtures.Decl(record, "beta");

        Assert.True(beta.DefaultValue.Present);
        Assert.Equal(string.Empty, beta.DefaultValue.LogicalValue);

        Assert.Equal(StageStatus.Staged, drafts.Stage(FileA, beta, FieldKind.DefaultValue, "filled").Status);
        Assert.Equal("filled", drafts.EffectiveValue(FileA, beta, FieldKind.DefaultValue));

        // Back to empty is a real value, not "no value": the draft is removed and "" remains.
        Assert.Equal(StageStatus.ClearedAsNoOp, drafts.Stage(FileA, beta, FieldKind.DefaultValue, string.Empty).Status);
        Assert.Equal(string.Empty, drafts.EffectiveValue(FileA, beta, FieldKind.DefaultValue));

        VariableDeclaration noDefault = EditingFixtures.Decl(record, "no_default");
        Assert.Null(drafts.EffectiveValue(FileA, noDefault, FieldKind.DefaultValue));
    }

    [Fact]
    public void DescriptionsAndAccessAreIndependentDraftsOfTheSameDeclaration()
    {
        TemplateRecord record = AlphaTemplate();
        VariableDeclaration alpha = EditingFixtures.Decl(record, "alpha");
        var drafts = new DraftStore();

        drafts.StageDeclaration(FileA, alpha, new Dictionary<FieldKind, string?>
        {
            [FieldKind.Access] = "external",
            [FieldKind.DefaultValue] = "WORLD",
            [FieldKind.Description] = "First parameter", // unchanged
        });

        Assert.Equal(2, drafts.Count);
        Assert.Equal(2, drafts.DirtyFieldCount(FileA, alpha));
        Assert.False(drafts.HasChange(FileA, alpha.DeclarationId, FieldKind.Description));
        Assert.Equal("external", drafts.EffectiveValue(FileA, alpha, FieldKind.Access));
        Assert.Equal("First parameter", drafts.EffectiveValue(FileA, alpha, FieldKind.Description));
    }

    [Fact]
    public void DiscardRemovesOnlyTheRequestedScope()
    {
        TemplateRecord a = AlphaTemplate();
        TemplateRecord b = OtherTemplate();
        var drafts = new DraftStore();

        drafts.Stage(FileA, EditingFixtures.Decl(a, "alpha"), FieldKind.DefaultValue, "1");
        drafts.Stage(FileA, EditingFixtures.Decl(a, "beta"), FieldKind.DefaultValue, "2");
        drafts.Stage(FileB, EditingFixtures.Decl(b, "alpha"), FieldKind.DefaultValue, "3");

        Assert.Equal(new Dictionary<string, int> { [FileA] = 2, [FileB] = 1 }, drafts.CountByFile());

        Assert.Equal(1, drafts.DiscardForDeclaration(FileA, EditingFixtures.Decl(a, "alpha").DeclarationId));
        Assert.Equal(2, drafts.Count);

        Assert.Equal(1, drafts.DiscardForFile(FileA));
        Assert.Equal(1, drafts.Count);
        Assert.Equal(1, drafts.CountForFile(FileB));

        drafts.Clear();
        Assert.False(drafts.HasAny);
    }

    [Fact]
    public void RemoveDropsExactlyOneStagedChange()
    {
        TemplateRecord a = AlphaTemplate();
        var drafts = new DraftStore();

        drafts.Stage(FileA, EditingFixtures.Decl(a, "alpha"), FieldKind.DefaultValue, "keep");
        drafts.Stage(FileA, EditingFixtures.Decl(a, "alpha"), FieldKind.Description, "drop");
        drafts.Stage(FileA, EditingFixtures.Decl(a, "beta"), FieldKind.DefaultValue, "other");
        Assert.Equal(3, drafts.Count);

        var descriptionKey = new ChangeKey(FileA, EditingFixtures.Decl(a, "alpha").DeclarationId, FieldKind.Description);
        Assert.True(drafts.Remove(descriptionKey));
        Assert.Equal(2, drafts.Count);
        Assert.Null(drafts.Find(FileA, EditingFixtures.Decl(a, "alpha").DeclarationId, FieldKind.Description));
        Assert.NotNull(drafts.Find(FileA, EditingFixtures.Decl(a, "alpha").DeclarationId, FieldKind.DefaultValue));

        // The save step removes what it has already written. Removing twice must report false, never
        // throw, so a partially applied batch cannot abort on bookkeeping.
        Assert.False(drafts.Remove(descriptionKey));
        Assert.Equal(2, drafts.Count);
    }
}

public class ParameterComparisonTests
{
    private const string FileA = @"C:\templates\a.dta";
    private const string FileB = @"C:\templates\b.dta";
    private const string FileC = @"C:\templates\c.dta";
    private const string FileD = @"C:\templates\d.dta";

    private static TemplateRecord Active() => EditingFixtures.Record(
        FileA,
        EditingFixtures.Template(EditingFixtures.Variable("alpha", "HELLO", description: "First")));

    private static TemplateRecord Same() => EditingFixtures.Record(
        FileB,
        EditingFixtures.Template(EditingFixtures.Variable("alpha", "HELLO", description: "First")));

    private static TemplateRecord Different() => EditingFixtures.Record(
        FileC,
        EditingFixtures.Template(EditingFixtures.Variable("alpha", "OTHER", access: "external", description: "First")));

    private static TemplateRecord NoAlpha() => EditingFixtures.Record(
        FileD,
        EditingFixtures.Template(EditingFixtures.Variable("gamma", "x")));

    private static TemplateRecord DuplicateAlpha() => EditingFixtures.Record(
        FileD,
        EditingFixtures.Template(
            EditingFixtures.Variable("alpha", "HELLO", description: "First"),
            EditingFixtures.Variable("alpha", "HELLO", description: "First")));

    [Fact]
    public void TheActiveTemplateIsAlwaysTheReferenceRow()
    {
        var drafts = new DraftStore();
        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(Active(), "alpha"), [Same()], drafts);

        Assert.Equal(2, rows.Count);
        Assert.Equal(CompareState.Self, rows[0].State);
        Assert.Equal(FileA, rows[0].FilePath);
    }

    [Fact]
    public void ExactNameMatchesAreClassifiedPerField()
    {
        TemplateRecord active = Active();
        var drafts = new DraftStore();

        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [Same(), Different()], drafts);

        TemplateComparison same = rows.Single(r => r.FilePath == FileB);
        Assert.Equal(CompareState.Same, same.State);
        Assert.All(same.Differences, d => Assert.Equal(FieldCompareState.Equal, d.State));

        TemplateComparison other = rows.Single(r => r.FilePath == FileC);
        Assert.Equal(CompareState.Different, other.State);
        FieldDiff access = other.Differences.Single(d => d.Field == FieldKind.Access);
        Assert.Equal(FieldCompareState.Different, access.State);
        Assert.Equal("internal", access.ActiveValue);
        Assert.Equal("external", access.TargetValue);
        FieldDiff defaultValue = other.Differences.Single(d => d.Field == FieldKind.DefaultValue);
        Assert.Equal(FieldCompareState.Different, defaultValue.State);
        Assert.Contains(defaultValue.Lines, l => l.Changed && l.Text == "OTHER");
    }

    [Fact]
    public void NamesAreMatchedExactlyNotCaseOrWhitespaceInsensitively()
    {
        TemplateRecord active = Active();
        TemplateRecord other = EditingFixtures.Record(
            FileB,
            EditingFixtures.Template(EditingFixtures.Variable("Alpha", "HELLO", description: "First")));

        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [other], new DraftStore());

        Assert.Equal(CompareState.Missing, rows.Single(r => r.FilePath == FileB).State);
    }

    [Fact]
    public void AbsentNameAndAmbiguousNameAreReportedSeparately()
    {
        var drafts = new DraftStore();
        TemplateRecord active = Active();

        IReadOnlyList<TemplateComparison> missing = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [NoAlpha()], drafts);
        TemplateComparison missingRow = missing.Single(r => r.FilePath == FileD);
        Assert.Equal(CompareState.Missing, missingRow.State);
        Assert.Contains("No declaration named 'alpha'", missingRow.Reason, StringComparison.Ordinal);

        IReadOnlyList<TemplateComparison> ambiguous = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [DuplicateAlpha()], drafts);
        TemplateComparison ambiguousRow = ambiguous.Single(r => r.FilePath == FileD);
        Assert.Equal(CompareState.Ineligible, ambiguousRow.State);
        Assert.Contains("declared 2 times", ambiguousRow.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyValuesAndMissingFieldsHaveDistinctStates()
    {
        TemplateRecord active = EditingFixtures.Record(
            FileA,
            EditingFixtures.Template(EditingFixtures.Variable("alpha", "")));

        TemplateRecord emptyElsewhere = EditingFixtures.Record(
            FileB,
            EditingFixtures.Template(EditingFixtures.Variable("alpha", "")));

        TemplateRecord noField = EditingFixtures.Record(
            FileC,
            EditingFixtures.Template(EditingFixtures.Variable("alpha", null)));

        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [emptyElsewhere, noField], new DraftStore());

        FieldDiff empty = rows.Single(r => r.FilePath == FileB)
            .Differences.Single(d => d.Field == FieldKind.DefaultValue);
        Assert.Equal(FieldCompareState.EmptyValue, empty.State);
        Assert.Equal(string.Empty, empty.TargetValue);

        FieldDiff missing = rows.Single(r => r.FilePath == FileC)
            .Differences.Single(d => d.Field == FieldKind.DefaultValue);
        Assert.Equal(FieldCompareState.MissingField, missing.State);
        Assert.Null(missing.TargetValue);
    }

    [Fact]
    public void StagedDraftsAreVisibleInTheComparisonAndMarkedAsDrafts()
    {
        TemplateRecord active = Active();
        TemplateRecord other = Same();
        var drafts = new DraftStore();

        drafts.Stage(FileA, EditingFixtures.Decl(active, "alpha"), FieldKind.DefaultValue, "WORLD");
        drafts.Stage(FileB, EditingFixtures.Decl(other, "alpha"), FieldKind.DefaultValue, "DRAFTED");

        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [other], drafts);

        FieldDiff diff = rows.Single(r => r.FilePath == FileB)
            .Differences.Single(d => d.Field == FieldKind.DefaultValue);

        Assert.Equal(FieldCompareState.Different, diff.State);
        Assert.Equal("WORLD", diff.ActiveValue);
        Assert.Equal("DRAFTED", diff.TargetValue);
        Assert.True(diff.ActiveHasDraft);
        Assert.True(diff.TargetHasDraft);
        Assert.True(rows.Single(r => r.FilePath == FileB).HasUnsavedDrafts);
    }

    [Fact]
    public void TemplatesNotReadInThisSessionAreNamedAsNotCompared()
    {
        TemplateRecord active = Active();
        var unread = EditingFixtures.Record(FileD, string.Empty, TemplateReadState.NotRead);
        var broken = EditingFixtures.Record(FileC, string.Empty, TemplateReadState.Error);

        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [unread, broken], new DraftStore());

        Assert.Equal(CompareState.NotLoaded, rows.Single(r => r.FilePath == FileD).State);
        Assert.Contains("has not been read", rows.Single(r => r.FilePath == FileD).Reason, StringComparison.Ordinal);
        Assert.Contains("container entry", rows.Single(r => r.FilePath == FileC).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopeLimitsWhichLoadedTemplatesAreCompared()
    {
        TemplateRecord active = Active();
        IReadOnlyList<TemplateComparison> rows = ParameterComparator.Compare(
            FileA, EditingFixtures.Decl(active, "alpha"), [Same(), Different()], new DraftStore(),
            scope: new HashSet<string> { FileB });

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.FilePath == FileB);
        Assert.DoesNotContain(rows, r => r.FilePath == FileC);
    }
}

public class BulkPlanTests
{
    private const string FileA = @"C:\templates\a.dta";
    private const string FileB = @"C:\templates\b.dta";
    private const string FileC = @"C:\templates\c.dta";
    private const string FileD = @"C:\templates\d.dta";
    private const string FileE = @"C:\templates\e.dta";

    private static TemplateRecord MissingOne() => EditingFixtures.Record(
        FileB, EditingFixtures.Template(EditingFixtures.Variable("alpha", "OLD_B", access: "external", description: "B desc")));

    private static TemplateRecord Ambiguous() => EditingFixtures.Record(
        FileC, EditingFixtures.Template(
            EditingFixtures.Variable("alpha", "X"),
            EditingFixtures.Variable("alpha", "Y")));

    private static TemplateRecord WithoutDefault() => EditingFixtures.Record(
        FileD, EditingFixtures.Template(EditingFixtures.Variable("alpha", null)));

    private static TemplateRecord UnsupportedDefault() => EditingFixtures.Record(
        FileE, EditingFixtures.Template(EditingFixtures.Variable("alpha", "one\ntwo")));

    [Fact]
    public void EveryTargetGetsAStatusAndIneligibleTargetsGetAReason()
    {
        var records = new[]
        {
            EditingFixtures.Record(FileA, EditingFixtures.Template(EditingFixtures.Variable("alpha", "OLD_A"))),
            EditingFixtures.Record(FileB, EditingFixtures.Template(EditingFixtures.Variable("gamma", "x"))),
            Ambiguous(),
            WithoutDefault(),
            UnsupportedDefault(),
            EditingFixtures.Record(FileE + ".unread", string.Empty, TemplateReadState.NotRead),
        };

        IReadOnlyList<BulkTarget> plan = BulkPlan.Plan("alpha", records, new DraftStore());

        BulkTarget ByPath(string path) => plan.Single(t => t.FilePath == path);
        Assert.Equal(BulkTargetStatus.Eligible, ByPath(FileA).Status);
        Assert.Equal("OLD_A", ByPath(FileA).CurrentValue);
        Assert.Equal(BulkTargetStatus.Missing, ByPath(FileB).Status);
        Assert.Contains("is not declared", ByPath(FileB).Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BulkTargetStatus.Ambiguous, ByPath(FileC).Status);
        Assert.Contains("writing would be a guess", ByPath(FileC).Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BulkTargetStatus.ReadOnlyShape, ByPath(FileD).Status);
        Assert.Contains("no 'default' property", ByPath(FileD).Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BulkTargetStatus.ReadOnlyShape, ByPath(FileE).Status);
        Assert.Equal(BulkTargetStatus.NotLoaded, ByPath(FileE + ".unread").Status);
        Assert.False(ByPath(FileC).IsSelectable);
        Assert.Equal(2, plan.Count(t => t.Status == BulkTargetStatus.ReadOnlyShape));
    }

    [Fact]
    public void BulkOnlyChangesDefaultValuesAndNeverOffersOtherFields()
    {
        var records = new[] { EditingFixtures.Record(FileA, EditingFixtures.Template(EditingFixtures.Variable("alpha", "OLD_A"))) };

        Assert.Throws<ArgumentException>(() => BulkPlan.Plan("alpha", records, new DraftStore(), null, FieldKind.Access));
        Assert.Throws<ArgumentException>(() => BulkPlan.Plan("alpha", records, new DraftStore(), null, FieldKind.Description));
    }

    [Fact]
    public void StagedBulkValueLeavesTargetAccessAndDescriptionUntouched()
    {
        TemplateRecord target = MissingOne(); // access external, description "B desc"
        VariableDeclaration declaration = EditingFixtures.Decl(target, "alpha");
        var drafts = new DraftStore();

        IReadOnlyList<BulkTarget> plan = BulkPlan.Plan("alpha", [target], drafts);
        Assert.Equal(BulkTargetStatus.Eligible, plan.Single().Status);

        StageResult staged = drafts.Stage(FileB, declaration, FieldKind.DefaultValue, "NEW VALUE");
        Assert.Equal(StageStatus.Staged, staged.Status);

        Assert.Single(drafts.Changes);
        Assert.Equal(FieldKind.DefaultValue, drafts.Changes.Single().Field);
        Assert.Equal("external", drafts.EffectiveValue(FileB, declaration, FieldKind.Access));
        Assert.Equal("B desc", drafts.EffectiveValue(FileB, declaration, FieldKind.Description));
        Assert.Equal("NEW VALUE", drafts.EffectiveValue(FileB, declaration, FieldKind.DefaultValue));

        // The parsed baseline model still describes the file as it is on disk.
        Assert.Equal("OLD_B", declaration.DefaultValue.LogicalValue);
    }

    [Fact]
    public void BulkStagingRefusesTargetsThatAreNotEligible()
    {
        var drafts = new DraftStore();
        var records = new[] { Ambiguous(), UnsupportedDefault(), MissingOne() };

        // A hand-made target list that ignores the plan must still be refused by the core layer.
        var forged = new[]
        {
            new BulkTarget(FileC, "c.dta", BulkTargetStatus.Ambiguous, "'alpha' is declared 2 times", 0, null, false),
            new BulkTarget(FileE, "e.dta", BulkTargetStatus.ReadOnlyShape, "unobserved default shape", 0, null, false),
        };

        foreach ((BulkTarget _, StageResult result) in BulkPlan.StageValues("alpha", "x", forged, records, drafts))
        {
            Assert.Equal(StageStatus.Rejected, result.Status);
        }

        Assert.False(drafts.HasAny);

        // Only plan-eligible targets stage through the same call, and only default values.
        IReadOnlyList<BulkTarget> plan = BulkPlan.Plan("alpha", records, drafts);
        IReadOnlyList<(BulkTarget Target, StageResult Result)> outcomes =
            BulkPlan.StageValues("alpha", "NEW", plan.Where(t => t.IsSelectable), records, drafts);

        Assert.Single(outcomes);
        Assert.Equal(StageStatus.Staged, outcomes[0].Result.Status);
        Assert.Equal(1, drafts.Count);
        Assert.Equal(FieldKind.DefaultValue, drafts.Changes.Single().Field);
    }

    [Fact]
    public void ABlankOrMissingSourceNameProducesNoTargets()
    {
        var records = new[] { MissingOne() };
        Assert.Empty(BulkPlan.Plan(null, records, new DraftStore()));
        Assert.Empty(BulkPlan.Plan(string.Empty, records, new DraftStore()));
    }

    [Fact]
    public void ScopeLimitsTheCandidateList()
    {
        var records = new[] { MissingOne(), Ambiguous() };
        IReadOnlyList<BulkTarget> plan = BulkPlan.Plan(
            "alpha", records, new DraftStore(), scope: new HashSet<string> { FileB });

        Assert.Single(plan);
        Assert.Equal(FileB, plan[0].FilePath);
    }
}
