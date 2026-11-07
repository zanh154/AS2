[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Config = 'src/AssignmentPRN.Presentation/appsettings.json'
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$configPath = [IO.Path]::GetFullPath($Config, $workspace)
$sqlPath = Join-Path $workspace 'database/20261017_rag_question_bank.sql'
$runner = Join-Path $workspace '.runtime/rag-bank-migration'
New-Item -ItemType Directory -Path $runner -Force -WhatIf:$false | Out-Null
$program = @'
using System.Text.Json;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Data;
using AssignmentPRN.DataAccess.Entities;
using AssignmentPRN.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using var config = JsonDocument.Parse(File.ReadAllText(args[0]));
var settings = new MySqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!) { AllowUserVariables = true };
var cs = settings.ConnectionString;
await using var connection = new MySqlConnection(cs);
await connection.OpenAsync();
using(var probe = new MySqlCommand("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='generated_questions' AND column_name='bank_question_id'",connection))
    Console.WriteLine("RAG bank column present: "+await probe.ExecuteScalarAsync());
if(args[2]=="check") return;
using(var migration = new MySqlCommand(File.ReadAllText(args[1]),connection)) {
    migration.CommandTimeout=120;
    await migration.ExecuteNonQueryAsync();
}
var options = new DbContextOptionsBuilder<AivesDbContext>().UseMySql(cs,new MySqlServerVersion(new Version(8,4,8))).Options;
List<int> pending;
await using(var db = new AivesDbContext(options)) pending = await db.GeneratedQuestions.AsNoTracking()
    .Where(x=>x.Status==GeneratedQuestionStatus.Approved&&x.BankQuestionId==null).Select(x=>x.QuestionId).ToListAsync();
foreach(var id in pending) {
    await using var db=new AivesDbContext(options);
    await new DocumentRepository(db).ReviewAsync(id,_=>{});
}
Console.WriteLine("Previously approved RAG questions published: "+pending.Count);
Console.WriteLine("RAG bank migration/backfill completed.");
'@
$businessPath = [Security.SecurityElement]::Escape((Join-Path $workspace 'src/AssignmentPRN.Business/AssignmentPRN.Business.csproj'))
$project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup><ItemGroup><ProjectReference Include="' + $businessPath + '" /></ItemGroup></Project>'
[IO.File]::WriteAllText((Join-Path $runner 'Program.cs'),$program,[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $runner 'runner.csproj'),$project,[Text.UTF8Encoding]::new($false))
$mode = if($PSCmdlet.ShouldProcess($sqlPath,'Apply schema and publish previously approved RAG questions')) {'apply'} else {'check'}
dotnet run --project (Join-Path $runner 'runner.csproj') -p:BuildProjectReferences=false --verbosity quiet -- $configPath $sqlPath $mode
if($LASTEXITCODE -ne 0) {throw 'RAG bank migration failed'}
