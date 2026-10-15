# Chạy một script .sql lên database dùng chung.
#
# Chuỗi kết nối được đọc từ một file appsettings.json có sẵn của bạn và chỉ sống trong
# tiến trình con; script không in nó ra và không chép nó đi đâu.
#
#   pwsh scripts/apply-migration.ps1 -Config <đường-dẫn-appsettings.json> -WhatIf
#   pwsh scripts/apply-migration.ps1 -Config <đường-dẫn-appsettings.json>
#
# -WhatIf chỉ kiểm tra: kết nối được không, bảng script cần có chưa, bảng script tạo đã
# tồn tại chưa. Không ghi gì cả. Hãy chạy -WhatIf trước.
#
# Script mặc định là database/20261016_ai_evaluation_criteria.sql; nó dùng
# CREATE TABLE IF NOT EXISTS nên chạy lại nhiều lần cũng không sao, và không sửa hay xoá
# bảng nào đang có.
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$Config,
    [string]$SqlFile = 'database/20261016_ai_evaluation_criteria.sql'
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$sqlPath = if ([System.IO.Path]::IsPathRooted($SqlFile)) { $SqlFile } else { Join-Path $workspace $SqlFile }
if (-not (Test-Path -LiteralPath $sqlPath)) { throw "Không tìm thấy script SQL: $sqlPath" }
if (-not (Test-Path -LiteralPath $Config)) { throw "Không tìm thấy file cấu hình: $Config" }

$runner = Join-Path ([System.IO.Path]::GetTempPath()) ("aives-apply-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $runner -WhatIf:$false | Out-Null
try {
    $program = @'
using System.Text.Json;
using MySqlConnector;

var (configPath, sqlPath, mode) = (args[0], args[1], args[2]);

using var document = JsonDocument.Parse(File.ReadAllText(configPath));
if (!document.RootElement.TryGetProperty("ConnectionStrings", out var section)
    || !section.TryGetProperty("DefaultConnection", out var value)
    || value.GetString() is not { Length: > 0 } connectionString)
{
    Console.Error.WriteLine("File cấu hình không có ConnectionStrings:DefaultConnection.");
    return 2;
}

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();
// Tên database và host thì in được; chuỗi kết nối đầy đủ thì không.
Console.WriteLine($"Đã kết nối: {connection.Database} trên {connection.DataSource}");

async Task<bool> ExistsAsync(string name)
{
    await using var command = new MySqlCommand(
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @name",
        connection);
    command.Parameters.AddWithValue("@name", name);
    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}

// Script tham chiếu hai bảng này; thiếu một trong hai thì khoá ngoại sẽ lỗi.
foreach (var required in new[] { "ai_evaluations", "rubric_criteria" })
{
    var exists = await ExistsAsync(required);
    Console.WriteLine($"  bảng {required}: {(exists ? "có" : "THIẾU")}");
    if (!exists)
    {
        Console.Error.WriteLine($"Thiếu bảng {required}. Chạy các migration trước đó (database/README.md) rồi thử lại.");
        return 3;
    }
}

const string target = "ai_evaluation_criteria";
if (await ExistsAsync(target))
{
    Console.WriteLine($"  bảng {target}: đã có, không cần chạy lại.");
    return 0;
}

if (mode == "check")
{
    Console.WriteLine($"  bảng {target}: chưa có. Bỏ -WhatIf để tạo.");
    return 0;
}

await using (var command = new MySqlCommand(File.ReadAllText(sqlPath), connection))
{
    await command.ExecuteNonQueryAsync();
}

Console.WriteLine(await ExistsAsync(target)
    ? $"Đã tạo bảng {target}."
    : $"Chạy xong nhưng không thấy bảng {target}; hãy kiểm tra lại script.");
return 0;
'@

    $csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MySqlConnector" Version="2.4.0" />
  </ItemGroup>
</Project>
'@

    Set-Content -Path (Join-Path $runner 'Program.cs') -Value $program -Encoding utf8 -WhatIf:$false
    Set-Content -Path (Join-Path $runner 'runner.csproj') -Value $csproj -Encoding utf8 -WhatIf:$false

    $mode = if ($PSCmdlet.ShouldProcess($sqlPath, 'Chạy trên database mà file cấu hình trỏ tới')) { 'apply' } else { 'check' }
    dotnet run --project (Join-Path $runner 'runner.csproj') --verbosity quiet -- $Config $sqlPath $mode
    if ($LASTEXITCODE -ne 0) { throw "Không chạy được script (mã lỗi $LASTEXITCODE)." }
}
finally {
    Remove-Item -Recurse -Force $runner -ErrorAction SilentlyContinue -WhatIf:$false
}
