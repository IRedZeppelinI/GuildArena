using GuildArena.Application.News.CreateNews;
using GuildArena.Application.News.GetLatestNews;
using GuildArena.Application.News.GetNewsArticle;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuildArena.Api.Controllers.News;

[Route("api/[controller]")]
public class NewsController : BaseApiController
{
    private readonly IMediator _mediator;

    public NewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetLatestNews([FromQuery] int limit = 5)
    {
        var result = await _mediator.Send(new GetLatestNewsQuery { Limit = limit });
        return HandleResult(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetNewsArticle(int id)
    {
        var result = await _mediator.Send(new GetNewsArticleQuery { Id = id });
        return HandleResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateNews([FromForm] CreateNewsApiRequest request)
    {
        Stream? fileStream = null;
        string? fileName = null;
        string? contentType = null;

        if (request.Image != null)
        {
            fileStream = request.Image.OpenReadStream();
            fileName = $"{Guid.NewGuid()}_{request.Image.FileName}";
            contentType = request.Image.ContentType;
        }

        var command = new CreateNewsCommand
        {
            Title = request.Title,
            Summary = request.Summary,
            Content = request.Content,
            FileStream = fileStream,
            FileName = fileName,
            ContentType = contentType
        };

        var result = await _mediator.Send(command);
        return HandleResult(result);
    }
}

// Request específico da API para aceitar Multipart Form Data
// Cannot be placed in the Shared project because WebAssembly does not support IFormFile.
public class CreateNewsApiRequest
{
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Content { get; set; }
    public IFormFile? Image { get; set; }
}