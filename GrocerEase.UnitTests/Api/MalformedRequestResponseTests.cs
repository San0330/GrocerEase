using GrocerEase.Api.Errors;
using GrocerEase.Contracts.Errors;
using GrocerEase.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Routing;

namespace GrocerEase.UnitTests.Api;

public class MalformedRequestResponseTests
{
    [Fact]
    public void Create_ReturnsA400ProblemInTheApiErrorShape()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");

        var result = Create(modelState, traceIdentifier: "http-trace");

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, json.StatusCode);
        Assert.Equal("application/problem+json", json.ContentType);
        var body = Assert.IsType<ValidationErrorResponse>(json.Value);
        Assert.Equal(ApiErrorCodes.MalformedRequest, body.Code);
        Assert.Equal(400, body.Status);
        Assert.Equal("Malformed Request", body.Title);
        Assert.Equal("http-trace", body.TraceId);
        Assert.Equal("The Email field is required.", Assert.Single(body.Errors["email"]));
    }

    [Fact]
    public void Create_ReplacesParserTextForJsonPathKeys_ButKeepsThePath()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(
            "$.email",
            "The JSON value could not be converted to GrocerEase.Contracts.Authentication.LoginRequest. Path: $.email | LineNumber: 0 | BytePositionInLine: 12.");

        var body = BodyOf(modelState);

        var message = Assert.Single(body.Errors["$.email"]);
        Assert.DoesNotContain("LineNumber", message);
        Assert.DoesNotContain("GrocerEase", message);
    }

    [Fact]
    public void Create_ReportsARootLevelErrorUnderDollar()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, "A non-empty request body is required.");

        var body = BodyOf(modelState);

        Assert.Equal("A non-empty request body is required.", Assert.Single(body.Errors["$"]));
        Assert.DoesNotContain("", body.Errors.Keys);
    }

    [Fact]
    public void Create_DropsTheBoundParameterNameWhenAnotherErrorExplainsIt()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$", "Expected depth to be zero at the end of the JSON payload.");
        modelState.AddModelError("request", "The request field is required.");

        var body = BodyOf(modelState);

        Assert.Equal(["$"], body.Errors.Keys);
    }

    [Fact]
    public void Create_KeepsTheParameterNameWhenItIsTheOnlyError()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("request", "The request field is required.");

        var body = BodyOf(modelState);

        Assert.Equal(["request"], body.Errors.Keys);
    }

    [Fact]
    public void Create_UsesADefaultMessageWhenAnErrorHasOnlyAnException()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Password", new InvalidOperationException("internal detail"), new EmptyModelMetadataProvider().GetMetadataForType(typeof(string)));

        var body = BodyOf(modelState);

        var message = Assert.Single(body.Errors["password"]);
        Assert.Equal("The input was not valid.", message);
    }

    [Fact]
    public void Create_CollapsesDuplicateMessagesForTheSameKey()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "The Email field is required.");
        modelState.AddModelError("Email", "The Email field is required.");

        var body = BodyOf(modelState);

        Assert.Single(body.Errors["email"]);
    }

    private static ValidationErrorResponse BodyOf(ModelStateDictionary modelState) =>
        Assert.IsType<ValidationErrorResponse>(Assert.IsType<JsonResult>(Create(modelState)).Value);

    private static IActionResult Create(ModelStateDictionary modelState, string traceIdentifier = "http-trace")
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext { TraceIdentifier = traceIdentifier },
            new RouteData(),
            new ControllerActionDescriptor
            {
                Parameters = [new ParameterDescriptor { Name = "request", ParameterType = typeof(object) }],
            },
            modelState);

        return MalformedRequestResponse.Create(actionContext);
    }
}
