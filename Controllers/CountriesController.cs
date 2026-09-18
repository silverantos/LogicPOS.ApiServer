using LogicPOS.ApiServer.DTOs;
using LogicPOS.ApiServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogicPOS.ApiServer.Controllers;

[AllowAnonymous]
[Route("countries")]
[Route("api/countries")]
public sealed class CountriesController : ApiControllerBase
{
    private readonly CountryService _service;

    public CountriesController(CountryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddCountryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Designation))
        {
            return BadRequest(new { errors = new[] { new { name = "designation", reason = "Designation é obrigatório." } } });
        }

        return StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCountryRequest request, CancellationToken cancellationToken)
    {
        return await _service.UpdateAsync(id, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await _service.DeleteAsync(id, cancellationToken) ? Ok() : NotFound();
    }
}
