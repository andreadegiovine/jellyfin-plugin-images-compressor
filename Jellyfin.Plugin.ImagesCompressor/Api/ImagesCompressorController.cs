using System.Collections.Generic;
using System.Net.Mime;
using Jellyfin.Plugin.ImagesCompressor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ImagesCompressor.Api;

/// <summary>
/// API used by the configuration page.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("ImagesCompressor")]
[Produces(MediaTypeNames.Application.Json)]
public class ImagesCompressorController : ControllerBase
{
    private readonly RunHistoryStore _history;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImagesCompressorController"/> class.
    /// </summary>
    /// <param name="history">The run history store.</param>
    public ImagesCompressorController(RunHistoryStore history)
    {
        _history = history;
    }

    /// <summary>
    /// Gets the details of the latest runs, newest first.
    /// </summary>
    /// <response code="200">Runs returned.</response>
    /// <returns>The runs.</returns>
    [HttpGet("Runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RunRecord>> GetRuns()
    {
        return Ok(_history.GetAll());
    }
}
