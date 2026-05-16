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
        var controller = new SingingEvaluationController(Mock.Of<IEvaluationService>());
        await using var performanceStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var performanceFile = new FormFile(performanceStream, 0, performanceStream.Length, "performanceFile", "performance.wav");

        var action = await controller.Submit(
            performanceFile,
            null,
            cancellationToken: default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var payload = Assert.IsType<ApiResponse<EvaluationSubmitResponse>>(badRequest.Value);
        Assert.Equal(40001, payload.Code);
        Assert.Equal("referenceFile required", payload.Message);
    }
}
