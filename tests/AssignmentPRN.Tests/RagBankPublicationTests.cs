
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using AssignmentPRN.Business.Policies;
using AssignmentPRN.DataAccess.Repositories;
using AssignmentPRN.DataAccess.Entities;
using Da = AssignmentPRN.DataAccess.Entities;
using DocumentStatus = AssignmentPRN.Business.Interfaces.DocumentStatus;
using GeneratedQuestionStatus = AssignmentPRN.Business.Interfaces.GeneratedQuestionStatus;
using DocumentBloomLevel = AssignmentPRN.Business.Interfaces.DocumentBloomLevel;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AssignmentPRN.Tests;
public partial class DocumentOperationalTests
{
 [Theory]
 [InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
 public async Task Approval_publishes_each_Bloom_level_once_and_preserves_bank_after_document_deletion(int bloom) {
  await using var db=await OpenAsync();
  var service=Service(db);
  var result=await service.ReviewQuestionAsync(1,1,new(GeneratedQuestionStatus.Approved,null,"Oral question","Expected",(string?)null,(DocumentBloomLevel)bloom));
  Assert.True(result.Success,result.Error);db.ChangeTracker.Clear();
  var source=await db.GeneratedQuestions.SingleAsync();Assert.NotNull(source.BankQuestionId);
  var bank=await db.Questions.SingleAsync();
  Assert.Equal(source.BankQuestionId,bank.QuestionId);Assert.Equal(bloom-1,(int)bank.BloomLevel);
  Assert.Equal("Expected",bank.ExpectedAnswer);Assert.Equal(AssignmentPRN.DataAccess.Enums.QuestionSource.AI,bank.Source);
  Assert.False((await service.ReviewQuestionAsync(1,1,new(GeneratedQuestionStatus.Approved,null))).Success);
  await new DocumentRepository(db).ReviewAsync(1,_=>{});
  Assert.Single(await db.Questions.ToListAsync());
  await new DocumentRepository(db).DeleteDocumentAsync(1);
  Assert.Single(await db.Questions.ToListAsync());
 }
 [Fact]
 public async Task Approval_files_the_bank_question_under_the_chosen_topic() {
  await using var db=await OpenAsync();
  db.CourseMaterials.Add(Material(7,1));await db.SaveChangesAsync();
  var result=await Service(db).ReviewQuestionAsync(1,1,new(GeneratedQuestionStatus.Approved,null,MaterialId:7));
  Assert.True(result.Success,result.Error);db.ChangeTracker.Clear();
  Assert.Equal(7,(await db.Questions.SingleAsync()).SourceMaterialId);
 }
 [Fact]
 public async Task Approval_rejects_a_topic_from_another_course() {
  await using var db=await OpenAsync();
  db.Courses.Add(new Course{CourseId=2,LecturerId=1,CourseCode="OTHER",CourseName="Other",IsActive=true});
  db.CourseMaterials.Add(Material(8,2));await db.SaveChangesAsync();
  var result=await Service(db).ReviewQuestionAsync(1,1,new(GeneratedQuestionStatus.Approved,null,MaterialId:8));
  Assert.False(result.Success);db.ChangeTracker.Clear();
  Assert.Empty(await db.Questions.ToListAsync());
  Assert.Equal(Da.GeneratedQuestionStatus.PendingReview,(await db.GeneratedQuestions.SingleAsync()).Status);
 }
 private static CourseMaterial Material(int id,int courseId)=>new(){MaterialId=id,CourseId=courseId,FileName=$"topic-{id}.pdf",FilePath=$"materials/{id}.pdf",FileType=AssignmentPRN.DataAccess.Enums.MaterialFileType.PDF,UploadedBy=1,UploadedAt=DateTime.Now};
 [Fact]
 public async Task Failed_bank_insert_rolls_back_approval() {
  await using var db=await OpenAsync();
  await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_bank BEFORE INSERT ON questions BEGIN SELECT RAISE(ABORT,'audit bank failure'); END;");
  var result=await Service(db).ReviewQuestionAsync(1,1,new(GeneratedQuestionStatus.Approved,null));
  Assert.False(result.Success);db.ChangeTracker.Clear();
  Assert.Equal(Da.GeneratedQuestionStatus.PendingReview,(await db.GeneratedQuestions.SingleAsync()).Status);
  Assert.Empty(await db.Questions.ToListAsync());
 }
}
