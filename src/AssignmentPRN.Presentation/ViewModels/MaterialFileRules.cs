using AssignmentPRN.Business.Services;
using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Policies;

namespace AssignmentPRN.Presentation.ViewModels;

/// <summary>
/// Upload rules for the course materials. Kept beside the views so the extension list
/// lives next to the label that tells the lecturer what to pick.
/// </summary>
public static class MaterialFileRules
{
    /// <summary>Keeps a single upload from filling the disk; PDF/DOCX/PPTX are the allowed types.</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    private static readonly Dictionary<string, MaterialFileType> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = MaterialFileType.PDF,
        [".docx"] = MaterialFileType.DOCX,
        [".pptx"] = MaterialFileType.PPTX
    };

    public static IReadOnlyList<string> AcceptedExtensions => [.. Allowed.Keys];

    public static string AcceptAttribute => string.Join(',', AcceptedExtensions);

    /// <summary>Maps an extension to the stored enum, or null when the file is not allowed.</summary>
    public static MaterialFileType? ResolveType(string? fileName) =>
        Path.GetExtension(fileName ?? string.Empty) is { Length: > 0 } extension
        && Allowed.TryGetValue(extension, out var type)
            ? type
            : null;
}

/// <summary>Upload rules and the downloadable starter file for the question import.</summary>
public static class CsvImportRules
{
    public const string Extension = ".csv";

    /// <summary>Rows are small; a file this size is already far past a useful import.</summary>
    public const long MaxBytes = 2L * 1024 * 1024;

    public const string AcceptAttribute = ".csv,text/csv";

    public static bool ResolveCsv(string? fileName) =>
        string.Equals(Path.GetExtension(fileName ?? string.Empty), Extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A header plus two worked rows. The topic column matches the stored file name of a
    /// material, and <c>correct</c> is the letter of the right choice.
    /// </summary>
    public static string Content => string.Join('\n',
        [
            "question_text;difficulty;bloom;material;expected_answer;option_a;option_b;option_c;option_d;correct",
            "Mô hình nào phân loại dữ liệu theo tốc độ thay đổi và độ mơ hồ?;Medium;Understand;bai-giang-01.pdf;Mô hình NoSQL;Mô hình quan hệ;Mô hình NoSQL;Mô hình phân cấp;Mô hình mạng;B",
            "Hành động nào đảm bảo tính toàn vẹn giao dịch?;Hard;Apply;;Ràng buộc ACID;Khóa chính;Giao dịch;Chỉ mục;B"
        ]) + '\n';
}
