using Common.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Meezan.Dto.DTOs.Category;
using Meezan.IServices.IService;
using System.Security.Claims;

namespace Meezan.Controllers
{
    [Route("api/categories")]
    [ApiController]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    public class CategoryController : ControllerBase
    {
        private ICategoryService CategoryService { get; }

        public CategoryController(ICategoryService categoryService)
        {
            CategoryService = categoryService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ResponseDto<List<CategoryDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTree([FromQuery] string kind)
            => Ok(await CategoryService.GetTree(User.FindFirstValue(ClaimTypes.NameIdentifier), kind, HttpContext.RequestAborted));

        [HttpPost]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Add(CreateCategoryDto dto)
            => Ok(await CategoryService.Add(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Update(int id, UpdateCategoryDto dto)
        {
            dto.Id = id;
            return Ok(await CategoryService.Update(User.FindFirstValue(ClaimTypes.NameIdentifier), dto, HttpContext.RequestAborted));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ResponseDto<EmptyResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Delete(int id)
            => Ok(await CategoryService.Delete(User.FindFirstValue(ClaimTypes.NameIdentifier), id, HttpContext.RequestAborted));
    }
}
