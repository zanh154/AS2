using AssignmentPRN.Business;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Tests;

/// <summary>
/// The bulk-import parser. The rule under test throughout: a bad line is reported with
/// its line number and skipped, while every good line of the same file still comes through.
/// </summary>
public class QuestionCsvImportTests
{
    private const string Header =
        "question_text;difficulty;bloom;material;expected_answer;option_a;option_b;option_c;option_d;correct";

    private static readonly Dictionary<string, int> Materials = new()
    {
        ["demo.pdf"] = 4,
        ["chuong-1.docx"] = 9
    };

    private static IReadOnlyList<QuestionImportRow> Parse(string body, out IReadOnlyList<string> errors) =>
        QuestionService.ParseCsv(Header + "\n" + body, Materials, out errors);

    [Fact]
    public void Parses_a_well_formed_row()
    {
        var rows = Parse("Thủ đô Việt Nam?;Easy;Remember;demo.pdf;Hà Nội;Hà Nội;Huế;Đà Nẵng;Cần Thơ;A", out var errors);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal("Thủ đô Việt Nam?", row.QuestionText);
        Assert.Equal(QuestionDifficulty.Easy, row.Difficulty);
        Assert.Equal(BloomLevel.Remember, row.BloomLevel);
        Assert.Equal(4, row.MaterialId);
        Assert.Equal("Hà Nội", row.ExpectedAnswer);
        Assert.Equal(4, row.Options.Count);
        Assert.Equal("Hà Nội", Assert.Single(row.Options, option => option.IsCorrect).Text);
    }

    [Fact]
    public void Accepts_a_numeric_correct_column()
    {
        var rows = Parse("Câu hỏi;Medium;Apply;demo.pdf;;A;B;C;D;3", out var errors);

        Assert.Empty(errors);
        Assert.Equal("C", Assert.Single(Assert.Single(rows).Options, option => option.IsCorrect).Text);
    }

    [Fact]
    public void Falls_back_to_medium_and_understand_when_the_metadata_is_blank()
    {
        var rows = Parse("Câu hỏi;;;demo.pdf;;A;B;;;A", out var errors);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal(QuestionDifficulty.Medium, row.Difficulty);
        Assert.Equal(BloomLevel.Understand, row.BloomLevel);
        Assert.Equal(2, row.Options.Count);
    }

    [Fact]
    public void Keeps_the_good_lines_and_reports_the_bad_one_with_its_line_number()
    {
        var rows = Parse(
            "Câu 1;Easy;Remember;demo.pdf;;A;B;C;D;A\n"
            + "Câu 2;Easy;Remember;demo.pdf;;A;;;;A\n"
            + "Câu 3;Hard;Analyze;demo.pdf;;A;B;C;D;B",
            out var errors);

        Assert.Equal(2, rows.Count);
        Assert.Equal(["Câu 1", "Câu 3"], rows.Select(row => row.QuestionText));
        Assert.Contains("Dòng 3", Assert.Single(errors));
    }

    [Fact]
    public void Rejects_a_correct_column_pointing_outside_the_options()
    {
        var rows = Parse("Câu hỏi;Easy;Remember;demo.pdf;;A;B;;;D", out var errors);

        Assert.Empty(rows);
        Assert.Contains("Dòng 2", Assert.Single(errors));
    }

    [Fact]
    public void Rejects_a_row_without_a_correct_column_value()
    {
        var rows = Parse("Câu hỏi;Easy;Remember;demo.pdf;;A;B;C;D;", out var errors);

        Assert.Empty(rows);
        Assert.Single(errors);
    }

    [Fact]
    public void Quoted_cells_may_contain_the_separator()
    {
        var rows = Parse("\"Chọn A; hoặc B?\";Easy;Remember;demo.pdf;;A;B;;;A", out var errors);

        Assert.Empty(errors);
        Assert.Equal("Chọn A; hoặc B?", Assert.Single(rows).QuestionText);
    }

    [Fact]
    public void Comma_separated_files_are_read_too()
    {
        var content = "question_text,difficulty,bloom,material,expected_answer,option_a,option_b,correct\n"
            + "Câu hỏi,Hard,Analyze,demo.pdf,,A,B,B";

        var rows = QuestionService.ParseCsv(content, Materials, out var errors);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal(QuestionDifficulty.Hard, row.Difficulty);
        Assert.Equal(BloomLevel.Analyze, row.BloomLevel);
    }

    [Fact]
    public void An_unknown_material_leaves_the_topic_empty_rather_than_failing_the_line()
    {
        var rows = Parse("Câu hỏi;Easy;Remember;khong-co.pdf;;A;B;;;A", out var errors);

        Assert.Empty(errors);
        Assert.Null(Assert.Single(rows).MaterialId);
    }

    [Fact]
    public void Material_names_match_regardless_of_case()
    {
        Assert.Equal(9, QuestionService.ResolveCsvMaterialId("CHUONG-1.DOCX", Materials));
        Assert.Null(QuestionService.ResolveCsvMaterialId("   ", Materials));
    }

    [Fact]
    public void An_empty_file_is_reported_instead_of_throwing()
    {
        var rows = QuestionService.ParseCsv(string.Empty, Materials, out var errors);

        Assert.Empty(rows);
        Assert.Single(errors);
    }

    [Fact]
    public void A_file_without_the_question_column_is_rejected()
    {
        var rows = QuestionService.ParseCsv("a;b;c\n1;2;3", Materials, out var errors);

        Assert.Empty(rows);
        Assert.Contains("question_text", Assert.Single(errors));
    }

    [Fact]
    public void A_file_with_fewer_than_two_option_columns_is_rejected()
    {
        var rows = QuestionService.ParseCsv(
            "question_text;option_a;correct\nCâu hỏi;A;A", Materials, out var errors);

        Assert.Empty(rows);
        Assert.Single(errors);
    }
}
