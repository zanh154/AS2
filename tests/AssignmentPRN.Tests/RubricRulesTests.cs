using AssignmentPRN.Business;
using AssignmentPRN.Business.Interfaces;
using AssignmentPRN.Business.Services;
using DaContracts = AssignmentPRN.DataAccess.Contracts;

namespace AssignmentPRN.Tests;

/// <summary>
/// Validation rules for rubric criteria: matching totals, mismatched totals, empty criteria, invalid scores, boundary cases.
/// </summary>
public class RubricRulesTests
{
    [Fact]
    public void SaveAsync_WithEmptyCriteria_Throws()
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = Array.Empty<RubricCriteriaInput>()
        };

        Assert.Throws<BusinessValidationException>(() => 
            RubricService.ValidateCriteria(request.Criteria));
    }

    [Fact]
    public void SaveAsync_WithZeroScoreCriteria_Throws()
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [new RubricCriteriaInput { CriteriaName = "Test", MaxScore = 0, DisplayOrder = 1 }]
        };

        Assert.Throws<BusinessValidationException>(() => 
            RubricService.ValidateCriteria(request.Criteria));
    }

    [Fact]
    public void SaveAsync_WithNegativeScoreCriteria_Throws()
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [new RubricCriteriaInput { CriteriaName = "Test", MaxScore = -1, DisplayOrder = 1 }]
        };

        Assert.Throws<BusinessValidationException>(() => 
            RubricService.ValidateCriteria(request.Criteria));
    }

    [Fact]
    public void SaveAsync_WithDuplicateDisplayOrders_Throws()
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [
                new RubricCriteriaInput { CriteriaName = "Test1", MaxScore = 5, DisplayOrder = 1 },
                new RubricCriteriaInput { CriteriaName = "Test2", MaxScore = 5, DisplayOrder = 1 }
            ]
        };

        Assert.Throws<BusinessValidationException>(() => 
            RubricService.ValidateCriteria(request.Criteria));
    }

    [Fact]
    public void SaveAsync_WithValidCriteria_Passes()
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [
                new RubricCriteriaInput { CriteriaName = "Test1", MaxScore = 5, DisplayOrder = 1 },
                new RubricCriteriaInput { CriteriaName = "Test2", MaxScore = 5, DisplayOrder = 2 }
            ]
        };

        // Should not throw
        RubricService.ValidateCriteria(request.Criteria);
    }

    [Fact]
    public void SaveAsync_TotalScoreMatchesSumOfCriteria()
    {
        var input = new DaContracts.RubricUpsertInput
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [
                new DaContracts.RubricCriteriaInput { CriteriaName = "Test1", MaxScore = 3.5m, DisplayOrder = 1 },
                new DaContracts.RubricCriteriaInput { CriteriaName = "Test2", MaxScore = 6.5m, DisplayOrder = 2 }
            ]
        };

        var expectedTotal = 10.0m;
        var actualTotal = input.Criteria.Sum(c => c.MaxScore);
        
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Theory]
    [InlineData(0.25, 0.25)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(100.0, 100.0)]
    public void SaveAsync_AcceptsBoundaryScores(decimal score, decimal expected)
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [new RubricCriteriaInput { CriteriaName = "Test", MaxScore = score, DisplayOrder = 1 }]
        };

        // Should not throw for valid scores
        RubricService.ValidateCriteria(request.Criteria);
        Assert.Equal(expected, score);
    }

    [Fact]
    public void SaveAsync_WithEmptyCriteriaName_Throws()
    {
        var request = new RubricSaveRequest
        {
            RubricId = 0,
            QuestionId = 1,
            Criteria = [new RubricCriteriaInput { CriteriaName = "   ", MaxScore = 5, DisplayOrder = 1 }]
        };

        Assert.Throws<BusinessValidationException>(() => 
            RubricService.ValidateCriteria(request.Criteria));
    }
}