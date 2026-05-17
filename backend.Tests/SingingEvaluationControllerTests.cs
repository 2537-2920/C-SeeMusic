using System.IO;
using System.Threading.Tasks;
using backend.Controllers;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace SeeMusic.Backend.Tests;

public class SingingEvaluationControllerTests
{
    [Fact]
    public async Task Submit_ShouldRejectMissingReferenceFile()
    {
        var controller = new SingingEvaluationController(
            Mock.Of<IInstantSingingEvaluationService>(),
            Mock.Of<ITransposeSuggestionService>(),
            Mock.Of<IPdfExportService>());
        await using var performanceStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var performanceFile = new FormFile(performanceStream, 0, performanceStream.Length, "performanceFile", "performance.wav");

        var action = await controller.Submit(
            performanceFile,
            null,
            cancellationToken: default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<ApiResponse<EvaluationReportResponse>>(badRequest.Value);
        Assert.Equal(40001, payload.Code);
        Assert.Equal("referenceFile required", payload.Message);
    }

    [Fact]
    public void GetTransposeSuggestion_ShouldRejectMissingTransposeBase()
    {
        var controller = new SingingEvaluationController(
            Mock.Of<IInstantSingingEvaluationService>(),
            Mock.Of<ITransposeSuggestionService>(),
            Mock.Of<IPdfExportService>());

        var action = controller.GetTransposeSuggestion(new TransposeSuggestionRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<ApiResponse<TransposeSuggestionResponse>>(badRequest.Value);
        Assert.Equal(40001, payload.Code);
        Assert.Equal("transposeBase required", payload.Message);
    }

    [Fact]
    public void ExportPdf_ShouldReturnPdfFile()
    {
        var pdfExportService = new Mock<IPdfExportService>();
        pdfExportService
            .Setup(service => service.Export(It.IsAny<EvaluationReportResponse>()))
            .Returns(new byte[] { 1, 2, 3, 4 });

        var controller = new SingingEvaluationController(
            Mock.Of<IInstantSingingEvaluationService>(),
            Mock.Of<ITransposeSuggestionService>(),
            pdfExportService.Object);

        var result = controller.ExportPdf(new EvaluationPdfExportRequest
        {
            Report = new EvaluationReportResponse
            {
                Summary = new EvaluationSummaryDto
                {
                    AnalysisId = "demo123"
                }
            }
        });

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("singing-evaluation-demo123.pdf", fileResult.FileDownloadName);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, fileResult.FileContents);
    }
}
